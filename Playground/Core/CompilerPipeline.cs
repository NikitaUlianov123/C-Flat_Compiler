using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using Compiler;
using Compiler.Tokens;

namespace Playground.Core;

/// <summary>
/// Drives the real C-Flat compiler (Lexer → Parser → SemanticAnalyzer → CodeGen) and,
/// if asked, runs the emitted assembly. Nothing here changes how the compiler works;
/// it only collects what each stage produced so the UI can show it.
/// </summary>
public static partial class CompilerPipeline
{
    public const string AssemblyName = "CFlatProgram";
    public const int MaxOutputLines = 5_000;
    public const int MaxOutputChars = 500_000;

    [GeneratedRegex(@"\(?\s*row\s+(\d+),\s*column\s+(\d+)\s*\)?", RegexOptions.IgnoreCase)]
    private static partial Regex PositionPattern();

    public static CompileResult Compile(string source, bool run, Action<string> output)
    {
        var result = new CompileResult();
        var timer = new Stopwatch();
        var lines = LineStarts(source);

        // ---------- 1. Lexer ----------
        timer.Restart();
        List<IToken> tokens;
        try
        {
            Lexer.Lex(source, out tokens);
        }
        catch (Exception ex)
        {
            Fail(result, "Lexer", timer, ex);
            return result;
        }
        timer.Stop();

        // Token positions come from adding up token lengths, which is exact even where Row/Column are not.
        var code = new List<SourceToken>();
        var positions = new Dictionary<(int Row, int Column), (int Start, int End)>();
        int offset = 0;
        foreach (var token in tokens)
        {
            int start = offset;
            offset += token.Text.Length;
            if (token is WhiteSpace) continue;

            if (token is not Comment)
            {
                code.Add(new SourceToken(token, start, offset));
                positions.TryAdd((token.Row, token.Column), (start, offset));
            }
            result.Tokens.Add(new TokenInfo(token.GetType().Name, Category(token), token.Text, start, token.Text.Length, token.Row, token.Column));

            if (token is Error)
            {
                result.Diagnostics.Add(new Diagnostic("Lexer", "error", $"Unexpected character '{token.Text}'.", start, offset));
            }
        }

        bool lexOk = !tokens.Any(t => t is Error);
        result.Stages.Add(new StageResult("Lexer", lexOk ? "ok" : "error", timer.Elapsed.TotalMilliseconds,
            $"{result.Tokens.Count(t => t.Category != "comment")} tokens"));
        if (!lexOk) return Skip(result, "Parser", "Semantic", "CodeGen", run ? "Run" : null);

        var builder = new SyntaxTreeBuilder(code);

        // ---------- 2. Parser ----------
        timer.Restart();
        ParseNode? tree;
        List<string> parseMessages;
        try
        {
            // The parser removes tokens from the list it is given, so hand it a copy.
            parseMessages = Parser.Parse(tokens.ToList(), out tree);
        }
        catch (Exception ex)
        {
            Fail(result, "Parser", timer, ex);
            return Skip(result, "Semantic", "CodeGen", run ? "Run" : null);
        }
        timer.Stop();

        foreach (var message in parseMessages)
        {
            AddOnce(result, Locate("Parser", message, source, lines, positions));
        }
        TryBuildTree(result, builder, tree);
        result.Stages.Add(new StageResult("Parser", parseMessages.Count == 0 ? "ok" : "error", timer.Elapsed.TotalMilliseconds,
            parseMessages.Count == 0 ? $"{CountNodes(result.Tree)} AST nodes" : Plural(result.Diagnostics.Count(d => d.Stage == "Parser"), "error")));
        if (parseMessages.Count > 0 || tree is null) return Skip(result, "Semantic", "CodeGen", run ? "Run" : null);

        // ---------- 3. Semantic analysis ----------
        timer.Restart();
        ScopeStack scopes;
        List<string> semanticMessages;
        try
        {
            // Analyze returns the analyzer's shared message list, so copy it.
            semanticMessages = SemanticAnalyzer.Analyze(tree, out scopes).ToList();
        }
        catch (Exception ex)
        {
            Fail(result, "Semantic", timer, ex);
            return Skip(result, "CodeGen", run ? "Run" : null);
        }
        timer.Stop();

        foreach (var message in semanticMessages)
        {
            AddOnce(result, Locate("Semantic", message, source, lines, positions));
        }
        TryBuildTree(result, builder, tree); // again, now with the types the analyzer filled in
        result.Symbols = Symbols(scopes);
        result.Stages.Add(new StageResult("Semantic", semanticMessages.Count == 0 ? "ok" : "error", timer.Elapsed.TotalMilliseconds,
            semanticMessages.Count == 0
                ? "types OK"
                : Plural(result.Diagnostics.Count(d => d.Stage == "Semantic"), "error")));
        if (semanticMessages.Count > 0) return Skip(result, "CodeGen", run ? "Run" : null);

        // ---------- 4. Code generation ----------
        timer.Restart();
        byte[] image;
        try
        {
            // CodeGen saves "<name>.exe" in the working directory (in the browser this is an in-memory file system).
            CodeGen.GenerateCode(tree, scopes, AssemblyName);
            string file = AssemblyName + ".exe";
            image = File.ReadAllBytes(file);
            File.Delete(file);
        }
        catch (Exception ex)
        {
            Fail(result, "CodeGen", timer, ex);
            return Skip(result, run ? "Run" : null);
        }
        timer.Stop();
        var emitTime = timer.Elapsed.TotalMilliseconds;

        result.AssemblyBase64 = Convert.ToBase64String(image);
        try
        {
            result.Il = IlDisassembler.Disassemble(image);
        }
        catch (Exception ex)
        {
            result.Il = $"// Could not disassemble the emitted assembly: {ex.Message}";
        }
        result.Stages.Add(new StageResult("CodeGen", "ok", emitTime, $"{image.Length:N0} bytes"));

        // ---------- 5. Run ----------
        if (run)
        {
            result.Run = Execute(image, output, out var runMs);
            result.Stages.Add(new StageResult("Run", result.Run.Status == "ok" ? "ok" : "error", runMs, result.Run.Status switch
            {
                "ok" => "finished",
                "limit" => "output limit",
                _ => "runtime error",
            }));
        }

        return result;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Emitted programs only use Console (rooted in the csproj) and System.Object.")]
    private static RunResult Execute(byte[] image, Action<string> output, out double milliseconds)
    {
        var timer = Stopwatch.StartNew();
        var writer = new LimitedWriter(output);
        var previous = Console.Out;
        try
        {
            var assembly = Assembly.Load(image);
            var entry = assembly.EntryPoint ?? throw new InvalidOperationException("The assembly has no entry point.");

            Console.SetOut(writer);
            entry.Invoke(null, entry.GetParameters().Length == 0 ? null : [Array.Empty<string>()]);
            return new RunResult("ok", null, timer.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            var inner = ex is TargetInvocationException { InnerException: { } cause } ? cause : ex;
            return inner switch
            {
                OutputLimitException => new RunResult("limit",
                    $"Output limit reached ({MaxOutputLines:N0} lines). The program was stopped.", timer.Elapsed.TotalMilliseconds),
                InvalidProgramException => new RunResult("error",
                    $"InvalidProgramException: the runtime rejected the generated IL. This is a code generator bug. ({inner.Message})",
                    timer.Elapsed.TotalMilliseconds),
                _ => new RunResult("error", $"{inner.GetType().Name}: {inner.Message}", timer.Elapsed.TotalMilliseconds),
            };
        }
        finally
        {
            try { writer.Flush(); } catch (OutputLimitException) { }
            Console.SetOut(previous);
            milliseconds = timer.Elapsed.TotalMilliseconds;
        }
    }

    private static void TryBuildTree(CompileResult result, SyntaxTreeBuilder builder, ParseNode? tree)
    {
        if (tree is null) return;
        try
        {
            result.Tree = builder.Build(tree);
        }
        catch
        {
            // The tree view is a bonus; never let it hide real compiler output.
        }
    }

    private static List<ClassSymbol> Symbols(ScopeStack scopes) =>
        scopes.classes.Values.Select(c => new ClassSymbol(
            c.Name,
            c.Fields.Select(f => $"{f.Value.Type} {f.Key}").ToList(),
            c.Constructors.Select(f => $"{c.Name}({string.Join(", ", f.Parameters.Select(p => p.Type))})").ToList(),
            c.Methods.Select(f => $"{f.ReturnType} {f.Name}({string.Join(", ", f.Parameters.Select(p => p.Type))})").ToList()))
        .ToList();

    /// <summary>
    /// Compiler messages end with "(Row r, Column c)" or "row r, column c". Map that to a character range.
    /// </summary>
    private static Diagnostic Locate(string stage, string message, string source, List<int> lines,
                                     Dictionary<(int Row, int Column), (int Start, int End)> positions)
    {
        var match = PositionPattern().Match(message);
        if (!match.Success) return new Diagnostic(stage, "error", message, -1, -1);

        int row = int.Parse(match.Groups[1].Value);
        int column = int.Parse(match.Groups[2].Value);
        string text = message.Remove(match.Index, match.Length).Trim().TrimEnd('.', ' ') + ".";

        if (positions.TryGetValue((row, column), out var range))
        {
            return new Diagnostic(stage, "error", text, range.Start, range.End);
        }

        // No token starts there. The lexer restarts its column count at 0 after a newline,
        // so add the line's indentation back before turning (row, column) into an offset.
        if (row < 0 || row >= lines.Count) return new Diagnostic(stage, "error", text, -1, -1);
        int lineStart = lines[row];
        int lineEnd = row + 1 < lines.Count ? lines[row + 1] - 1 : source.Length;
        int indent = 0;
        if (row > 0)
        {
            while (lineStart + indent < lineEnd && char.IsWhiteSpace(source[lineStart + indent])) indent++;
        }
        int start = Math.Min(lineStart + indent + column, lineEnd);
        return new Diagnostic(stage, "error", text, start, Math.Min(start + 1, lineEnd));
    }

    /// <summary>The analyzer sometimes reports the same problem more than once; show it once.</summary>
    private static void AddOnce(CompileResult result, Diagnostic diagnostic)
    {
        if (!result.Diagnostics.Contains(diagnostic)) result.Diagnostics.Add(diagnostic);
    }

    private static List<int> LineStarts(string source)
    {
        var starts = new List<int> { 0 };
        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == '\n') starts.Add(i + 1);
        }
        return starts;
    }

    private static void Fail(CompileResult result, string stage, Stopwatch timer, Exception ex)
    {
        timer.Stop();
        result.Stages.Add(new StageResult(stage, "error", timer.Elapsed.TotalMilliseconds, "internal error"));
        result.Diagnostics.Add(new Diagnostic(stage, "internal",
            $"The {stage.ToLowerInvariant()} threw {ex.GetType().Name}: {ex.Message}", -1, -1));
    }

    private static CompileResult Skip(CompileResult result, params string?[] stages)
    {
        foreach (var stage in stages)
        {
            if (stage is not null) result.Stages.Add(new StageResult(stage, "skipped", 0));
        }
        return result;
    }

    private static int CountNodes(TreeNode? node) => node is null ? 0 : 1 + node.Children.Sum(CountNodes);

    private static string Plural(int count, string one, string? many = null) =>
        $"{count} {(count == 1 ? one : many ?? one + "s")}";

    private static string Category(IToken token) => token switch
    {
        Comment => "comment",
        Error => "error",
        Compiler.Tokens.Label => "label",
        StringValue => "string",
        NumericValue => "number",
        Identifier => "identifier",
        OpenParenthesis or CloseParenthesis or OpenCurlyBracket or CloseCurlyBracket
            or Semicolon or Comma or Dot => "punctuation",
        _ when token.GetType().Name.EndsWith("Keyword") => "keyword",
        _ when token.GetType().Name.EndsWith("Operator") => "operator",
        _ => "other",
    };

    private sealed class OutputLimitException : Exception;

    /// <summary>Console writer that streams each finished line out and stops runaway programs.</summary>
    private sealed class LimitedWriter(Action<string> sink) : TextWriter
    {
        private readonly StringBuilder buffer = new();
        private int lines;
        private int chars;

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (++chars > MaxOutputChars) throw new OutputLimitException();
            buffer.Append(value);
            if (value == '\n')
            {
                Flush();
                if (++lines >= MaxOutputLines) throw new OutputLimitException();
            }
        }

        public override void Write(string? value)
        {
            if (value is null) return;
            foreach (char c in value) Write(c);
        }

        public override void Flush()
        {
            if (buffer.Length == 0) return;
            sink(buffer.ToString());
            buffer.Clear();
        }
    }
}

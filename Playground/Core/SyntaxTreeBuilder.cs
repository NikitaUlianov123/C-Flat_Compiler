using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Compiler;
using Compiler.Tokens;

using ProgramNode = Compiler.Program;

namespace Playground.Core;

/// <summary>A token that is not whitespace or a comment, with its exact place in the source.</summary>
public readonly record struct SourceToken(IToken Token, int Start, int End);

/// <summary>
/// Turns the compiler's <see cref="ParseNode"/> tree into a plain tree the UI can draw.
/// The compiler's AST drops most punctuation and keeps some data in properties,
/// so each node's source range is rebuilt from the tokens that are still in its subtree,
/// then widened to cover the keywords, braces and semicolons the parser removed.
/// </summary>
public sealed class SyntaxTreeBuilder
{
    private readonly List<SourceToken> code;
    private readonly Dictionary<IToken, (int Start, int End)> tokenRanges = [];
    private readonly Dictionary<(int Row, int Column), (int Start, int End)> positions = [];

    public SyntaxTreeBuilder(List<SourceToken> code)
    {
        this.code = code;
        foreach (var t in code)
        {
            tokenRanges.TryAdd(t.Token, (t.Start, t.End));
            positions.TryAdd((t.Token.Row, t.Token.Column), (t.Start, t.End));
        }
    }

    public TreeNode Build(ParseNode root)
    {
        var node = Visit(root, role: null);
        node = FillGaps(node, 0, int.MaxValue);
        return node with { Kind = "Program" };
    }

    /// <summary>
    /// Incrementers and empty returns keep no tokens at all, so look for their operator or keyword
    /// in the gap between their neighbours, then grow the parents to cover what was found.
    /// </summary>
    private TreeNode FillGaps(TreeNode node, int lo, int hi)
    {
        var kids = node.Children;
        int floor = lo;
        for (int i = 0; i < kids.Count; i++)
        {
            int ceiling = kids.Skip(i + 1).FirstOrDefault(k => k.Start >= 0)?.Start ?? hi;
            var kid = kids[i];

            if (kid.Start < 0)
            {
                var found = kid.Kind switch
                {
                    "Incrementer" or "ExpressionIncrementer" => FindIncrementer(kid, floor, ceiling),
                    "ReturnStatement" => FindFirst<ReturnKeyword>(floor, ceiling) is { } r ? (r.Start, ThroughSemicolon(r.End)) : ((int, int)?)null,
                    _ => null,
                };
                if (found is { } range) kid = kid with { Start = range.Item1, End = range.Item2 };
            }

            kid = FillGaps(kid, floor, ceiling);
            kids[i] = kid;
            if (kid.End >= 0) floor = kid.End;
        }

        int start = node.Start, end = node.End;
        foreach (var kid in kids.Where(k => k.Start >= 0))
        {
            start = start < 0 ? kid.Start : Math.Min(start, kid.Start);
            end = Math.Max(end, kid.End);
        }
        return node with { Start = start, End = end };
    }

    private (int, int)? FindIncrementer(TreeNode node, int lo, int hi)
    {
        for (int i = IndexAtOrAfter(lo); i < code.Count && code[i].End <= hi; i++)
        {
            if (code[i].Token is not (IncrementOperator or DecrementOperator)) continue;

            bool prefix = node.Detail is { } d && (d.StartsWith("++") || d.StartsWith("--"));
            int first = i, last = i;
            if (prefix)
            {
                while (last + 1 < code.Count && code[last + 1].Token is Identifier or Dot) last++;
            }
            else
            {
                while (first - 1 >= 0 && code[first - 1].Token is Identifier or Dot) first--;
            }
            int end = code[last].End;
            return (code[first].Start, node.Kind == "Incrementer" ? ThroughSemicolon(end) : end);
        }
        return null;
    }

    private SourceToken? FindFirst<T>(int lo, int hi) where T : IToken
    {
        for (int i = IndexAtOrAfter(lo); i < code.Count && code[i].End <= hi; i++)
        {
            if (code[i].Token is T) return code[i];
        }
        return null;
    }

    private TreeNode Visit(object item, string? role)
    {
        if (item is IToken token)
        {
            var (start, end) = tokenRanges.TryGetValue(token, out var r) ? r : (-1, -1);
            return new TreeNode(token.GetType().Name, token.Text, null, role, start, end, []);
        }

        var node = (ParseNode)item;
        var children = new List<TreeNode>();

        foreach (var child in node.Children)
        {
            if (child is null) continue;
            children.Add(Visit(child, RoleOf(node, child)));
        }

        // Some nodes keep their operands in properties instead of Children.
        switch (node)
        {
            case FunctionCall call:
                children.AddRange(call.Parameters.Select(p => Visit(p, "arg")));
                break;
            case ClassInstantiation inst:
                children.AddRange(inst.Parameters.Select(p => Visit(p, "arg")));
                break;
            case ReturnStatement { Value: not null } ret:
                children.Add(Visit(ret.Value, "value"));
                break;
        }

        var (rangeStart, rangeEnd) = RangeOf(node, children);

        string kind = node is ASTNode a ? a.Token.GetType().Name : node.GetType().Name;
        string? type = string.IsNullOrEmpty(node.TypeExpected) ? null : node.TypeExpected;
        if (node is FunctionCall { Target: not null } target) type = target.Target.ReturnType;

        return new TreeNode(kind, DetailOf(node), type, role, rangeStart, rangeEnd, children);
    }

    private (int Start, int End) RangeOf(ParseNode node, List<TreeNode> children)
    {
        int start = int.MaxValue, end = -1;
        foreach (var child in children.Where(c => c.Start >= 0))
        {
            start = Math.Min(start, child.Start);
            end = Math.Max(end, child.End);
        }

        if (node is ASTNode ast)
        {
            if (tokenRanges.TryGetValue(ast.Token, out var own))
            {
                start = Math.Min(start, own.Start);
                end = Math.Max(end, own.End);
            }
        }
        else if (node.Location != (0, 0) || (end >= 0 && start <= FirstLineEnd()))
        {
            // Nodes that never set Location report (0,0), so only trust (0,0) when the subtree is on the first line.
            if (positions.TryGetValue(node.Location, out var located))
            {
                start = Math.Min(start, located.Start);
                end = Math.Max(end, located.End);
            }
        }

        if (end < 0) return (-1, -1);

        // Widen the range over the tokens the parser threw away.
        switch (node)
        {
            case PrintStatement:
                start = PreviousStart<PrintKeyword>(start) ?? start;
                end = ThroughSemicolon(end);
                break;
            case ReturnStatement:
                start = PreviousStart<ReturnKeyword>(start) ?? start;
                end = ThroughSemicolon(end);
                break;
            case IfntStatement:
                start = PreviousStart<IfntKeyword>(start) ?? start;
                end = Math.Max(end, MatchingEnd<OpenCurlyBracket, CloseCurlyBracket>(start));
                break;
            case IfStatement or WhileLoop or ForLoop or FunctionDeclaration or ClassDeclaration:
            case ASTNode { Token: ElseKeyword }:
                end = Math.Max(end, MatchingEnd<OpenCurlyBracket, CloseCurlyBracket>(start));
                break;
            case FunctionCall or ClassInstantiation:
                end = Math.Max(end, MatchingEnd<OpenParenthesis, CloseParenthesis>(start));
                break;
            case VariableDeclaration or GotoStatement:
                // Location is the first token; the name token right after it was removed from Children.
                end = ThroughSemicolon(Math.Max(end, TokenEnd(IndexAtOrAfter(start) + 1)));
                break;
            case VariableAssignment:
                end = ThroughSemicolon(end);
                break;
            case VariableName { Owner.Length: > 0 }:
                // owner . name
                end = Math.Max(end, TokenEnd(IndexAtOrAfter(start) + 2));
                break;
        }

        return (start, end);
    }

    private int? firstLineEnd;
    private int FirstLineEnd()
    {
        firstLineEnd ??= code.Where(t => t.Token.Row == 0).Select(t => t.End).DefaultIfEmpty(-1).Max();
        return firstLineEnd.Value;
    }

    /// <summary>Index of the first code token that starts at or after <paramref name="offset"/>.</summary>
    private int IndexAtOrAfter(int offset)
    {
        int lo = 0, hi = code.Count;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (code[mid].Start < offset) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private int TokenEnd(int index) => index >= 0 && index < code.Count ? code[index].End : -1;

    /// <summary>Start of the nearest <typeparamref name="T"/> token shortly before <paramref name="offset"/>.</summary>
    private int? PreviousStart<T>(int offset) where T : IToken
    {
        int i = IndexAtOrAfter(offset) - 1;
        for (int steps = 0; i >= 0 && steps < 8; i--, steps++)
        {
            var token = code[i].Token;
            if (token is T) return code[i].Start;
            if (token is Semicolon or OpenCurlyBracket or CloseCurlyBracket) break;
        }
        return null;
    }

    /// <summary>Skip closing parentheses after <paramref name="end"/>; include a semicolon if one follows.</summary>
    private int ThroughSemicolon(int end)
    {
        for (int i = IndexAtOrAfter(end); i < code.Count; i++)
        {
            if (code[i].Token is Semicolon) return code[i].End;
            if (code[i].Token is not CloseParenthesis) break;
        }
        return end;
    }

    /// <summary>End of the bracket that closes the first <typeparamref name="TOpen"/> at or after <paramref name="start"/>.</summary>
    private int MatchingEnd<TOpen, TClose>(int start) where TOpen : IToken where TClose : IToken
    {
        int depth = 0;
        for (int i = IndexAtOrAfter(start); i < code.Count; i++)
        {
            if (code[i].Token is TOpen) depth++;
            else if (code[i].Token is TClose && depth > 0 && --depth == 0) return code[i].End;
        }
        return -1;
    }

    private static string? RoleOf(ParseNode parent, object child)
    {
        switch (parent)
        {
            case IfStatement s: return Pick(child, (s.Condition, "cond"), (s.Body, "then"), (s.Followup, "else"));
            case IfntStatement s: return Pick(child, (s.Condition, "cond"), (s.Body, "then"), (s.Followup, "else"));
            case WhileLoop s: return Pick(child, (s.Condition, "cond"), (s.Body, "body"), (s.Followup, "else"));
            case ForLoop s: return Pick(child, (s.Initialization, "init"), (s.Condition, "cond"), (s.Followup, "step"), (s.Body, "body"));
            default: return null;
        }

        static string? Pick(object child, params (ParseNode? Node, string Role)[] options) =>
            options.FirstOrDefault(o => ReferenceEquals(o.Node, child)).Role;
    }

    private static string? DetailOf(ParseNode node)
    {
        switch (node)
        {
            case ASTNode ast:
                return ast.Token.Text;
            case ConstructorDeclaration ctor:
                return $"{ctor.Name}({Params(ctor.Parameters)})";
            case FunctionDeclaration func:
                return $"{func.ReturnType} {func.Name}({Params(func.Parameters)})";
            case FunctionCall call:
                return $"{Qualify(call.Owner, call.Name)}({(call.Parameters.Count > 0 ? "…" : "")})";
            case ClassDeclaration cls:
                return cls.Name;
            case ClassInstantiation inst:
                return $"new {inst.ClassName}({(inst.Parameters.Count > 0 ? "…" : "")})";
            case VariableDeclaration decl:
                return $"{decl.TypeExpected} {decl.Name}";
            case VariableAssignment assign when assign.Name is not null:
                return $"{Qualify(assign.Name.Owner, assign.Name.Name)} =";
            case VariableName name:
                return Qualify(name.Owner, name.Name);
            case Incrementer inc when inc.Name is not null:
                string op = inc.IsIncrement ? "++" : "--";
                string target = Qualify(inc.Name.Owner, inc.Name.Name);
                return inc.IsPre ? op + target : target + op;
            case GotoStatement jump:
                return jump.LabelName;
            case ProgramNode or PrintStatement or ReturnStatement:
                return null;
            default:
                return GenericDetail(node);
        }

        static string Params(List<FunctionParameter> ps) => string.Join(", ", ps.Select(p => $"{p.Type} {p.Name}"));
        static string Qualify(string owner, string name) => string.IsNullOrEmpty(owner) ? name : $"{owner}.{name}";
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "The Compiler assembly is rooted in the csproj.")]
    private static string? GenericDetail(ParseNode node)
    {
        var values = node.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && p.Name != nameof(ParseNode.TypeExpected))
            .Select(p => p.GetValue(node) as string)
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();
        return values.Count == 0 ? null : string.Join(" ", values);
    }
}

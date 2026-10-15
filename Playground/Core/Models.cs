using System.Text.Json.Serialization;

namespace Playground.Core;

/// <summary>Everything the UI needs to show for one pass through the compiler.</summary>
public sealed record CompileResult
{
    public List<StageResult> Stages { get; init; } = [];
    public List<Diagnostic> Diagnostics { get; init; } = [];
    public List<TokenInfo> Tokens { get; init; } = [];
    public TreeNode? Tree { get; set; }
    public List<ClassSymbol> Symbols { get; set; } = [];
    public string? Il { get; set; }
    public string? AssemblyBase64 { get; set; }
    public RunResult? Run { get; set; }
}

public sealed record StageResult(string Name, string Status, double Milliseconds, string? Note = null);

/// <summary>A compiler message. Start/End are character offsets in the source, or -1 when unknown.</summary>
public sealed record Diagnostic(string Stage, string Severity, string Message, int Start, int End);

public sealed record TokenInfo(string Kind, string Category, string Text, int Start, int Length, int Row, int Column);

public sealed record TreeNode(
    string Kind,
    string? Detail,
    string? Type,
    string? Role,
    int Start,
    int End,
    List<TreeNode> Children);

public sealed record ClassSymbol(
    string Name,
    List<string> Fields,
    List<string> Constructors,
    List<string> Methods);

public sealed record RunResult(string Status, string? Error, double Milliseconds);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
                             DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CompileResult))]
public partial class PlaygroundJsonContext : JsonSerializerContext;

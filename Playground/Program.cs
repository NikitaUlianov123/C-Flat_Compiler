using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;

using Playground.Core;

[assembly: SupportedOSPlatform("browser")]

namespace Playground;

public static partial class Interop
{
    // The .NET runtime runs inside a Web Worker (see wwwroot/worker.js).
    // The worker calls Compile for every edit, and with run = true when the user presses Run.
    public static void Main() { }

    [JSExport]
    public static string Compile(string source, bool run)
    {
        var result = CompilerPipeline.Compile(source, run, run ? Output : _ => { });
        return JsonSerializer.Serialize(result, PlaygroundJsonContext.Default.CompileResult);
    }

    /// <summary>Streams program output to the page while the program is still running.</summary>
    [JSImport("output", "playground")]
    private static partial void Output(string text);
}

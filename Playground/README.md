# C-Flat Playground

A static web page that runs the real C-Flat compiler from [`../Compiler`](../Compiler) in the browser.
It does not change the compiler. It only calls it and shows what each stage produced.

```sh
dotnet run                    # local server with the playground
dotnet publish -c Release     # static site in bin/Release/net10.0/publish/wwwroot
```

The published `wwwroot` folder works on any static host. GitHub Pages gets it from
[`.github/workflows/playground.yml`](../.github/workflows/playground.yml).

## How it works

```
index.html + app.js  ──postMessage──▶  worker.js  ──[JSExport]──▶  Interop.Compile (C#)
   Monaco editor                       .NET runtime                   CompilerPipeline
   panels and tabs     ◀── JSON ────   (WebAssembly)                  Lexer → Parser → SemanticAnalyzer → CodeGen
                       ◀── output ──                                  Assembly.Load + run
```

- **`Core/CompilerPipeline.cs`** calls the compiler stage by stage, times each stage, turns the
  compiler's messages into source ranges, and runs the emitted assembly with `Console` sent to the page.
  A program stops after 5,000 lines of output.
- **`Core/SyntaxTreeBuilder.cs`** turns the compiler's `ParseNode` tree into JSON for the tree view.
  The AST drops most punctuation, so it rebuilds each node's source range from the token stream.
- **`Core/IlDisassembler.cs`** is a small ildasm. It reads the emitted PE image with
  `System.Reflection.Metadata` and prints the CIL.
- **`wwwroot/worker.js`** hosts the .NET runtime in a Web Worker. An endless loop then blocks only the
  worker. The page stops the worker after 10 seconds (or when you press Stop) and starts a new one.
- **`wwwroot/app.js`** is the UI. It loads the [Monaco editor](https://microsoft.github.io/monaco-editor/)
  from a CDN. `wwwroot/cflat-monaco.js` has the C-Flat and CIL syntax colors.

## Add an example

Put a `.cflat` file in [`wwwroot/examples`](wwwroot/examples) and add it to
[`wwwroot/examples/index.json`](wwwroot/examples/index.json).

## Notes

- CodeGen writes `<name>.exe` to the working directory. In the browser this is the in-memory
  file system of the .NET runtime. The pipeline reads the file back and deletes it.
- The project keeps the whole `Compiler` assembly and `System.Console` when it trims
  (`TrimmerRootAssembly`), because the compiler uses reflection and compiled programs call
  `Console.WriteLine` overloads that nothing else uses.
- `worker.js` sets `globalThis.dotnetSidecar = true` before it loads `dotnet.js`. Without this flag,
  `dotnet.js` treats the worker as one of its own helper threads and never finishes startup.

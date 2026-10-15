// Runs the .NET runtime, and with it the real C-Flat compiler, in a Web Worker.
// A C-Flat program with an endless loop then blocks only this worker, and the page
// can stop it by terminating the worker and starting a fresh one.

// dotnet.js treats a worker with an onmessage handler as one of its own helper threads, unless the
// worker says that it hosts the whole runtime (a "sidecar"). Say so before dotnet.js loads.
globalThis.dotnetSidecar = true;

const queue = [];
let interop = null;
let currentId = 0;

self.onmessage = (event) => {
  queue.push(event.data);
  pump();
};

function pump() {
  if (!interop) return;
  while (queue.length > 0) {
    const message = queue.shift();
    if (message.type !== 'compile') continue;

    currentId = message.id;
    try {
      const json = interop.Compile(message.source, message.run);
      postMessage({ type: 'result', id: message.id, json });
    } catch (error) {
      postMessage({ type: 'crash', id: message.id, message: String(error?.message ?? error) });
    }
  }
}

async function boot() {
  try {
    const { dotnet } = await import('./_framework/dotnet.js');
    const runtime = await dotnet
      .withModuleConfig({
        onDownloadResourceProgress: (loaded, total) => postMessage({ type: 'progress', loaded, total }),
      })
      .create();

    runtime.setModuleImports('playground', {
      output: (text) => postMessage({ type: 'output', id: currentId, text }),
    });

    const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    interop = exports.Playground.Interop;
    postMessage({ type: 'ready' });
    pump();
  } catch (error) {
    postMessage({ type: 'fatal', message: String(error?.message ?? error) });
  }
}

boot();

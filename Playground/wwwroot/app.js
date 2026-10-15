import { registerLanguages, editorTheme } from './cflat-monaco.js';

const MONACO_BASE = 'https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min/';
const RUN_TIMEOUT_MS = 10_000;
const COMPILE_TIMEOUT_MS = 15_000;
const STAGES = ['Lexer', 'Parser', 'Semantic', 'CodeGen', 'Run'];

const $ = (selector) => document.querySelector(selector);
const isMac = /Mac|iPhone|iPad/.test(navigator.platform);

const storage = {
  get(key) { try { return localStorage.getItem(key); } catch { return null; } },
  set(key, value) { try { localStorage.setItem(key, value); } catch { /* private mode */ } },
};

const state = {
  result: null,          // last CompileResult from the worker
  resultVersion: -1,     // editor model version the result belongs to
  runVersion: -1,        // editor model version of the last run
  running: false,
  runtimeReady: false,
  wantCompile: false,
  wantRun: false,
  busy: false,
  collapsed: new Set(), // syntax tree paths the user folded
  treeRows: [],          // [{ el, start, end, depth }] for cursor sync
  tokenEls: [],
  activeTab: storage.get('cflat.tab') || 'output',
};

let editor = null;
let ilEditor = null;
let monaco = null;
let highlight = null;

// ---------------------------------------------------------------------------
// Compiler worker
// ---------------------------------------------------------------------------

class CompilerWorker {
  constructor() {
    this.nextId = 1;
    this.pending = new Map();
    this.start();
  }

  start() {
    this.ready = false;
    this.worker = new Worker(new URL('./worker.js', import.meta.url), { type: 'module' });
    this.worker.onmessage = (event) => this.onMessage(event.data);
    this.worker.onerror = (event) => {
      event.preventDefault?.();
      this.restart(new Error(event.message || 'The compiler worker crashed.'));
    };
  }

  onMessage(message) {
    switch (message.type) {
      case 'progress':
        onRuntimeProgress(message.loaded, message.total);
        break;
      case 'ready':
        this.ready = true;
        onRuntimeReady();
        break;
      case 'fatal':
        onRuntimeFailed(message.message);
        break;
      case 'output':
        this.pending.get(message.id)?.onOutput?.(message.text);
        break;
      case 'result':
      case 'crash': {
        const job = this.pending.get(message.id);
        if (!job) return;
        clearTimeout(job.timer);
        this.pending.delete(message.id);
        if (message.type === 'result') {
          job.resolve(JSON.parse(message.json));
        } else {
          // The runtime may be broken after a crash (for example a stack overflow), so start a fresh one.
          job.reject(Object.assign(new Error(message.message), { kind: 'crash' }));
          this.restart(null);
        }
        break;
      }
    }
  }

  compile(source, { run = false, onOutput = null, timeout = COMPILE_TIMEOUT_MS } = {}) {
    const id = this.nextId++;
    return new Promise((resolve, reject) => {
      const job = { resolve, reject, onOutput, timer: 0 };
      job.timer = setTimeout(() => {
        this.restart(Object.assign(new Error('timeout'), { kind: 'timeout' }));
      }, timeout);
      this.pending.set(id, job);
      this.worker.postMessage({ type: 'compile', id, source, run });
    });
  }

  /** Throw away the worker (and whatever it was running) and boot a new one. */
  restart(reason) {
    this.worker.terminate();
    for (const job of this.pending.values()) {
      clearTimeout(job.timer);
      job.reject(reason ?? Object.assign(new Error('restarted'), { kind: 'restarted' }));
    }
    this.pending.clear();
    onRuntimeRestarting();
    this.start();
  }
}

const compiler = new CompilerWorker();

// ---------------------------------------------------------------------------
// Scheduling: one request at a time; a run beats a plain compile.
// ---------------------------------------------------------------------------

function requestCompile() {
  state.wantCompile = true;
  pump();
}

function requestRun() {
  if (!state.runtimeReady) return;
  state.wantRun = true;
  pump();
}

function pump() {
  if (!state.runtimeReady || state.busy) return;
  if (state.wantRun) {
    state.wantRun = false;
    state.wantCompile = false;
    doRun();
  } else if (state.wantCompile) {
    state.wantCompile = false;
    doCompile();
  }
}

async function doCompile() {
  state.busy = true;
  const version = editor.getModel().getVersionId();
  setStageBusy(['Lexer', 'Parser', 'Semantic', 'CodeGen']);
  const started = performance.now();
  try {
    const result = await compiler.compile(editor.getValue());
    showResult(result, version, performance.now() - started, false);
  } catch (error) {
    showWorkerError(error, false);
  } finally {
    state.busy = false;
    pump();
  }
}

async function doRun() {
  state.busy = true;
  state.running = true;
  updateRunButton();

  const version = editor.getModel().getVersionId();
  state.runVersion = version;
  startConsole();
  setStageBusy(STAGES);
  const started = performance.now();

  try {
    const result = await compiler.compile(editor.getValue(), {
      run: true,
      timeout: RUN_TIMEOUT_MS,
      onOutput: appendConsole,
    });
    showResult(result, version, performance.now() - started, true);
    finishConsole(result);
  } catch (error) {
    showWorkerError(error, true);
  } finally {
    state.busy = false;
    state.running = false;
    updateRunButton();
    pump();
  }
}

function stopRun() {
  if (!state.running) return;
  compiler.restart(Object.assign(new Error('stopped'), { kind: 'stopped' }));
}

// ---------------------------------------------------------------------------
// Runtime status
// ---------------------------------------------------------------------------

function onRuntimeProgress(loaded, total) {
  const pct = total > 0 ? Math.round((loaded / total) * 100) : 0;
  $('#boot-bar').style.width = `${pct}%`;
  setRuntimeStatus('loading', `Loading .NET runtime… ${pct}%`);
}

function onRuntimeReady() {
  state.runtimeReady = true;
  $('#boot').classList.add('is-hidden');
  setRuntimeStatus('ready', '.NET 10 WebAssembly runtime ready');
  updateRunButton();
  if (editor) requestCompile();
}

function onRuntimeRestarting() {
  state.runtimeReady = false;
  setRuntimeStatus('loading', 'Restarting the .NET runtime…');
  updateRunButton();
}

function onRuntimeFailed(message) {
  const boot = $('#boot');
  boot.classList.remove('is-hidden');
  boot.classList.add('is-error');
  $('#boot-title').textContent = 'The .NET runtime could not start';
  $('#boot-text').textContent = `${message} — The playground needs a recent version of Chrome, Edge, Firefox or Safari with WebAssembly enabled.`;
  setRuntimeStatus('error', 'Runtime failed to start');
}

function setRuntimeStatus(kind, text) {
  const el = $('#runtime-status');
  el.classList.toggle('status-ready', kind === 'ready');
  el.classList.toggle('status-error', kind === 'error');
  $('#runtime-text').textContent = text;
}

function updateRunButton() {
  const button = $('#run');
  button.disabled = !state.runtimeReady && !state.running;
  button.classList.toggle('is-running', state.running);
  button.querySelector('.btn-label').textContent = state.running ? 'Stop' : 'Run';
  button.title = state.running ? 'Stop the program' : `Compile and run (${isMac ? '⌘' : 'Ctrl'}+Enter)`;
}

// ---------------------------------------------------------------------------
// Results
// ---------------------------------------------------------------------------

function showResult(result, version, elapsed, ran) {
  state.result = result;
  state.resultVersion = version;

  renderStages(result.stages, ran);
  renderProblems(result.diagnostics);
  renderTokens(result.tokens);
  renderTree(result.tree);
  renderSymbols(result.symbols);
  renderIl(result.il, result.assemblyBase64);

  const total = result.stages.filter((s) => s.name !== 'Run').reduce((sum, s) => sum + s.milliseconds, 0);
  $('#compile-time').textContent = `Compiled in ${formatMs(total)}`;

  if (ran && result.diagnostics.length > 0 && !result.run) selectTab('problems');
}

function showWorkerError(error, ran) {
  if (error.kind === 'restarted') return; // a newer request replaced this one

  const message = {
    timeout: ran
      ? `Stopped after ${RUN_TIMEOUT_MS / 1000} seconds. The program did not finish. Is there an endless loop?`
      : 'The compiler took too long and was stopped.',
    stopped: 'You stopped the program.',
    crash: `The .NET runtime crashed: ${error.message}. A deep recursion can cause this.`,
  }[error.kind] ?? `Unexpected error: ${error.message}`;

  if (ran) {
    setStage('Run', 'error', error.kind === 'stopped' ? 'stopped' : error.kind === 'timeout' ? 'timed out' : 'crashed');
    for (const name of STAGES.slice(0, 4)) {
      const el = stageEl(name);
      if (el.dataset.status === 'busy') setStage(name, 'ok', 'done');
    }
    finishConsole({ run: { status: 'error', error: message } });
    selectTab('output');
  } else {
    for (const name of STAGES.slice(0, 4)) setStage(name, 'skipped', '—');
    showToast(message);
  }
}

// ----- pipeline -----

const stageEl = (name) => document.querySelector(`.stage[data-stage="${name}"]`);

function setStage(name, status, note, ms = null) {
  const el = stageEl(name);
  el.dataset.status = status;
  el.querySelector('.stage-note').textContent = note;
  el.querySelector('.stage-time').textContent = ms == null ? '' : typeof ms === 'string' ? ms : formatMs(ms);
  el.title = `${name}: ${note}${typeof ms === 'number' ? ` (${formatMs(ms)})` : ''}`;
}

function setStageBusy(names) {
  for (const name of names) setStage(name, 'busy', name === 'Run' ? 'running…' : 'working…');
}

function renderStages(stages, ran) {
  for (const name of STAGES) {
    const stage = stages.find((s) => s.name === name);
    if (stage) {
      if (stage.status === 'skipped') setStage(name, 'skipped', 'skipped');
      else setStage(name, stage.status, stage.note ?? '', stage.milliseconds);
    } else if (name === 'Run' && !ran) {
      const lastRunCurrent = state.runVersion === state.resultVersion;
      if (!lastRunCurrent) setStage('Run', 'idle', 'not run yet', `${isMac ? '⌘' : 'Ctrl'}+Enter`);
    }
  }
}

// ----- problems -----

function renderProblems(diagnostics) {
  const model = editor.getModel();
  const list = $('#problems');
  list.replaceChildren();

  const markers = [];
  for (const d of diagnostics) {
    const located = d.start >= 0;
    const from = located ? model.getPositionAt(d.start) : null;
    const to = located ? model.getPositionAt(Math.max(d.end, d.start + 1)) : null;

    if (located) {
      markers.push({
        severity: monaco.MarkerSeverity.Error,
        message: d.message,
        source: d.stage,
        startLineNumber: from.lineNumber,
        startColumn: from.column,
        endLineNumber: to.lineNumber,
        endColumn: to.column,
      });
    }

    const internal = d.severity === 'internal';
    const li = document.createElement('li');
    const button = document.createElement('button');
    button.type = 'button';
    button.className = `problem${internal ? ' internal' : ''}`;
    if (located) Object.assign(button.dataset, { start: d.start, end: Math.max(d.end, d.start + 1) });
    button.innerHTML = `
      <span class="problem-icon">${internal ? '!' : '✕'}</span>
      <span class="problem-msg"></span>
      <span class="problem-where">${located ? `Ln ${from.lineNumber}, Col ${from.column}` : ''}</span>
      <span class="problem-meta"><span class="problem-stage"></span><span class="problem-hint"></span></span>`;
    button.querySelector('.problem-msg').textContent = d.message;
    button.querySelector('.problem-stage').textContent = d.stage;
    button.querySelector('.problem-hint').textContent = internal
      ? 'The compiler itself threw an exception. This is a compiler bug, not a mistake in your program.'
      : stageHint(d.stage);
    li.append(button);
    list.append(li);
  }

  monaco.editor.setModelMarkers(model, 'cflat', markers);
  $('#problems-empty').hidden = diagnostics.length > 0;
  const badge = $('#problem-count');
  badge.hidden = diagnostics.length === 0;
  badge.textContent = diagnostics.length;
}

function stageHint(stage) {
  return {
    Lexer: 'The tokenizer found a character it does not know.',
    Parser: 'This statement does not match the C-Flat grammar.',
    Semantic: 'Type checking or name lookup failed.',
    CodeGen: 'Code generation failed.',
  }[stage] ?? '';
}

// ----- tokens -----

const CATEGORIES = ['keyword', 'identifier', 'number', 'string', 'operator', 'punctuation', 'label', 'comment'];

function renderLegend() {
  $('#token-legend').innerHTML = CATEGORIES
    .map((c) => `<span class="legend-item cat-${c}" style="--c: var(--syn-${legendVar(c)})">${c}</span>`)
    .join('');
}

const legendVar = (c) => ({ identifier: 'ident', punctuation: 'punct' }[c] ?? c);

function renderTokens(tokens) {
  const model = editor.getModel();
  const container = $('#tokens');
  container.replaceChildren();
  state.tokenEls = [];

  let row = null;
  let currentLine = -1;
  for (const token of tokens) {
    const line = model.getPositionAt(token.start).lineNumber;
    if (line !== currentLine) {
      currentLine = line;
      const lineEl = document.createElement('div');
      lineEl.className = 'token-line';
      lineEl.innerHTML = `<span class="token-line-no">${line}</span><div class="token-row"></div>`;
      row = lineEl.lastElementChild;
      container.append(lineEl);
    }

    const el = document.createElement('button');
    el.type = 'button';
    el.className = `tok cat-${token.category}`;
    el.dataset.start = token.start;
    el.dataset.end = token.start + token.length;
    el.title = `${token.kind}: ${token.text}`;
    const text = document.createElement('span');
    text.className = 'tok-text';
    text.textContent = token.text.length > 40 ? `${token.text.slice(0, 38)}…` : token.text;
    const kind = document.createElement('span');
    kind.className = 'tok-kind';
    kind.textContent = token.kind;
    el.append(text, kind);
    row.append(el);
    state.tokenEls.push({ el, start: token.start, end: token.start + token.length });
  }

  if (tokens.length === 0) {
    container.innerHTML = '<div class="empty"><p>No tokens yet. Type some code.</p></div>';
  }
  syncCursor();
}

// ----- syntax tree -----

function kindClass(kind) {
  if (/Operator$/.test(kind)) return 'k-expr';
  if (kind === 'NumericValue' || kind === 'StringValue' || /^(True|False|Null)Keyword$/.test(kind)) return 'k-value';
  if (kind === 'Identifier' || kind === 'VariableName') return 'k-name';
  return '';
}

function renderTree(tree) {
  const root = $('#tree');
  root.replaceChildren();
  state.treeRows = [];

  if (!tree) {
    root.innerHTML = '<li class="empty"><p>The parser did not produce a tree. Look at the Problems tab.</p></li>';
    return;
  }

  const build = (node, path, depth) => {
    const li = document.createElement('li');
    li.dataset.path = path;
    if (state.collapsed.has(path)) li.classList.add('is-collapsed');

    const row = document.createElement('div');
    row.className = 'tree-node';
    if (node.start >= 0) Object.assign(row.dataset, { start: node.start, end: node.end });

    const twisty = document.createElement('button');
    twisty.type = 'button';
    twisty.className = `twisty${node.children.length ? '' : ' leaf'}`;
    twisty.setAttribute('aria-label', 'Toggle children');
    twisty.innerHTML = '<svg viewBox="0 0 10 10"><path d="M2 3.5l3 3 3-3"/></svg>';
    row.append(twisty);

    if (node.role) row.append(span('node-role', node.role));
    row.append(span(`node-kind ${kindClass(node.kind)}`, node.kind));
    if (node.detail) row.append(span('node-detail', node.detail));
    if (node.type) row.append(span('node-type', node.type));

    li.append(row);
    if (node.start >= 0) state.treeRows.push({ el: row, start: node.start, end: node.end, depth });

    if (node.children.length) {
      const ul = document.createElement('ul');
      node.children.forEach((child, i) => ul.append(build(child, `${path}.${i}`, depth + 1)));
      li.append(ul);
    }
    return li;
  };

  root.append(build(tree, '0', 0));
  syncCursor();
}

function span(className, text) {
  const el = document.createElement('span');
  el.className = className;
  el.textContent = text;
  return el;
}

// ----- symbols -----

function renderSymbols(symbols) {
  const container = $('#symbols');
  container.replaceChildren();
  if (!symbols?.length) {
    container.innerHTML = '<div class="empty"><p>Symbols appear after the semantic analyzer runs.</p></div>';
    return;
  }

  const ordered = [...symbols].sort((a, b) => (a.name === 'Program') - (b.name === 'Program'));
  for (const cls of ordered) {
    const isProgram = cls.name === 'Program';
    const card = document.createElement('section');
    card.className = 'symbol-card';
    card.innerHTML = `<div class="symbol-head"><span class="kw">class</span><span></span><small>${isProgram ? 'top-level code' : ''}</small></div>`;
    card.querySelector('.symbol-head span:nth-child(2)').textContent = cls.name;

    const group = (title, items) => {
      const g = document.createElement('div');
      g.className = 'symbol-group';
      g.innerHTML = `<h4>${title}</h4><ul></ul>`;
      const ul = g.querySelector('ul');
      if (items.length === 0) ul.innerHTML = '<li class="none">none</li>';
      for (const item of items) ul.append(signature(item));
      card.append(g);
    };
    if (!isProgram || cls.fields.length) group('Fields', cls.fields);
    if (!isProgram) group('Constructors', cls.constructors);
    group(isProgram ? 'Functions' : 'Methods', cls.methods);
    container.append(card);
  }
}

/** Color the type names in a signature such as "int Pow(int, int)". */
function signature(text) {
  const li = document.createElement('li');
  const known = new Set(['int', 'string', 'bool', 'void']);
  for (const part of text.split(/(\b[A-Za-z_]\w*\b)/)) {
    if (!part) continue;
    const isType = known.has(part) || (/^[A-Z]/.test(part) && state.result?.symbols?.some((s) => s.name === part));
    li.append(isType ? span('sym-type', part) : document.createTextNode(part));
  }
  return li;
}

// ----- IL -----

let assemblyBytes = null;

function renderIl(il, base64) {
  const text = il ?? '// The IL appears here when the program compiles without errors.';
  if (ilEditor.getValue() !== text) {
    const top = ilEditor.getScrollTop();
    ilEditor.setValue(text);
    ilEditor.setScrollTop(top);
  }
  assemblyBytes = base64 ?? null;
  $('#download').disabled = !assemblyBytes;
  const size = $('#il-size');
  size.hidden = !assemblyBytes;
  if (assemblyBytes) size.textContent = `${atob(assemblyBytes).length.toLocaleString()} bytes`;
}

function downloadAssembly() {
  if (!assemblyBytes) return;
  const bytes = Uint8Array.from(atob(assemblyBytes), (c) => c.charCodeAt(0));
  const url = URL.createObjectURL(new Blob([bytes], { type: 'application/octet-stream' }));
  const a = Object.assign(document.createElement('a'), { href: url, download: 'CFlatProgram.dll' });
  document.body.append(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

// ----- console -----

function startConsole() {
  const pre = $('#console');
  pre.textContent = '';
  pre.hidden = false;
  pre.classList.remove('is-stale');
  $('#output-empty').hidden = true;
  $('#output-stale').hidden = true;
  const status = $('#run-status');
  status.hidden = false;
  status.className = 'run-status';
  status.textContent = 'Compiling and running…';
  $('#output-title').textContent = 'Console';
  selectTab('output');
}

function appendConsole(text) {
  $('#console').append(text);
}

function finishConsole(result) {
  const status = $('#run-status');
  const pre = $('#console');
  const run = result.run;

  if (!run) {
    const crashed = result.diagnostics?.some((d) => d.severity === 'internal');
    status.className = 'run-status error';
    status.textContent = crashed
      ? 'The compiler crashed before the program could run. The Problems tab has the details.'
      : 'The program did not run because it has compile errors. Look at the Problems tab.';
    pre.hidden = pre.textContent.length === 0;
    return;
  }

  const stage = result.stages?.find((s) => s.name === 'Run');
  if (run.status === 'ok') {
    status.className = 'run-status ok';
    status.textContent = `✓ Program exited normally${stage ? ` in ${formatMs(stage.milliseconds)}` : ''}.`;
    if (pre.textContent.length === 0) pre.textContent = '(no output)';
  } else {
    status.className = 'run-status error';
    status.textContent = run.error;
  }
  pre.hidden = pre.textContent.length === 0;
}

function clearConsole() {
  $('#console').textContent = '';
  $('#console').hidden = true;
  $('#run-status').hidden = true;
  $('#output-stale').hidden = true;
  $('#output-empty').hidden = false;
}

// ---------------------------------------------------------------------------
// Tabs, highlighting and cursor sync
// ---------------------------------------------------------------------------

function selectTab(name) {
  state.activeTab = name;
  storage.set('cflat.tab', name);
  for (const tab of document.querySelectorAll('.tab')) {
    const selected = tab.dataset.tab === name;
    tab.setAttribute('aria-selected', String(selected));
    tab.tabIndex = selected ? 0 : -1;
    document.getElementById(tab.getAttribute('aria-controls')).hidden = !selected;
  }
  if (name === 'il') ilEditor?.layout();
  syncCursor();
}

function highlightRange(start, end, reveal = false) {
  const model = editor.getModel();
  const from = model.getPositionAt(start);
  const to = model.getPositionAt(end);
  const range = new monaco.Range(from.lineNumber, from.column, to.lineNumber, to.column);
  highlight.set([{ range, options: { className: 'hl-range', isWholeLine: false } }]);
  if (reveal) editor.revealRangeInCenterIfOutsideViewport(range);
  return range;
}

function clearHighlight() {
  highlight?.clear();
}

function wireHoverTargets(container) {
  container.addEventListener('mouseover', (event) => {
    const target = event.target.closest('[data-start]');
    if (target && isCurrent()) highlightRange(+target.dataset.start, +target.dataset.end, true);
  });
  container.addEventListener('mouseleave', clearHighlight);
  container.addEventListener('click', (event) => {
    if (event.target.closest('.twisty')) return;
    const target = event.target.closest('[data-start]');
    if (!target || !isCurrent()) return;
    const range = highlightRange(+target.dataset.start, +target.dataset.end, true);
    editor.setSelection(range);
    editor.focus();
  });
}

/** Results are for an older version of the text while the user is still typing. */
const isCurrent = () => state.resultVersion === editor.getModel().getVersionId();

function syncCursor() {
  if (!editor || !isCurrent()) return;
  const offset = editor.getModel().getOffsetAt(editor.getPosition());

  if (state.activeTab === 'tree') {
    let best = null;
    for (const row of state.treeRows) {
      row.el.classList.remove('is-cursor');
      if (offset >= row.start && offset <= row.end && (!best || row.depth >= best.depth)) best = row;
    }
    if (best) {
      best.el.classList.add('is-cursor');
      if (!best.el.closest('.is-collapsed')) best.el.scrollIntoView({ block: 'nearest' });
    }
  } else if (state.activeTab === 'tokens') {
    for (const t of state.tokenEls) {
      const active = offset >= t.start && offset < t.end;
      t.el.classList.toggle('is-active', active);
      if (active) t.el.scrollIntoView({ block: 'nearest' });
    }
  }
}

// ---------------------------------------------------------------------------
// Sharing, examples, theme, layout
// ---------------------------------------------------------------------------

function toBase64Url(bytes) {
  let binary = '';
  for (let i = 0; i < bytes.length; i += 0x8000) binary += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function fromBase64Url(text) {
  const binary = atob(text.replace(/-/g, '+').replace(/_/g, '/'));
  return Uint8Array.from(binary, (c) => c.charCodeAt(0));
}

async function encodeSource(source) {
  const bytes = new TextEncoder().encode(source);
  if (!('CompressionStream' in window)) return `src=${toBase64Url(bytes)}`;
  const stream = new Blob([bytes]).stream().pipeThrough(new CompressionStream('deflate-raw'));
  return `code=${toBase64Url(new Uint8Array(await new Response(stream).arrayBuffer()))}`;
}

async function decodeHash(hash) {
  const params = new URLSearchParams(hash.replace(/^#/, ''));
  if (params.has('code')) {
    const stream = new Blob([fromBase64Url(params.get('code'))]).stream().pipeThrough(new DecompressionStream('deflate-raw'));
    return await new Response(stream).text();
  }
  if (params.has('src')) return new TextDecoder().decode(fromBase64Url(params.get('src')));
  return null;
}

async function share() {
  const url = `${location.origin}${location.pathname}#${await encodeSource(editor.getValue())}`;
  try {
    await navigator.clipboard.writeText(url);
    showToast('Link copied. Anyone with the link sees this program.');
  } catch {
    history.replaceState(null, '', url);
    showToast('Link is in the address bar. Copy it from there.');
  }
}

let toastTimer = 0;
function showToast(text) {
  const toast = $('#toast');
  toast.textContent = text;
  toast.classList.add('is-visible');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => toast.classList.remove('is-visible'), 3200);
}

let examples = [];

async function loadExamples() {
  try {
    examples = await (await fetch('examples/index.json')).json();
  } catch {
    examples = [];
  }
  const select = $('#examples');
  for (const example of examples) {
    const option = document.createElement('option');
    option.value = example.file;
    option.textContent = example.title;
    option.title = example.description;
    select.append(option);
  }
}

async function fetchExample(file) {
  const response = await fetch(`examples/${file}`);
  if (!response.ok) throw new Error(`Could not load ${file}`);
  return (await response.text()).replace(/\r\n/g, '\n');
}

async function openExample(file) {
  const source = await fetchExample(file);
  const model = editor.getModel();
  // Use an edit (not setValue) so Ctrl+Z brings the old code back.
  editor.pushUndoStop();
  model.pushEditOperations([], [{ range: model.getFullModelRange(), text: source }], () => null);
  editor.pushUndoStop();
  editor.setPosition({ lineNumber: 1, column: 1 });
  editor.revealLine(1);
  $('#file-name').textContent = file;
  editor.focus();
}

function applyTheme(theme) {
  document.documentElement.dataset.theme = theme;
  if (monaco) monaco.editor.setTheme(editorTheme(theme));
}

function toggleTheme() {
  const next = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
  storage.set('cflat.theme', next);
  applyTheme(next);
}

function setupGutter() {
  const gutter = $('#gutter');
  const bench = $('#workbench');
  const saved = parseFloat(storage.get('cflat.split'));
  if (saved > 20 && saved < 80) bench.style.setProperty('--split', `${saved}%`);

  const setSplit = (pct) => {
    const clamped = Math.min(75, Math.max(25, pct));
    bench.style.setProperty('--split', `${clamped}%`);
    storage.set('cflat.split', String(clamped));
  };

  gutter.addEventListener('pointerdown', (event) => {
    gutter.setPointerCapture(event.pointerId);
    gutter.classList.add('is-dragging');
    const rect = bench.getBoundingClientRect();
    const move = (e) => setSplit(((e.clientX - rect.left) / rect.width) * 100);
    const up = () => {
      gutter.classList.remove('is-dragging');
      gutter.removeEventListener('pointermove', move);
      gutter.removeEventListener('pointerup', up);
    };
    gutter.addEventListener('pointermove', move);
    gutter.addEventListener('pointerup', up);
  });

  gutter.addEventListener('keydown', (event) => {
    if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
    const current = parseFloat(getComputedStyle(bench).getPropertyValue('--split')) || 52;
    setSplit(current + (event.key === 'ArrowLeft' ? -2 : 2));
  });
}

const formatMs = (ms) => (ms < 0.01 ? '<0.01 ms' : ms < 1 ? `${ms.toFixed(2)} ms` : ms < 10 ? `${ms.toFixed(1)} ms` : `${Math.round(ms)} ms`);

// ---------------------------------------------------------------------------
// Startup
// ---------------------------------------------------------------------------

function loadMonaco() {
  window.MonacoEnvironment = {
    getWorkerUrl: () => `data:text/javascript;charset=utf-8,${encodeURIComponent(
      `self.MonacoEnvironment = { baseUrl: '${MONACO_BASE}' }; importScripts('${MONACO_BASE}vs/base/worker/workerMain.js');`)}`,
  };
  return new Promise((resolve, reject) => {
    window.require.config({ paths: { vs: `${MONACO_BASE}vs` } });
    window.require(['vs/editor/editor.main'], () => resolve(window.monaco), reject);
  });
}

async function initialSource() {
  if (location.hash.length > 1) {
    try {
      const shared = await decodeHash(location.hash);
      if (shared != null) {
        history.replaceState(null, '', location.pathname + location.search);
        return { source: shared, name: 'shared.cflat' };
      }
    } catch {
      showToast('That share link is broken, so the default program is shown.');
    }
  }
  const draft = storage.get('cflat.source');
  if (draft) return { source: draft, name: storage.get('cflat.file') || 'program.cflat' };
  return { source: await fetchExample('hello.cflat'), name: 'hello.cflat' };
}

async function main() {
  updateRunButton();
  renderLegend();
  setupGutter();
  $('#run-kbd').textContent = isMac ? '⌘ ↵' : 'Ctrl ↵';
  for (const kbd of document.querySelectorAll('#output-empty .kbd')) kbd.textContent = isMac ? '⌘ ↵' : 'Ctrl ↵';

  const [loadedMonaco, start] = await Promise.all([loadMonaco(), initialSource(), loadExamples()]);
  monaco = loadedMonaco;
  registerLanguages(monaco);

  const common = {
    theme: editorTheme(document.documentElement.dataset.theme),
    fontFamily: "'JetBrains Mono', ui-monospace, Consolas, monospace",
    fontSize: 13.5,
    lineHeight: 22,
    automaticLayout: true,
    minimap: { enabled: false },
    scrollBeyondLastLine: false,
    padding: { top: 12, bottom: 12 },
    renderLineHighlight: 'all',
    smoothScrolling: true,
    fixedOverflowWidgets: true,
  };

  editor = monaco.editor.create($('#editor'), {
    ...common,
    value: start.source,
    language: 'cflat',
    tabSize: 4,
    insertSpaces: true,
    bracketPairColorization: { enabled: true },
    guides: { indentation: true, bracketPairs: false },
    cursorBlinking: 'smooth',
    cursorSmoothCaretAnimation: 'on',
    'semanticHighlighting.enabled': false,
  });
  $('#file-name').textContent = start.name;

  ilEditor = monaco.editor.create($('#il-editor'), {
    ...common,
    value: '// The IL appears here when the program compiles without errors.',
    language: 'cil',
    readOnly: true,
    domReadOnly: true,
    lineNumbers: 'off',
    folding: true,
    renderLineHighlight: 'none',
    glyphMargin: false,
  });

  highlight = editor.createDecorationsCollection();
  document.fonts?.ready.then(() => monaco.editor.remeasureFonts());

  editor.addAction({
    id: 'cflat.run',
    label: 'Run program',
    keybindings: [monaco.KeyMod.CtrlCmd | monaco.KeyCode.Enter],
    run: () => requestRun(),
  });

  let saveTimer = 0;
  let compileTimer = 0;
  editor.onDidChangeModelContent(() => {
    clearTimeout(compileTimer);
    compileTimer = setTimeout(requestCompile, 250);
    clearTimeout(saveTimer);
    saveTimer = setTimeout(() => {
      storage.set('cflat.source', editor.getValue());
      storage.set('cflat.file', $('#file-name').textContent);
    }, 400);
    clearHighlight();
    if (!$('#console').hidden) {
      $('#console').classList.add('is-stale');
      $('#output-stale').hidden = false;
    }
  });

  editor.onDidChangeCursorPosition((event) => {
    $('#cursor-pos').textContent = `Ln ${event.position.lineNumber}, Col ${event.position.column}`;
    syncCursor();
  });

  monaco.languages.registerHoverProvider('cflat', {
    provideHover(model, position) {
      if (!state.result || state.resultVersion !== model.getVersionId()) return null;
      const offset = model.getOffsetAt(position);
      const token = state.result.tokens.find((t) => offset >= t.start && offset < t.start + t.length);
      if (!token || token.category === 'comment') return null;

      let node = null;
      const visit = (n, depth) => {
        if (n.start >= 0 && offset >= n.start && offset < n.end) {
          if (!node || depth >= node.depth) node = { n, depth };
        }
        n.children.forEach((c) => visit(c, depth + 1));
      };
      if (state.result.tree) visit(state.result.tree, 0);

      const lines = [`**${token.kind}** token`];
      if (node && node.depth > 0) {
        const n = node.n;
        lines.push(`AST: \`${n.kind}\`${n.detail ? ` \`${n.detail}\`` : ''}${n.type ? ` : \`${n.type}\`` : ''}`);
      }
      const from = model.getPositionAt(token.start);
      const to = model.getPositionAt(token.start + token.length);
      return {
        range: new monaco.Range(from.lineNumber, from.column, to.lineNumber, to.column),
        contents: lines.map((value) => ({ value })),
      };
    },
  });

  // Toolbar
  $('#run').addEventListener('click', () => (state.running ? stopRun() : requestRun()));
  $('#share').addEventListener('click', share);
  $('#theme').addEventListener('click', toggleTheme);
  $('#download').addEventListener('click', downloadAssembly);
  $('#clear-output').addEventListener('click', clearConsole);
  $('#examples').addEventListener('change', async (event) => {
    const file = event.target.value;
    event.target.value = '';
    if (file) await openExample(file);
  });

  // Tabs and pipeline
  for (const tab of document.querySelectorAll('.tab')) {
    tab.addEventListener('click', () => selectTab(tab.dataset.tab));
  }
  $('.tabs').addEventListener('keydown', (event) => {
    if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
    const tabs = [...document.querySelectorAll('.tab')];
    const index = tabs.findIndex((t) => t.dataset.tab === state.activeTab);
    const next = tabs[(index + (event.key === 'ArrowRight' ? 1 : tabs.length - 1)) % tabs.length];
    selectTab(next.dataset.tab);
    next.focus();
  });
  for (const stage of document.querySelectorAll('.stage')) {
    stage.addEventListener('click', () => {
      const failed = stage.dataset.status === 'error' && stage.dataset.stage !== 'Run';
      selectTab(failed ? 'problems' : stage.dataset.tab);
    });
  }

  // Tree folding
  $('#tree').addEventListener('click', (event) => {
    const twisty = event.target.closest('.twisty');
    if (!twisty || twisty.classList.contains('leaf')) return;
    const li = twisty.closest('li');
    li.classList.toggle('is-collapsed');
    if (li.classList.contains('is-collapsed')) state.collapsed.add(li.dataset.path);
    else state.collapsed.delete(li.dataset.path);
  });
  $('#expand-all').addEventListener('click', () => {
    state.collapsed.clear();
    for (const li of document.querySelectorAll('#tree li.is-collapsed')) li.classList.remove('is-collapsed');
  });
  $('#collapse-all').addEventListener('click', () => {
    for (const li of document.querySelectorAll('#tree li')) {
      if (li.dataset.path && li.dataset.path !== '0' && li.querySelector('ul')) {
        li.classList.add('is-collapsed');
        state.collapsed.add(li.dataset.path);
      }
    }
  });

  wireHoverTargets($('#tokens'));
  wireHoverTargets($('#tree'));
  wireHoverTargets($('#problems'));

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey) && !editor.hasTextFocus()) {
      event.preventDefault();
      requestRun();
    }
  });

  window.matchMedia('(prefers-color-scheme: light)').addEventListener('change', (event) => {
    if (!storage.get('cflat.theme')) applyTheme(event.matches ? 'light' : 'dark');
  });

  selectTab(state.activeTab);
  setStage('Run', 'idle', 'not run yet', `${isMac ? '⌘' : 'Ctrl'}+Enter`);
  if (state.runtimeReady) requestCompile();
}

main().catch((error) => {
  console.error(error);
  onRuntimeFailed(`The page could not start: ${error.message}`);
});

// Monaco editor support for C-Flat (syntax colors, brackets, snippets) and for the IL view.

const KEYWORDS = ['print', 'if', 'else', 'while', 'for', 'true', 'false', 'goto', 'return', 'class', 'new', 'null'];
const TYPES = ['int', 'string', 'bool', 'void'];

// One palette for the editor, the token list and the syntax tree. Keep in sync with styles.css.
const PALETTE = {
  dark: {
    bg: '#11131a', fg: '#e4e6ee', muted: '#7d8499', line: '#1a1d27', select: '#2d2a4a',
    keyword: '#c4a7ff', type: '#7fb4ff', number: '#ff9e6b', string: '#a5e07a', operator: '#6fd8e8',
    comment: '#6a7189', label: '#ffd27a', punct: '#9aa1b5', error: '#ff6b81',
  },
  light: {
    bg: '#ffffff', fg: '#1d2130', muted: '#7a8094', line: '#f4f4fa', select: '#e4defd',
    keyword: '#7340d8', type: '#1f62c9', number: '#c2510a', string: '#2c7d16', operator: '#0b7c93',
    comment: '#8a90a2', label: '#9a6300', punct: '#5d6478', error: '#d3204a',
  },
};

function strip(hex) {
  return hex.replace('#', '');
}

function defineTheme(monaco, name, base, p) {
  monaco.editor.defineTheme(name, {
    base,
    inherit: true,
    rules: [
      { token: '', foreground: strip(p.fg) },
      { token: 'keyword', foreground: strip(p.keyword), fontStyle: 'bold' },
      { token: 'keyword.modifier', foreground: strip(p.keyword) },
      { token: 'type', foreground: strip(p.type) },
      { token: 'type.assembly', foreground: strip(p.muted) },
      { token: 'identifier', foreground: strip(p.fg) },
      { token: 'number', foreground: strip(p.number) },
      { token: 'string', foreground: strip(p.string) },
      { token: 'string.invalid', foreground: strip(p.error) },
      { token: 'operator', foreground: strip(p.operator) },
      { token: 'delimiter', foreground: strip(p.punct) },
      { token: 'comment', foreground: strip(p.comment), fontStyle: 'italic' },
      { token: 'label', foreground: strip(p.label) },
      { token: 'opcode', foreground: strip(p.operator) },
      { token: 'variable', foreground: strip(p.number) },
    ],
    colors: {
      'editor.background': p.bg,
      'editor.foreground': p.fg,
      'editor.lineHighlightBackground': p.line,
      'editor.lineHighlightBorder': p.line,
      'editor.selectionBackground': p.select,
      'editorLineNumber.foreground': p.muted + '99',
      'editorLineNumber.activeForeground': p.fg,
      'editorCursor.foreground': p.keyword,
      'editorIndentGuide.background1': p.muted + '22',
      'editorIndentGuide.activeBackground1': p.muted + '55',
      'editorGutter.background': p.bg,
      'editorWidget.background': p.bg,
      'scrollbarSlider.background': p.muted + '33',
      'scrollbarSlider.hoverBackground': p.muted + '55',
      'scrollbarSlider.activeBackground': p.muted + '77',
      'editorError.foreground': p.error,
      'editorBracketMatch.background': p.select,
      'editorBracketMatch.border': p.muted + '00',
    },
  });
}

export function registerLanguages(monaco) {
  monaco.languages.register({ id: 'cflat', extensions: ['.cflat'], aliases: ['C-Flat', 'cflat'] });

  monaco.languages.setMonarchTokensProvider('cflat', {
    keywords: KEYWORDS,
    types: TYPES,
    tokenizer: {
      root: [
        [/\/\/.*$/, 'comment'],
        [/ifn't\b/, 'keyword'],
        [/[A-Za-z_]\w*:/, 'label'],
        [/[A-Za-z_]\w*/, { cases: { '@keywords': 'keyword', '@types': 'type', '@default': 'identifier' } }],
        [/"([^"\\]|\\.)*$/, 'string.invalid'],
        [/"([^"\\]|\\.)*"/, 'string'],
        [/\d+(\.\d+)?([eE][+-]?\d+)?/, 'number'],
        [/=\?|!=|<=|>=|&&|\|\||\+\+|--|[=<>!+\-*/]/, 'operator'],
        [/[{}()]/, '@brackets'],
        [/[;,.]/, 'delimiter'],
      ],
    },
  });

  monaco.languages.setLanguageConfiguration('cflat', {
    comments: { lineComment: '//' },
    brackets: [['{', '}'], ['(', ')']],
    autoClosingPairs: [
      { open: '{', close: '}' },
      { open: '(', close: ')' },
      { open: '"', close: '"', notIn: ['string', 'comment'] },
    ],
    surroundingPairs: [
      { open: '{', close: '}' },
      { open: '(', close: ')' },
      { open: '"', close: '"' },
    ],
    wordPattern: /ifn't|[A-Za-z_]\w*|-?\d+/g,
  });

  monaco.languages.registerCompletionItemProvider('cflat', {
    provideCompletionItems(model, position) {
      const word = model.getWordUntilPosition(position);
      const range = {
        startLineNumber: position.lineNumber,
        endLineNumber: position.lineNumber,
        startColumn: word.startColumn,
        endColumn: word.endColumn,
      };
      const { Keyword, Snippet, TypeParameter, Variable } = monaco.languages.CompletionItemKind;
      const snippet = monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet;
      const s = (label, insertText, documentation) =>
        ({ label, kind: Snippet, insertText, insertTextRules: snippet, documentation, range });

      const snippets = [
        s('print', 'print(${1:"Hello"});', 'Write a string or a variable to the console.'),
        s('if', 'if(${1:condition})\n{\n\t$0\n}', 'Run the block when the condition is true.'),
        s("ifn't", "ifn't(${1:condition})\n{\n\t$0\n}", 'Run the block when the condition is FALSE.'),
        s('while', 'while(${1:condition})\n{\n\t$0\n}', 'Loop while the condition is true.'),
        s('while-else', 'while(${1:condition})\n{\n\t$2\n}\nelse\n{\n\t$0\n}', 'The else block runs if the loop never ran.'),
        s('for', 'for(int ${1:i} = 0; ${1:i} < ${2:10}; ${1:i}++)\n{\n\t$0\n}', 'Counting loop.'),
        s('function', '${1:void} ${2:Name}(${3})\n{\n\t$0\n}', 'Declare a function.'),
        s('class', 'class ${1:Name}\n{\n\t${2:int} ${3:Field};\n\n\t${1:Name}()\n\t{\n\t\t$0\n\t}\n}', 'Declare a class with a constructor.'),
      ];
      // A keyword that has a snippet with the same name is shown once, as the snippet.
      const snippetNames = new Set(snippets.map((item) => item.label));
      const suggestions = [
        ...snippets,
        ...KEYWORDS.filter((k) => !snippetNames.has(k)).map((k) => ({ label: k, kind: Keyword, insertText: k, range })),
        ...TYPES.map((t) => ({ label: t, kind: TypeParameter, insertText: t, range })),
      ];

      const seen = new Set([...KEYWORDS, ...TYPES]);
      for (const match of model.getValue().matchAll(/[A-Za-z_]\w*/g)) {
        const name = match[0];
        if (seen.has(name) || name === word.word) continue;
        seen.add(name);
        suggestions.push({ label: name, kind: Variable, insertText: name, range });
      }
      return { suggestions };
    },
  });

  monaco.languages.register({ id: 'cil' });
  monaco.languages.setMonarchTokensProvider('cil', {
    tokenizer: {
      root: [
        [/\/\/.*$/, 'comment'],
        [/IL_[0-9a-f]{4}:?/, 'label'],
        [/\.[a-z]+/, 'keyword'],
        [/"([^"\\]|\\.)*"/, 'string'],
        [/\[[A-Za-z0-9_.]+\]/, 'type.assembly'],
        [/\b(public|private|family|assembly|static|instance|hidebysig|specialname|rtspecialname|extends|class|valuetype|init|beforefieldinit|virtual|sealed|abstract)\b/, 'keyword.modifier'],
        [/\b(void|bool|char|u?int(8|16|32|64)|float(32|64)|string|object)\b/, 'type'],
        [/\bV_\d+\b/, 'variable'],
        [/\b[a-z][a-z0-9]*(\.[a-z0-9]+)*\b/, 'opcode'],
        [/-?\b\d+\b/, 'number'],
      ],
    },
  });

  defineTheme(monaco, 'cflat-dark', 'vs-dark', PALETTE.dark);
  defineTheme(monaco, 'cflat-light', 'vs', PALETTE.light);
}

export const editorTheme = (theme) => (theme === 'light' ? 'cflat-light' : 'cflat-dark');

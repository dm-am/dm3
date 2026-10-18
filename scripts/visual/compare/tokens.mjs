// Source code as a sequence of tokens with the comments taken out, so that an
// edit which only touched comments compares equal and any other edit does not.
//
// A comment the tools read is code and stays in the sequence: bundler
// annotations (#__PURE__), @vite-ignore, eslint and TypeScript directives,
// coverage and environment pragmas, and the legal comments (/*!) the build
// keeps, in scripts and styles alike. C# has rules of its own in csharp.mjs.
//
// TypeScript and JavaScript are read by the TypeScript parser and compared as
// a syntax tree plus its tokens: the tree is what tells `return\nx` from
// `return x`, which have the same tokens. A .vue file is compared block by
// block: scripts as above, the template compiled the way a production build
// compiles it (comments dropped, whitespace condensed), styles with their
// comments and blank lines removed. Sass keeps its indentation, which is its
// nesting; plain CSS is read the way the CSS gate reads it.

import { createRequire } from "node:module";
import { join } from "node:path";
import { pathToFileURL } from "node:url";
import { csharpTokens } from "./csharp.mjs";
import { CLIENT } from "./git.mjs";
import { LexError, MARK, Tokens, sourceText } from "./lexing.mjs";

const require = createRequire(join(CLIENT, "package.json"));
let typescriptModule;
let compilerSfcModule;
const typescript = () => (typescriptModule ??= require("typescript"));
const compilerSfc = () => (compilerSfcModule ??= require("vue/compiler-sfc"));

const { canonicalCss } = await import(
  pathToFileURL(
    join(CLIENT, "src", "shared", "lib", "testing", "cssSnapshot.ts"),
  ).href
);

const SCRIPT_DIRECTIVES = [
  /^\/\*\s*[#@]__[A-Z_]+__\s*\*\/$/,
  /@vite-ignore\b/,
  /^\/[/*]\s*eslint(?:-[a-z-]+)?(?:\s|\*\/|$)/,
  /^\/\*\s*(?:globals?|exported)\s/,
  /^\/[/*]\s*@ts-(?:expect-error|ignore|nocheck|check)\b/,
  /^\/\/\/\s*<(?:reference|amd-module|amd-dependency)\b/,
  /@(?:vitest|jest)-environment\b/,
  /^\/[/*]\s*prettier-ignore\b/,
  /^\/[/*]\s*(?:v8|c8|istanbul)\s+ignore\b/,
  /^\/\*!/,
  /@license\b/,
  /@preserve\b/,
  /^\/[/*]\s*@jsx(?:ImportSource|Frag|Runtime)?\b/,
];
const TEMPLATE_DIRECTIVES = [/^\s*eslint(?:-[a-z-]+)?(?:\s|$)/];

/** Comments in a stretch the parser already knows is only whitespace and comments. */
function scanTrivia(tokens, text, from, to, base) {
  let i = from;
  while (i < to) {
    if (text.startsWith("//", i)) {
      let end = text.indexOf("\n", i);
      if (end === -1 || end > to) end = to;
      tokens.comment(text.slice(i, end), base + i, SCRIPT_DIRECTIVES);
      i = end;
    } else if (text.startsWith("/*", i)) {
      const close = text.indexOf("*/", i + 2);
      const end = close === -1 ? to : close + 2;
      tokens.comment(text.slice(i, end), base + i, SCRIPT_DIRECTIVES);
      i = end;
    } else if (i === 0 && text.startsWith("#!")) {
      const end = text.indexOf("\n");
      tokens.push(text.slice(0, end === -1 ? to : end), base);
      i = end === -1 ? to : end;
    } else {
      i++;
    }
  }
}

const SCRIPT_KINDS = {
  ts: "TS",
  tsx: "TSX",
  js: "JS",
  jsx: "JSX",
  json: "JSON",
};

/** TypeScript, JavaScript or JSON(C): the syntax tree and its tokens. */
export function scriptTokens(
  text,
  fileName,
  lang,
  tokens = new Tokens(text),
  base = 0,
) {
  const ts = typescript();
  const kind = SCRIPT_KINDS[lang];
  if (!kind)
    throw new LexError(base, `script language "${lang}" is not supported`);
  const source = ts.createSourceFile(
    fileName,
    text,
    ts.ScriptTarget.Latest,
    false,
    ts.ScriptKind[kind],
  );
  const [problem] = source.parseDiagnostics;
  if (problem) {
    throw new LexError(
      base + problem.start,
      ts.flattenDiagnosticMessageText(problem.messageText, " "),
    );
  }
  const firstDoc = ts.SyntaxKind.FirstJSDocNode;
  const lastDoc = ts.SyntaxKind.LastJSDocNode;
  const endOfFile = ts.SyntaxKind.EndOfFileToken;
  let previousEnd = 0;
  const visit = (node) => {
    // JSDoc is attached to the tree as nodes of its own; it is a comment.
    if (node.kind >= firstDoc && node.kind <= lastDoc) return;
    const children = node.getChildren(source);
    if (children.length === 0) {
      if (node.end <= node.pos && node.kind !== endOfFile) return;
      const start = node.getStart(source);
      scanTrivia(tokens, text, previousEnd, start, base);
      if (node.end > start)
        tokens.push(text.slice(start, node.end), base + start);
      previousEnd = node.end;
      return;
    }
    tokens.push(`${MARK}(${node.kind}`, base + node.pos);
    for (const child of children) visit(child);
    tokens.push(`${MARK})`, base + node.end);
  };
  visit(source);
  return tokens;
}

/**
 * One line of Sass with its comments removed and the whitespace inside it
 * collapsed; the indentation is kept, since it carries the nesting.
 */
function sassCode(line) {
  const indent = line.length - line.trimStart().length;
  let out = "";
  let quote = "";
  let inUrl = false;
  let space = false;
  for (let i = indent; i < line.length; i++) {
    const c = line[i];
    if (quote) {
      out += c;
      if (c === "\\") out += line[++i] ?? "";
      else if (c === quote) quote = "";
      continue;
    }
    if (inUrl) {
      out += c;
      if (c === ")") inUrl = false;
      continue;
    }
    if (c === "/" && line[i + 1] === "/") break;
    if (c === "/" && line[i + 1] === "*") {
      const close = line.indexOf("*/", i + 2);
      if (line[i + 2] === "!") {
        // A legal comment is shipped, so it is code.
        const end = close === -1 ? line.length : close + 2;
        if (space && out !== "") out += " ";
        space = false;
        out += line.slice(i, end);
        i = end - 1;
        continue;
      }
      if (close === -1) break;
      i = close + 1;
      space = true;
      continue;
    }
    if (c === " " || c === "\t") {
      space = true;
      continue;
    }
    if (space && out !== "") out += " ";
    space = false;
    out += c;
    if (c === '"' || c === "'") quote = c;
    else if (/url\($/i.test(out) && !/^\s*["']/.test(line.slice(i + 1)))
      inUrl = true;
  }
  return out === "" ? "" : line.slice(0, indent) + out;
}

/**
 * Sass in the indented syntax, line by line. A line that starts with // or /*
 * is a comment together with every line indented under it, the way the
 * indented syntax reads it; blank lines carry nothing. A legal comment (/*!)
 * is shipped, so it stays, with the lines under it.
 */
export function sassTokens(text, tokens = new Tokens(text), base = 0) {
  let at = 0;
  let commentIndent = -1;
  let legal = false;
  for (const line of text.split("\n")) {
    const lineStart = at;
    at += line.length + 1;
    const body = line.trimStart();
    if (body === "") continue;
    const indent = line.length - body.length;
    if (commentIndent >= 0) {
      if (indent > commentIndent) {
        if (legal) tokens.push(line.trimEnd(), base + lineStart);
        continue;
      }
      commentIndent = -1;
    }
    if (body.startsWith("//") || body.startsWith("/*")) {
      commentIndent = indent;
      legal = body.startsWith("/*!");
      if (legal) tokens.push(line.trimEnd(), base + lineStart);
      continue;
    }
    const code = sassCode(line);
    if (code) tokens.push(code, base + lineStart + indent);
  }
  return tokens;
}

/** Plain CSS, as the CSS gate reads a stylesheet, legal comments kept. */
export function cssTokens(text, tokens = new Tokens(text), base = 0) {
  for (const line of canonicalCss(text, true)) tokens.push(line, base);
  return tokens;
}

/** A single file component, block by block. */
export function vueTokens(text, fileName) {
  const { parse, compileTemplate } = compilerSfc();
  const { descriptor, errors } = parse(text, {
    filename: fileName,
    sourceMap: false,
  });
  if (errors.length > 0) {
    throw new LexError(
      errors[0].loc?.start.offset ?? 0,
      String(errors[0].message ?? errors[0]),
    );
  }
  const tokens = new Tokens(text);
  const header = (block) => {
    const attributes = Object.entries(block.attrs)
      .map(([name, value]) => (value === true ? name : `${name}=${value}`))
      .sort()
      .join(" ");
    tokens.push(`${MARK}<${block.type} ${attributes}>`, block.loc.start.offset);
  };
  const scoped = descriptor.styles.some((style) => style.scoped);
  const template = descriptor.template;
  if (template) {
    header(template);
    const start = template.loc.start.offset;
    if (template.lang && template.lang !== "html") {
      tokens.push(template.content, start);
    } else {
      for (const match of template.content.matchAll(/<!--([\s\S]*?)-->/g)) {
        if (TEMPLATE_DIRECTIVES.some((pattern) => pattern.test(match[1]))) {
          tokens.push(match[0], start + match.index);
        }
      }
      const compiled = compileTemplate({
        source: template.content,
        filename: fileName,
        id: "data-v-00000000",
        scoped,
        slotted: descriptor.slotted,
        isProd: true,
        compilerOptions: { comments: false },
      });
      if (compiled.errors.length > 0) {
        const [error] = compiled.errors;
        throw new LexError(
          start + (error.loc?.start.offset ?? 0),
          String(error.message ?? error),
        );
      }
      // The compiled code has no positions in the file; a difference in it is
      // reported at the template.
      for (const line of compiled.code.split("\n")) tokens.push(line, start);
    }
  }
  for (const script of [descriptor.script, descriptor.scriptSetup]) {
    if (!script) continue;
    header(script);
    scriptTokens(
      script.content,
      fileName,
      script.lang ?? "js",
      tokens,
      script.loc.start.offset,
    );
  }
  for (const style of descriptor.styles) {
    header(style);
    const start = style.loc.start.offset;
    if (style.lang === "sass") sassTokens(style.content, tokens, start);
    else if (!style.lang || style.lang === "css")
      cssTokens(style.content, tokens, start);
    else tokens.push(style.content, start);
  }
  for (const block of descriptor.customBlocks) {
    header(block);
    tokens.push(block.content, block.loc.start.offset);
  }
  return tokens;
}

const LANGUAGES = {
  ".ts": (text, path) => scriptTokens(text, path, "ts"),
  ".mts": (text, path) => scriptTokens(text, path, "ts"),
  ".cts": (text, path) => scriptTokens(text, path, "ts"),
  ".tsx": (text, path) => scriptTokens(text, path, "tsx"),
  ".js": (text, path) => scriptTokens(text, path, "js"),
  ".mjs": (text, path) => scriptTokens(text, path, "js"),
  ".cjs": (text, path) => scriptTokens(text, path, "js"),
  ".jsx": (text, path) => scriptTokens(text, path, "jsx"),
  ".json": (text, path) => scriptTokens(text, path, "json"),
  ".vue": (text, path) => vueTokens(text, path),
  ".cs": (text) => csharpTokens(text),
  ".sass": (text) => sassTokens(text),
  ".css": (text) => cssTokens(text),
};

/** Whether the lexers know a file's language. */
export function isSupported(path) {
  const dot = path.lastIndexOf(".");
  return dot !== -1 && path.slice(dot).toLowerCase() in LANGUAGES;
}

/**
 * The tokens of a file. A byte order mark and CRLF line ends are not code:
 * the text is read without them.
 */
export function tokensOf(path, content) {
  const text = sourceText(content);
  return LANGUAGES[path.slice(path.lastIndexOf(".")).toLowerCase()](text, path);
}

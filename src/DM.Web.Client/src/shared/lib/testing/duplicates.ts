/**
 * Repeated code across the repository, counted as the clean-up baseline counted
 * it, for the ceilings of duplicationRatchet.spec.ts: a line is its code without
 * comments, whitespace, imports and usings; a repeat is a run of eight such
 * lines (60 characters, four lines over three) in two places, overlaps merged,
 * every place but the longest redundant.
 */
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { extname, join } from "node:path";

export type Group =
  | "client"
  | "spec"
  | "e2e"
  | "scripts"
  | "server"
  | "srvtest";
export const GROUPS: readonly Group[] = [
  "client",
  "spec",
  "e2e",
  "scripts",
  "server",
  "srvtest",
];

type Lang = "ts" | "cs" | "css" | "html";
interface Line {
  code: string;
  comment: string;
  block?: string;
}

const CLIENT = "src/DM.Web.Client/";
const WINDOW = 8;
const SCRIPT = [".ts", ".mts", ".js", ".mjs", ".cjs"];

/** Which group a repository path counts in, if any. */
function groupOf(path: string): Group | null {
  const ext = extname(path);
  if (path.startsWith(`${CLIENT}src/`)) {
    if (path.endsWith(".spec.ts")) return "spec";
    return [".vue", ".ts", ".sass", ".css"].includes(ext) ? "client" : null;
  }
  if (path.startsWith(`${CLIENT}e2e/`)) {
    return [".ts", ".js", ".cjs", ".mjs"].includes(ext) ? "e2e" : null;
  }
  if (path.startsWith("scripts/")) {
    return SCRIPT.includes(ext) ? "scripts" : null;
  }
  if (ext !== ".cs") return null;
  if (path.startsWith("src/")) {
    return path.includes("/Migrations/") ? null : "server";
  }
  return path.startsWith("test/") ? "srvtest" : null;
}

// ---------------------------------------------------------------- lexer

const REGEX_PREV = new Set("(,=:[!&|?{};+-*%<>~^".split(""));
const REGEX_KW =
  /(?:^|[^\w$])(return|typeof|case|in|of|delete|void|throw|new|else|do|yield|await|instanceof)$/;

type State =
  | "code"
  | "line"
  | "block"
  | "htmlc"
  | "str"
  | "tpl"
  | "regex"
  | "verb"
  | "raw";

/**
 * Splits source into lines of code and comment. String, template and regex
 * bodies stay in the code; only comments leave it.
 */
class Lexer {
  private readonly lines: Line[] = [];
  private code = "";
  private comment = "";
  private state: State = "code";
  private quote = "";
  private rawQuotes = 0;
  private inClass = false;
  private readonly expressions: number[] = [];
  private recent = "";

  private readonly text: string;
  private readonly lang: Lang;

  constructor(text: string, lang: Lang) {
    this.text = text;
    this.lang = lang;
  }

  run(): Line[] {
    let i = 0;
    while (i < this.text.length) {
      i = this.text[i] === "\n" ? this.newline(i) : this.step(i);
    }
    if (this.code !== "" || this.comment !== "" || !this.text.endsWith("\n")) {
      this.push();
    }
    return this.lines;
  }

  private push() {
    this.lines.push({ code: this.code, comment: this.comment });
    this.code = "";
    this.comment = "";
  }

  /** Code the heuristics may look back at. */
  private keep(s: string) {
    this.code += s;
    this.recent = (this.recent + s).slice(-40);
  }

  /** The body of a literal: code, but not what a regex guess looks back at. */
  private hide(s: string) {
    this.code += s;
  }

  private newline(i: number): number {
    const ends = this.lang !== "html";
    if (this.state === "line" || this.state === "regex") this.state = "code";
    else if (this.state === "str" && ends) this.state = "code";
    this.push();
    this.recent = `${this.recent} `.slice(-40);
    return i + 1;
  }

  private step(i: number): number {
    switch (this.state) {
      case "code":
        return this.inCode(i);
      case "line":
        this.comment += this.text[i];
        return i + 1;
      case "block":
        return this.until(i, "*/");
      case "htmlc":
        return this.until(i, "-->");
      case "str":
        return this.inString(i);
      case "tpl":
        return this.inTemplate(i);
      case "regex":
        return this.inRegex(i);
      case "verb":
        return this.inVerbatim(i);
      case "raw":
        return this.inRaw(i);
    }
  }

  /** Inside a comment that runs to a closing marker. */
  private until(i: number, close: string): number {
    if (this.text.startsWith(close, i)) {
      this.comment += close;
      this.state = "code";
      return i + close.length;
    }
    this.comment += this.text[i];
    return i + 1;
  }

  /** A backslash and the character it escapes, both hidden. */
  private escape(i: number): number {
    this.hide("\\");
    const next = this.text[i + 1];
    if (next === undefined || next === "\n") return i + 1;
    this.hide(next);
    return i + 2;
  }

  private open(state: State, token: string, i: number): number {
    this.state = state;
    this.keep(token);
    return i + token.length;
  }

  private inCode(i: number): number {
    const ch = this.text[i];
    if (this.lang === "html") {
      if (!this.text.startsWith("<!--", i)) return this.open("code", ch, i);
      this.state = "htmlc";
      this.comment += "<!--";
      return i + 4;
    }
    const pair = ch + (this.text[i + 1] ?? "");
    if (pair === "//" || pair === "/*") {
      this.state = pair === "//" ? "line" : "block";
      this.comment += pair;
      return i + 2;
    }
    if (this.lang === "cs" && ch === '"') return this.csQuote(i);
    if (ch === '"' || ch === "'") {
      this.quote = ch;
      return this.open("str", ch, i);
    }
    return this.lang === "ts" ? this.scriptCode(i) : this.open("code", ch, i);
  }

  private csQuote(i: number): number {
    let quotes = 0;
    while (this.text[i + quotes] === '"') quotes++;
    if (/@\$?$|\$@$/.test(this.recent)) return this.open("verb", '"', i);
    if (quotes >= 3) {
      this.rawQuotes = quotes;
      return this.open("raw", '"'.repeat(quotes), i);
    }
    if (quotes === 2) return this.open("code", '""', i);
    this.quote = '"';
    return this.open("str", '"', i);
  }

  /** What only TypeScript has: templates, regex literals, `${}` nesting. */
  private scriptCode(i: number): number {
    const ch = this.text[i];
    if (ch === "`") return this.open("tpl", ch, i);
    if (ch === "/" && this.regexMayOpen()) {
      this.inClass = false;
      return this.open("regex", ch, i);
    }
    const depth = this.expressions.length - 1;
    if (depth >= 0 && ch === "{") this.expressions[depth]++;
    if (depth >= 0 && ch === "}") {
      if (this.expressions[depth] === 0) {
        this.expressions.pop();
        return this.open("tpl", ch, i);
      }
      this.expressions[depth]--;
    }
    return this.open("code", ch, i);
  }

  private regexMayOpen(): boolean {
    const before = this.recent.replace(/\s+$/, "");
    return (
      before === "" ||
      REGEX_PREV.has(before[before.length - 1]) ||
      REGEX_KW.test(before)
    );
  }

  private inString(i: number): number {
    const ch = this.text[i];
    if (ch === "\\") return this.escape(i);
    if (ch === this.quote) return this.open("code", ch, i);
    this.hide(ch);
    return i + 1;
  }

  private inTemplate(i: number): number {
    const ch = this.text[i];
    if (ch === "\\") return this.escape(i);
    if (ch === "`") return this.open("code", ch, i);
    if (ch === "$" && this.text[i + 1] === "{") {
      this.expressions.push(0);
      return this.open("code", "${", i);
    }
    this.hide(ch);
    return i + 1;
  }

  private inRegex(i: number): number {
    const ch = this.text[i];
    if (ch === "\\") return this.escape(i);
    if (ch === "/" && !this.inClass) return this.open("code", ch, i);
    if (ch === "[") this.inClass = true;
    else if (ch === "]") this.inClass = false;
    this.hide(ch);
    return i + 1;
  }

  private inVerbatim(i: number): number {
    if (this.text.startsWith('""', i)) {
      this.hide('""');
      return i + 2;
    }
    if (this.text[i] === '"') return this.open("code", '"', i);
    this.hide(this.text[i]);
    return i + 1;
  }

  private inRaw(i: number): number {
    if (this.text[i] !== '"') {
      this.hide(this.text[i]);
      return i + 1;
    }
    let quotes = 0;
    while (this.text[i + quotes] === '"') quotes++;
    const run = '"'.repeat(quotes);
    if (quotes >= this.rawQuotes) return this.open("code", run, i);
    this.hide(run);
    return i + quotes;
  }
}

const lex = (text: string, lang: Lang) => new Lexer(text, lang).run();

/** The blocks of a single-file component, each read in its own language. */
function vueLines(raw: string[]): Line[] {
  const out: Line[] = new Array(raw.length);
  let outside: number[] = [];
  const flush = () => {
    const read = lex(`${outside.map((at) => raw[at]).join("\n")}\n`, "html");
    outside.forEach((at, k) => {
      out[at] = { ...(read[k] ?? { code: "", comment: "" }), block: "outside" };
    });
    outside = [];
  };
  const tagLine = (at: number, tag: string) => {
    out[at] = { code: raw[at], comment: "", block: `${tag}-tag` };
  };
  let i = 0;
  while (i < raw.length) {
    const tag = raw[i].match(/^<(template|script|style)\b/)?.[1];
    if (!tag) {
      outside.push(i++);
      continue;
    }
    if (outside.length) flush();
    let open = i;
    while (open < raw.length && !/>\s*$/.test(raw[open])) tagLine(open++, tag);
    const close = new RegExp(`^</${tag}>\\s*$`);
    let end = open + 1;
    while (end < raw.length && !close.test(raw[end])) end++;
    const lang = tag === "template" ? "html" : tag === "style" ? "css" : "ts";
    const inner = raw.slice(open + 1, end);
    const read = inner.length ? lex(`${inner.join("\n")}\n`, lang) : [];
    tagLine(open, tag);
    inner.forEach((_, k) => {
      out[open + 1 + k] = {
        ...(read[k] ?? { code: "", comment: "" }),
        block: tag,
      };
    });
    if (end < raw.length) tagLine(end, tag);
    i = end + 1;
  }
  if (outside.length) flush();
  return out;
}

function linesOf(text: string, ext: string): Line[] {
  const bare = text.charCodeAt(0) === 0xfeff ? text.slice(1) : text;
  const clean = bare.replace(/\r\n?/g, "\n");
  if (ext === ".vue") {
    const raw = clean.split("\n");
    if (raw.length && raw[raw.length - 1] === "") raw.pop();
    return vueLines(raw);
  }
  const lang: Lang =
    ext === ".cs"
      ? "cs"
      : [".sass", ".scss", ".css"].includes(ext)
        ? "css"
        : "ts";
  return lex(clean, lang);
}

// ---------------------------------------------------------------- repeats

interface Normal {
  text: string;
  line: number;
}

const CS_IMPORT =
  /^(global\s+)?using\s+(static\s+)?[\w.]+(\s*=\s*[\w.<>, ]+)?\s*;$|^namespace\s+[\w.]+\s*;$/;
const TS_IMPORT_OPEN =
  /^import\b|^export\s*(\*|\{[^}]*\}?)\s*(from\b|$)|^export\s+type\s*\{/;
const TS_IMPORT_CLOSE =
  /\bfrom\s*['"][^'"]+['"]\s*;?$|^import\s*['"][^'"]+['"]\s*;?$|;$/;

/** The lines a repeat is looked for in: code only, no whitespace, no imports. */
function normalLines(text: string, ext: string): Normal[] {
  const script = SCRIPT.includes(ext);
  const out: Normal[] = [];
  let inImport = false;
  linesOf(text, ext).forEach((line, i) => {
    const raw = line.code.trim();
    const compact = line.code.replace(/\s+/g, "");
    if (!compact) return;
    let isImport = false;
    if (ext === ".cs") isImport = CS_IMPORT.test(raw);
    else if (script || (ext === ".vue" && line.block === "script")) {
      if (!inImport && TS_IMPORT_OPEN.test(raw)) inImport = true;
      isImport = inImport;
      if (inImport && TS_IMPORT_CLOSE.test(raw)) inImport = false;
    }
    if (!isImport) out.push({ text: compact, line: i + 1 });
  });
  return out;
}

interface Place {
  file: number;
  from: number;
  to: number;
}

/** One place of a repeated piece, lines numbered from one. */
interface Spot {
  path: string;
  from: number;
  to: number;
}

/** Code found in more than one place, and what it costs: every place but its longest. */
export interface Piece {
  cost: number;
  places: Spot[];
}

function solid(lines: Normal[], start: number): boolean {
  let chars = 0;
  let long = 0;
  for (let k = start; k < start + WINDOW; k++) {
    chars += lines[k].text.length;
    if (lines[k].text.length >= 4) long++;
  }
  return chars >= 60 && long >= 4;
}

/** Every window of eight lines, keyed by its content, with where it occurs. */
function windows(files: Normal[][]) {
  const ids = new Map<string, number>();
  const idOf = (text: string) => {
    if (!ids.has(text)) ids.set(text, ids.size);
    return ids.get(text) as number;
  };
  const occurrences = new Map<string, number[]>();
  const keys = files.map((lines, file) => {
    const fileIds = lines.map((line) => idOf(line.text));
    const own: (string | null)[] = [];
    for (let s = 0; s + WINDOW <= lines.length; s++) {
      const key = solid(lines, s)
        ? fileIds.slice(s, s + WINDOW).join(",")
        : null;
      own.push(key);
      if (key !== null) {
        const at = occurrences.get(key) ?? [];
        at.push(file * 1e7 + s);
        occurrences.set(key, at);
      }
    }
    return own;
  });
  return { keys, occurrences };
}

/** Runs of windows repeated together, as the places each run covers. */
function fragments(files: Normal[][]): Place[][] {
  const { keys, occurrences } = windows(files);
  const repeated = (file: number, s: number) => {
    const key = keys[file][s];
    const at = key == null ? undefined : occurrences.get(key);
    return at && at.length >= 2 ? at : null;
  };
  const shifted = (a: number[], b: number[], d: number) =>
    a.length === b.length && a.every((v, i) => b[i] === v + d);
  const out: Place[][] = [];
  for (const at of occurrences.values()) {
    if (at.length < 2) continue;
    const file = Math.floor(at[0] / 1e7);
    const start = at[0] % 1e7;
    const before = start > 0 ? repeated(file, start - 1) : null;
    if (before && shifted(before, at, 1)) continue;
    let length = 1;
    while (start + length < keys[file].length) {
      const next = repeated(file, start + length);
      if (!next || !shifted(at, next, length)) break;
      length++;
    }
    out.push(
      at.map((v) => {
        const lines = files[Math.floor(v / 1e7)];
        const s = v % 1e7;
        const last = Math.min(lines.length - 1, s + length + WINDOW - 2);
        return {
          file: Math.floor(v / 1e7),
          from: lines[s].line,
          to: lines[last].line,
        };
      }),
    );
  }
  return out;
}

/** Merged spans of one file, overlapping ones joined. */
function merged(spans: [number, number][]): [number, number][] {
  const out: [number, number][] = [];
  for (const [from, to] of [...spans].sort((a, b) => a[0] - b[0])) {
    const last = out[out.length - 1];
    if (last && from <= last[1]) last[1] = Math.max(last[1], to);
    else out.push([from, to]);
  }
  return out;
}

/**
 * The repeated pieces of one group: fragments whose places overlap in a file
 * are one piece, and a piece costs every place but its longest.
 */
function redundantPieces(files: Normal[][], paths: string[]): Piece[] {
  const frags = fragments(files);
  const parent = frags.map((_, i) => i);
  const find = (x: number): number => {
    while (parent[x] !== x) x = parent[x] = parent[parent[x]];
    return x;
  };
  const byFile = new Map<
    number,
    { frag: number; from: number; to: number }[]
  >();
  frags.forEach((places, frag) =>
    places.forEach(({ file, from, to }) => {
      byFile.set(file, [...(byFile.get(file) ?? []), { frag, from, to }]);
    }),
  );
  for (const spans of byFile.values()) {
    spans.sort((a, b) => a.from - b.from);
    let current: { frag: number; to: number } | null = null;
    for (const span of spans) {
      if (current && span.from <= current.to) {
        parent[find(span.frag)] = find(current.frag);
        current.to = Math.max(current.to, span.to);
      } else current = { frag: span.frag, to: span.to };
    }
  }
  const pieces = new Map<number, Map<number, [number, number][]>>();
  frags.forEach((places, frag) => {
    const piece =
      pieces.get(find(frag)) ?? new Map<number, [number, number][]>();
    pieces.set(find(frag), piece);
    for (const { file, from, to } of places) {
      piece.set(file, [...(piece.get(file) ?? []), [from, to]]);
    }
  });
  return [...pieces.values()]
    .map((piece) => {
      const places = [...piece].flatMap(([file, spans]) =>
        merged(spans).map(([from, to]) => ({ path: paths[file], from, to })),
      );
      const lengths = places.map(({ from, to }) => to - from + 1);
      const cost = lengths.reduce((a, b) => a + b, 0) - Math.max(...lengths);
      return { cost, places };
    })
    .sort((a, b) => b.cost - a.cost);
}

export const redundantLines = (pieces: readonly Piece[]): number =>
  pieces.reduce((sum, piece) => sum + piece.cost, 0);

/** The files a commit would carry, in git's order, as repository paths. */
export function committedFiles(root: string): string[] {
  const listed = execFileSync(
    "git",
    ["ls-files", "-z", "--cached", "--others", "--exclude-standard"],
    { cwd: root, maxBuffer: 64 << 20 },
  ).toString("utf8");
  return [...new Set(listed.split("\0").filter(Boolean))].filter((path) =>
    existsSync(join(root, path)),
  );
}

/**
 * The repeated pieces of every group, each group measured on its own, the
 * costliest first.
 */
export function measureDuplicates(
  root: string,
  paths: readonly string[] = committedFiles(root),
): Record<Group, Piece[]> {
  const byGroup = new Map<Group, { lines: Normal[][]; paths: string[] }>(
    GROUPS.map((g) => [g, { lines: [], paths: [] }]),
  );
  for (const path of paths) {
    const group = groupOf(path);
    if (!group) continue;
    const text = readFileSync(join(root, path), "utf8");
    const files = byGroup.get(group);
    files?.lines.push(normalLines(text, extname(path)));
    files?.paths.push(path);
  }
  return Object.fromEntries(
    GROUPS.map((g) => {
      const files = byGroup.get(g) ?? { lines: [], paths: [] };
      return [g, redundantPieces(files.lines, files.paths)];
    }),
  ) as Record<Group, Piece[]>;
}

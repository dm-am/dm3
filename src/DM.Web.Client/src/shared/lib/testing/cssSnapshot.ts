/**
 * The styles of a client build as the cascade reads them: every sheet, the
 * styles and sheet links of each page, and the order each chunk brings its
 * sheets. Normalized only: whitespace, comments and blocks left empty by them,
 * the last optional semicolon, and scope ids renumbered by first appearance
 * (the production id hashes the whole source). Order is kept, since the cascade
 * reads it, so the build is made with the CSS minifier off. Markup and the
 * preload lists are compare-code --build's.
 *
 * scripts/visual/compare-css.mjs loads this with Node's type stripping: node:
 * imports only, and no syntax beyond type annotations.
 */
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { join, relative, sep } from "node:path";

/**
 * The prefix of a line that opens a section of a snapshot.
 *
 * @public scripts/visual/compare-css.mjs imports it by URL to count sections,
 * and knip does not read that script.
 */
export const SECTION = "### ";

/**
 * Renumbers the scope ids of one sheet or chunk by first appearance, in both
 * forms (data-v-<id> in selectors, -<id> on @keyframes names), with labels
 * (-~1, -~2) no source can spell.
 */
export function relabelScopeIds(text: string): string {
  const ids = new Set<string>();
  for (const [, id] of text.matchAll(/data-v-([0-9a-f]{8})/g)) {
    if (id) ids.add(id);
  }
  // A sheet can hold a component's keyframes and none of its selectors.
  for (const [, id] of text.matchAll(
    /@(?:-[a-z]+-)?keyframes\s+[\w-]*-([0-9a-f]{8})(?!\w)/g,
  )) {
    if (id) ids.add(id);
  }
  const labels = new Map<string, number>();
  return text.replace(
    /-([0-9a-f]{8})(?![0-9A-Za-z_])/g,
    (whole, id: string) => {
      if (!ids.has(id)) return whole;
      if (!labels.has(id)) labels.set(id, labels.size + 1);
      return `-~${labels.get(id)}`;
    },
  );
}

/**
 * A declaration with the whitespace around its first colon removed. Only in a
 * declaration: in a selector, `a :hover` and `a:hover` are different things.
 */
function canonicalDeclaration(statement: string): string {
  if (statement.startsWith("@")) return statement;
  let depth = 0;
  let quote = "";
  for (let i = 0; i < statement.length; i++) {
    const c = statement[i];
    if (quote) {
      if (c === "\\") i++;
      else if (c === quote) quote = "";
    } else if (c === '"' || c === "'") quote = c;
    else if (c === "(") depth++;
    else if (c === ")") depth--;
    else if (c === ":" && depth === 0) {
      return `${statement.slice(0, i).trim()}:${statement.slice(i + 1).trim()}`;
    }
  }
  return statement;
}

/**
 * One stylesheet as lines: `selector{`, a declaration per line ending in `;`,
 * and `}`; whitespace collapsed and dropped where CSS never reads it, kept
 * before and after parentheses where it can be a combinator.
 *
 * `keepLegalComments` keeps each /*! comment in place: the production build
 * ships it, so the comparison of sources counts it as code. The CSS gate
 * leaves it out, since the cascade does not read it.
 */
export function canonicalCss(css: string, keepLegalComments = false): string[] {
  const writer: CssWriter = {
    lines: [],
    statement: "",
    space: false,
    glued: true,
    depth: 0,
    keepLegalComments,
  };
  for (let i = 0; i < css.length; ) i = readToken(writer, css, i);
  closeDeclaration(writer);
  return writer.lines;
}

/** What canonicalCss has written so far and the statement it has in progress. */
interface CssWriter {
  lines: string[];
  statement: string;
  /** Whitespace was read since the last token. */
  space: boolean;
  /** The last token takes no space after it. */
  glued: boolean;
  /** Parentheses open around the position. */
  depth: number;
  keepLegalComments: boolean;
}

/**
 * Reads the token that opens at a position and returns the position after it,
 * or undefined when a token of another kind opens there.
 */
type CssReader = (
  writer: CssWriter,
  css: string,
  i: number,
) => number | undefined;

/**
 * Tried in turn at every position; the first that recognizes what opens there
 * reads it, and a character none of them recognizes is a token of its own.
 */
const CSS_READERS: CssReader[] = [
  readComment,
  readQuoted,
  readUnquotedUrl,
  readSpace,
  readEscape,
  readStatementEnd,
  readPunctuation,
];

function readToken(writer: CssWriter, css: string, i: number): number {
  for (const read of CSS_READERS) {
    const next = read(writer, css, i);
    if (next !== undefined) return next;
  }
  put(writer, css[i], false, false);
  return i + 1;
}

function put(
  writer: CssWriter,
  text: string,
  glueBefore: boolean,
  glueAfter: boolean,
): void {
  if (writer.space && !glueBefore && !writer.glued) writer.statement += " ";
  writer.statement += text;
  writer.space = false;
  writer.glued = glueAfter;
}

function resetStatement(writer: CssWriter): void {
  writer.statement = "";
  writer.space = false;
  writer.glued = true;
}

/** Closes the declaration in progress, if there is one. */
function closeDeclaration(writer: CssWriter): void {
  if (writer.statement)
    writer.lines.push(`${canonicalDeclaration(writer.statement)};`);
  resetStatement(writer);
}

function readComment(writer: CssWriter, css: string, i: number) {
  if (!(css[i] === "/" && css[i + 1] === "*")) return undefined;
  const close = css.indexOf("*/", i + 2);
  const end = close === -1 ? css.length : close + 2;
  if (writer.keepLegalComments && css[i + 2] === "!")
    put(writer, css.slice(i, end), false, false);
  else writer.space = true;
  return end;
}

function readQuoted(writer: CssWriter, css: string, i: number) {
  const c = css[i];
  if (c !== '"' && c !== "'") return undefined;
  let j = i + 1;
  while (j < css.length && css[j] !== c) j += css[j] === "\\" ? 2 : 1;
  put(writer, css.slice(i, j + 1), false, false);
  return j + 1;
}

/**
 * An unquoted url is one token: neither "/*" nor ";" inside it means what it
 * means elsewhere.
 */
function readUnquotedUrl(writer: CssWriter, css: string, i: number) {
  const c = css[i];
  if (
    !(
      (c === "u" || c === "U") &&
      /^url\(\s*[^\s"')]/i.test(css.slice(i, i + 64))
    )
  )
    return undefined;
  const close = css.indexOf(")", i);
  put(
    writer,
    css
      .slice(i, close + 1)
      .replace(/^(url\()\s+/i, "$1")
      .replace(/\s+\)$/, ")"),
    false,
    false,
  );
  return close + 1;
}

function readSpace(writer: CssWriter, css: string, i: number) {
  if (!/\s/.test(css[i])) return undefined;
  writer.space = true;
  return i + 1;
}

function readEscape(writer: CssWriter, css: string, i: number) {
  if (css[i] !== "\\") return undefined;
  put(writer, css.slice(i, i + 2), false, false);
  return i + 2;
}

/** A brace or a semicolon outside parentheses, which ends a statement. */
function readStatementEnd(writer: CssWriter, css: string, i: number) {
  if (writer.depth !== 0) return undefined;
  const c = css[i];
  if (c === "{") {
    writer.lines.push(`${writer.statement}{`);
    resetStatement(writer);
  } else if (c === "}") {
    closeDeclaration(writer);
    // An empty block applies nothing; an @media left empty by it goes the
    // same way when its own brace closes.
    if (writer.lines[writer.lines.length - 1]?.endsWith("{"))
      writer.lines.pop();
    else writer.lines.push("}");
  } else if (c === ";") closeDeclaration(writer);
  else return undefined;
  return i + 1;
}

function readPunctuation(writer: CssWriter, css: string, i: number) {
  const c = css[i];
  if (c === ",") put(writer, c, true, true);
  else if (c === "(") {
    writer.depth++;
    put(writer, c, false, true);
  } else if (c === ")") {
    writer.depth--;
    put(writer, c, true, false);
  } else return undefined;
  return i + 1;
}

function filesUnder(root: string): string[] {
  const found: string[] = [];
  const walk = (dir: string) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = join(dir, entry.name);
      if (entry.isDirectory()) walk(full);
      else found.push(relative(root, full).split(sep).join("/"));
    }
  };
  walk(root);
  return found.sort();
}

interface ManifestChunk {
  file: string;
  css?: string[];
  imports?: string[];
}

/** The value of one attribute of an HTML tag, or undefined without it. */
function attribute(tag: string, name: string): string | undefined {
  const match = new RegExp(
    `\\s${name}\\s*=\\s*(?:"([^"]*)"|'([^']*)'|([^\\s"'>]+))`,
    "i",
  ).exec(tag);
  return match ? (match[1] ?? match[2] ?? match[3]) : undefined;
}

/**
 * What a page puts into the cascade, in document order: each linked sheet
 * (with its media) and each <style>, after the styles index.html writes;
 * comments and script text are passed over.
 */
function pageStyles(html: string): string[] {
  const lines: string[] = [];
  const tags =
    /<!--[\s\S]*?-->|<script\b[^>]*>[\s\S]*?<\/script\s*>|<link\b[^>]*>|<style\b([^>]*)>([\s\S]*?)<\/style\s*>/gi;
  for (const [tag, styleAttributes, style] of html.matchAll(tags)) {
    const kind = tag.slice(0, 5).toLowerCase();
    if (kind === "<styl") {
      const attributes = (styleAttributes ?? "").replace(/\s+/g, " ").trimEnd();
      lines.push(
        `<style${attributes}>`,
        ...canonicalCss(style ?? ""),
        "</style>",
      );
    } else if (kind === "<link") {
      const rel = attribute(tag, "rel")?.toLowerCase().split(/\s+/) ?? [];
      if (!rel.includes("stylesheet")) continue;
      const media = attribute(tag, "media");
      lines.push(
        `<link> ${attribute(tag, "href") ?? tag}${media ? ` media=${media}` : ""}`,
      );
    }
  }
  return lines;
}

/**
 * The sheets a chunk brings, in Vite's preload order: depth first through its
 * imports, each chunk's sheets after its imports', every one once (addDeps).
 */
function chunkSheets(
  manifest: Record<string, ManifestChunk>,
  key: string,
): string[] {
  const sheets = new Set<string>();
  const visited = new Set<string>();
  const visit = (name: string) => {
    const chunk = manifest[name];
    if (!chunk || visited.has(name)) return;
    visited.add(name);
    chunk.imports?.forEach(visit);
    chunk.css?.forEach((sheet) => sheets.add(sheet));
  };
  visit(key);
  return [...sheets];
}

/**
 * The snapshot of one build output, as lines:
 * - `### page <file>`: the sheets it links and the styles it holds;
 * - `### chunk <file>`: the sheets loading it brings (needs the manifest);
 * - `### sheet <file>`: every stylesheet, as canonicalCss reads it.
 */
export function cssSnapshot(outDir: string): string[] {
  const lines: string[] = [];
  const files = filesUnder(outDir);
  for (const page of files.filter((file) => file.endsWith(".html"))) {
    lines.push(
      `${SECTION}page ${page}`,
      ...pageStyles(readFileSync(join(outDir, page), "utf8")),
    );
  }
  const manifestFile = join(outDir, ".vite", "manifest.json");
  if (existsSync(manifestFile)) {
    const manifest = JSON.parse(readFileSync(manifestFile, "utf8")) as Record<
      string,
      ManifestChunk
    >;
    const chunks = new Map<string, string[]>();
    for (const key of Object.keys(manifest)) {
      const sheets = chunkSheets(manifest, key);
      if (sheets.length > 0) chunks.set(manifest[key].file, sheets);
    }
    for (const file of [...chunks.keys()].sort()) {
      lines.push(`${SECTION}chunk ${file}`, ...(chunks.get(file) ?? []));
    }
  }
  for (const sheet of files.filter((file) => file.endsWith(".css"))) {
    lines.push(
      `${SECTION}sheet ${sheet}`,
      ...canonicalCss(
        relabelScopeIds(readFileSync(join(outDir, sheet), "utf8")),
      ),
    );
  }
  return lines;
}

function sections(lines: string[]): Map<string, string[]> {
  const found = new Map<string, string[]>();
  let current: string[] | undefined;
  for (const line of lines) {
    if (line.startsWith(SECTION)) {
      current = [];
      found.set(line.slice(SECTION.length), current);
    } else if (current) {
      current.push(line);
    } else {
      throw new Error(
        `a snapshot starts with a section header, not with "${line}"`,
      );
    }
  }
  return found;
}

/**
 * The index of the first line two sections do not share: the length of the
 * shorter one when one is a prefix of the other, and of both when they are equal.
 */
function firstDifference(a: string[], b: string[]): number {
  const shared = Math.min(a.length, b.length);
  for (let line = 0; line < shared; line++) {
    if (a[line] !== b[line]) return line;
  }
  return shared;
}

/** Where two snapshots part, one entry per section that differs. */
export interface SnapshotDifference {
  section: string;
  change: "added" | "removed" | "changed";
  /** The index of the first differing line inside the section. */
  line: number;
  /** The lines both sides share just before that one. */
  before: string[];
  /** Each side from that line on. */
  base: string[];
  head: string[];
}

/**
 * Every section that differs between two snapshots, with the lines around the
 * first place it differs. Empty when the builds apply the same styles.
 */
export function compareSnapshots(
  base: string[],
  head: string[],
  context = 3,
): SnapshotDifference[] {
  const left = sections(base);
  const right = sections(head);
  const names = [...new Set([...left.keys(), ...right.keys()])].sort();
  const found: SnapshotDifference[] = [];
  for (const section of names) {
    const a = left.get(section);
    const b = right.get(section);
    const line = a && b ? firstDifference(a, b) : 0;
    if (a && b && line === a.length && line === b.length) continue;
    found.push({
      section,
      change: !a ? "added" : !b ? "removed" : "changed",
      line,
      before: (a ?? []).slice(Math.max(0, line - context), line),
      base: (a ?? []).slice(line, line + context + 1),
      head: (b ?? []).slice(line, line + context + 1),
    });
  }
  return found;
}

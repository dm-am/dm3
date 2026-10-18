/**
 * The styles of a client build, reduced to what the cascade reads: every
 * stylesheet, the styles and sheet links of each page, and the order in which
 * each chunk brings its sheets. Two builds that differ only in what is
 * normalized below give the browser the same rules in the same order.
 *
 * Three things are normalized and nothing else:
 * - whitespace and comments, which the production minifier drops anyway,
 *   and with them a block left with nothing inside: Sass keeps a rule whose
 *   only content is a loud comment, and the comment is all it carried;
 * - the semicolon after the last declaration of a block, which is optional;
 * - the scope ids of scoped styles. @vitejs/plugin-vue derives the id of a
 *   component from its path and, in a production build, from its whole
 *   source, so a comment edited in a component's script renames every
 *   selector of its style and every @keyframes it defines. Ids are renumbered
 *   by first appearance in each sheet: a sheet that applies the same rules to
 *   the same components in the same order reads the same.
 *
 * The order of declarations, of rules and of sheets is kept as built, because
 * the cascade depends on it: the later of two declarations wins, and a sheet
 * loaded later wins over an earlier one of the same weight. That is also why
 * the build this reads is made with the CSS minifier off: lightningcss sorts
 * declarations and merges rules, and after it a swap of two declarations
 * could not be seen.
 *
 * Only styles are read. The markup a component renders, and with it the
 * classes its elements carry, is in the scripts of the build, and so is the
 * exact list of sheets each dynamic import preloads: `compare-code.mjs
 * --build` compares those.
 *
 * scripts/visual/compare-css.mjs runs the gate on top of this file, and Node
 * loads it there with its types stripped: it may import only node: modules
 * and may use no syntax beyond type annotations.
 */
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { join, relative, sep } from "node:path";

/** The prefix of a line that opens a section of a snapshot. */
export const SECTION = "### ";

/**
 * Renumbers the scope ids of one sheet or chunk by first appearance.
 *
 * A scoped style carries its component's id in two forms: the attribute
 * data-v-<id> in its selectors (data-v-<id>-s under :slotted), and the suffix
 * -<id> Vue gives the name of each @keyframes it defines, wherever that name
 * is used. Both forms of one id get the same label, so a component whose id
 * changed reads the same everywhere and two components still read apart. The
 * labels (-~1, -~2, ...) are nothing a source can spell.
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
 * One stylesheet as lines: a rule opens with `selector{`, each declaration is
 * a line of its own ending in `;`, and `}` closes the rule. Whitespace runs
 * are collapsed to one space and removed where CSS never reads them: around
 * braces, semicolons and commas, inside parentheses and around a
 * declaration's colon. A space before a parenthesis and after a closing one
 * stays, because there it can be a combinator or a separator.
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
 * What a page puts into the cascade itself, in document order: each sheet it
 * links (with its media, if it names one) and each <style> it holds, read as
 * canonicalCss reads a sheet. The built page links the bundle's sheets after
 * the styles written into index.html, so the order between the two counts as
 * much as either. Comments and the text of scripts are passed over, so
 * neither a commented-out tag nor a string in a script is read as one.
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
 * The sheets loading a chunk brings, in the order Vite's preload puts them on
 * the page: depth first through the chunks it imports, each chunk's own sheets
 * after those of its imports, every chunk and every sheet once (addDeps in
 * Vite's import analysis). The list one particular import preloads also
 * leaves out the importing chunk; those lists are in the scripts, and
 * compare-code --build compares them.
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
 * - `### page <file>`: for every page, the sheets it links and the styles it
 *   holds, in document order;
 * - `### chunk <file>`: for every chunk that brings stylesheets, which ones,
 *   in the order loading it brings them (needs the build manifest; without
 *   one the sections are absent);
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

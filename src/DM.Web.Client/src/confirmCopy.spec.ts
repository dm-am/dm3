/**
 * @vitest-environment node
 */

/**
 * One wording for every confirmation on the site.
 *
 * Thirty-six dialogs used to promise thirty-six different things. Five of them
 * were deletions and no two agreed on what deletion meant; the file name stood
 * bare while the rubric and the tag stood in quotes; one sentence carried a
 * class name out of the code; the account and the two-factor dialogs ran to
 * three sentences each, and no reader finishes three sentences with a finger
 * already on the button.
 *
 * The wording is a rule now, and the rule is three things. The heading asks a
 * question with the verb the confirm button carries, so the reader who reads
 * nothing else still knows what pressing it does. The sentence under it is one
 * sentence and names the one consequence. And nothing in the window is red:
 * the danger is in the words, not in the ink, so no call site hands the shell
 * a `danger` flag any more.
 *
 * Read from the sources rather than from a mounted screen, because half of
 * these texts live in tables a test would have to walk a page to reach, and
 * because the thing that breaks is a text someone edits, not a behaviour.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative } from "path";
import { fileURLToPath } from "url";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));
const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

/**
 * Below this the walk found nothing and an empty run would pass for a clean
 * one. The call sites are counted, not listed: a list of thirty-six paths is
 * a second thing to keep in step with the first.
 */
const AT_LEAST = 30;

/** A stand-in for `${...}` inside a template literal: it carries no full stop. */
const HOLE = "X";

function collect(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) collect(full, out);
    } else if (full.endsWith(".vue")) {
      out.push(full);
    }
  }
  return out;
}

const asPath = (file: string): string =>
  relative(CLIENT_SRC, file).split("\\").join("/");

// --- reading literals out of a script ---------------------------------------

/**
 * The string and template literals in a slice of script, with every `${...}`
 * inside a template replaced by a hole. Written as a scan and not as a regex
 * because a quote inside the other kind of quote is ordinary here ("Аккаунт
 * удален" sits inside a single-quoted attribute) and a regex reads it as a
 * terminator.
 */
function literalsIn(text: string): string[] {
  const out: string[] = [];
  let i = 0;
  while (i < text.length) {
    const quote = text[i];
    if (quote !== '"' && quote !== "'" && quote !== "`") {
      i += 1;
      continue;
    }
    i += 1;
    let value = "";
    while (i < text.length && text[i] !== quote) {
      if (text[i] === "\\") {
        value += text[i + 1] ?? "";
        i += 2;
        continue;
      }
      if (quote === "`" && text[i] === "$" && text[i + 1] === "{") {
        let depth = 1;
        i += 2;
        while (i < text.length && depth > 0) {
          if (text[i] === "{") depth += 1;
          if (text[i] === "}") depth -= 1;
          i += 1;
        }
        value += HOLE;
        continue;
      }
      value += text[i];
      i += 1;
    }
    i += 1;
    out.push(value);
  }
  return out;
}

/**
 * The slice a value occupies: from where it starts to the comma or semicolon
 * that ends it at the top level. A property in a table ends at its comma, a
 * `const` ends at its semicolon, and the commas inside a `computed(...)` sit
 * a level down and do not end anything.
 */
function valueRegion(script: string, from: number): string {
  let depth = 0;
  let i = from;
  while (i < script.length) {
    const ch = script[i];
    if (ch === '"' || ch === "'" || ch === "`") {
      i += 1;
      while (i < script.length && script[i] !== ch) {
        i += script[i] === "\\" ? 2 : 1;
      }
      i += 1;
      continue;
    }
    if (ch === "(" || ch === "[" || ch === "{") depth += 1;
    if (ch === ")" || ch === "]" || ch === "}") {
      if (depth === 0) break;
      depth -= 1;
    }
    if ((ch === "," || ch === ";") && depth === 0) break;
    i += 1;
  }
  return script.slice(from, i);
}

const escapeName = (name: string) => name.replace(/[^\w$]/g, "\\$&");

/** Every literal the file binds to `name`, as a table property or a `const`. */
function literalsOf(source: string, name: string): string[] {
  const safe = escapeName(name);
  const anchors = [
    new RegExp(`(^|[\\s{,(])${safe}\\s*:`, "gm"),
    new RegExp(`\\bconst\\s+${safe}\\s*=`, "g"),
  ];
  const out: string[] = [];
  for (const anchor of anchors) {
    for (const hit of source.matchAll(anchor)) {
      const from = (hit.index ?? 0) + hit[0].length;
      out.push(...literalsIn(valueRegion(source, from)));
    }
  }
  return out;
}

// --- reading the call sites -------------------------------------------------

interface Prop {
  /** The literal when the attribute carries one, the expression otherwise. */
  raw: string;
  bound: boolean;
}

interface Site {
  path: string;
  tag: string;
  title?: Prop;
  message?: Prop;
}

/**
 * The attribute as written, whichever quote holds it. The quote is captured
 * and matched back, so the other kind of quote inside the value is text.
 */
function attribute(tag: string, name: string): Prop | undefined {
  const hit = tag.match(
    new RegExp(`\\s(:|v-bind:)?${escapeName(name)}=(["'])([\\s\\S]*?)\\2`),
  );
  return hit ? { raw: hit[3], bound: Boolean(hit[1]) } : undefined;
}

function sites(): Site[] {
  const found: Site[] = [];
  for (const file of collect(CLIENT_SRC)) {
    const source = readFileSync(file, "utf8");
    for (const hit of source.matchAll(/<ConfirmDialog\b[\s\S]*?\/>/g)) {
      const tag = hit[0];
      found.push({
        path: asPath(file),
        tag,
        title: attribute(tag, "title"),
        message: attribute(tag, "message"),
      });
    }
  }
  return found;
}

/**
 * What a prop can put on the screen. A literal attribute is itself; a bound one
 * is read back out of the file it is bound in, by the name at the tail of the
 * expression (`pendingMod?.title`, `confirmTitle`).
 */
function textsOf(site: Site, prop: Prop): string[] {
  if (!prop.bound) return [decode(prop.raw)];
  // The `?? ''` tail of an optional chain is a fallback, not a text: read as a
  // literal it would answer for the whole prop and this gate would see an
  // empty heading everywhere it is written.
  const expr = prop.raw.replace(/\?\?\s*(['"`])\1\s*$/, "").trim();
  if (/^['"`]/.test(expr)) return literalsIn(expr).map(decode);
  const tail = expr.split(/[.?]/).filter(Boolean).pop();
  if (!tail) return [];
  const source = readFileSync(join(CLIENT_SRC, site.path), "utf8");
  return literalsOf(source, tail.trim()).map(decode);
}

const decode = (text: string) => text.replace(/&quot;/g, '"').trim();

/**
 * One sentence: nothing closes inside the line. Counted by what is NOT there
 * rather than by a period at the end, because the period at the end is a
 * separate rule with a zone of its own (sentencePeriods.spec.ts takes it off
 * a single sentence in a list or in anything that pops up, this window among
 * them), and a check that demanded one here would stand against that one.
 */
function oneSentence(text: string): boolean {
  return ![...text.matchAll(/[.!?]\s/g)].length;
}

const all = sites();

describe("the copy of a confirmation", () => {
  it("is read off every call site there is", () => {
    expect(all.length).toBeGreaterThanOrEqual(AT_LEAST);

    const unread = all
      .filter((site) => !site.title || !textsOf(site, site.title).some(Boolean))
      .map(
        (site) =>
          `${site.path}: the heading could not be read back, so this gate would pass it without looking`,
      );
    expect(unread).toEqual([]);
  });

  it("asks a question in the heading", () => {
    const offenders: string[] = [];
    for (const site of all) {
      if (!site.title) continue;
      for (const text of textsOf(site, site.title)) {
        if (!text || text.endsWith("?")) continue;
        offenders.push(
          `${site.path}: heading "${text}" is not a question, and the reader cannot tell what the button does`,
        );
      }
    }
    expect(offenders).toEqual([]);
  });

  it("names one consequence in one sentence", () => {
    const offenders: string[] = [];
    for (const site of all) {
      if (!site.message) continue;
      for (const text of textsOf(site, site.message)) {
        if (!text || oneSentence(text)) continue;
        offenders.push(
          `${site.path}: "${text}" is not one sentence, and nobody reads the third one with a finger on the button`,
        );
      }
    }
    expect(offenders).toEqual([]);
  });

  it("carries no red into the window", () => {
    const offenders = all
      .filter((site) => /[\s:](?:v-bind:)?danger[=\s/]/.test(site.tag))
      .map(
        (site) =>
          `${site.path}: the confirmation is handed a danger flag, and the danger belongs to the words, not to the fill`,
      );
    expect(offenders).toEqual([]);
  });
});

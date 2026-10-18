/**
 * The client's half of the generated markup checks.
 *
 * Three things live here, for bbcodeProperties.spec.ts and visualMode.spec.ts:
 *
 * - arbitraries for fast-check, built from the alphabet both sides share
 *   (markupAlphabet.ts): a soup of pieces of every kind, and markup the grammar
 *   accepts whole on a surface;
 * - the corpus the server wrote (markupCorpus.golden.json, by
 *   BbMarkupCorpusShould): generated texts with what the server shows their
 *   author, what it gives the author's editor back and whether it takes them on
 *   save;
 * - a reading of rendered markup into what the eye gets: runs of text, each
 *   with the set of things it is drawn with (bold, a hidden block and its
 *   addressees, a link and its address). The server's page and the editor's
 *   preview are written in two dialects of HTML, and this reading is where they
 *   meet: two renderings that read the same show the same.
 *
 * Spec-only: it reads a file off disk. How the checks work and how a failure
 * is replayed is written in docs/guides/TESTING.md, section "Генеративные
 * проверки разметки".
 */
import fc from "fast-check";
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  bbcodeToHtml,
  htmlToBbcode,
  type BBCodeContext,
  type BBCodeIssue,
} from "@/shared/lib/utils/bbcode";
import { MARKUP_ALPHABET } from "./markupAlphabet";

// ============================================================================
// SURFACES
// ============================================================================

export type MarkupSurface =
  | "GamePost"
  | "Comment"
  | "GlobalChatMessage"
  | "Profile"
  | "DirectMessage";

/** The editor context each surface of the server is written in. */
export const CONTEXT_OF: Record<MarkupSurface, BBCodeContext> = {
  GamePost: "post",
  Comment: "common",
  GlobalChatMessage: "message",
  Profile: "info",
  DirectMessage: "direct",
};

// ============================================================================
// THE CORPUS
// ============================================================================

/** One text of the corpus and the server's answers about it. */
export interface CorpusEntry {
  surface: MarkupSurface;
  /** Generated as markup the grammar accepts whole. */
  valid: boolean;
  input: string;
  /** What its author reads on the page; null for a text too deep to render. */
  display: string | null;
  /** What the author's editor is given back (the author_edit audience). */
  authorEdit: string;
  /** Why the save path of the surface refuses the text, or null. */
  refusal: string | null;
}

/** Which tags the page draws with one address between their halves. */
export interface AddressVerdict {
  address: string;
  link: boolean;
  img: boolean;
  mention: boolean;
}

/** The corpus as the server last wrote it. */
export function readCorpus(): CorpusEntry[] {
  return readCorpusFile().entries;
}

/** What the server answers about every address of the alphabet. */
export function readAddressVerdicts(): AddressVerdict[] {
  return readCorpusFile().addresses;
}

function readCorpusFile(): {
  entries: CorpusEntry[];
  addresses: AddressVerdict[];
} {
  // DM_MARKUP_CORPUS names another corpus, one the server wrote to search
  // beyond this one (docs/guides/TESTING.md); "write" is the server's own word
  // for writing this one, and means nothing here.
  const other = process.env.DM_MARKUP_CORPUS;
  const file =
    other && other !== "write"
      ? other
      : join(
          dirname(fileURLToPath(import.meta.url)),
          "markupCorpus.golden.json",
        );
  return JSON.parse(readFileSync(file, "utf8")) as {
    entries: CorpusEntry[];
    addresses: AddressVerdict[];
  };
}

// ============================================================================
// WHAT BOTH SIDES READ
// ============================================================================

/**
 * The text without what both sides take out before reading it, a surrogate
 * with no partner as U+FFFD: written out from BBCODE_RENDERING.md rather than
 * taken from the converter, so a range the converter got wrong is a failure.
 */
export function asBothSidesRead(text: string): string {
  let out = "";
  for (let i = 0; i < text.length; i++) {
    const code = text.charCodeAt(i);
    const removed =
      (code <= 0x1f && code !== 0x09 && code !== 0x0a && code !== 0x0d) ||
      (code >= 0x7f && code <= 0x9f) ||
      (code >= 0x202a && code <= 0x202e) ||
      (code >= 0x2066 && code <= 0x2069);
    if (removed) continue;
    const high = code >= 0xd800 && code <= 0xdbff;
    const next = text.charCodeAt(i + 1);
    if (high && next >= 0xdc00 && next <= 0xdfff) {
      out += text[i] + text[i + 1];
      i++;
      continue;
    }
    out += code >= 0xd800 && code <= 0xdfff ? "\uFFFD" : text[i];
  }
  return out;
}

/**
 * Whether the save path refuses a text for this finding of the markup check:
 * every finding about a hidden block does, less a closing tag with a value,
 * which is text and closes nothing, so the block around it is refused only if
 * nothing else closes it (and then the finding "not closed" names it). A text
 * nested too deep is refused as well, by the limit of nesting (IBbCodeNestingLimit).
 * BBCODE_RENDERING.md, section "Предупреждения редактора", has the words each
 * of these is read by.
 */
export function refusesSave(issue: BBCodeIssue): boolean {
  switch (issue.reason) {
    case "private-nested":
    case "private-outside-game":
    case "too-deep":
      return true;
    case "private-spelling":
      return !issue.text.includes("не закрывает скрытый текст");
    case "stray-closing":
      return issue.text.startsWith("[/private]");
    default:
      return false;
  }
}

/**
 * The text as the editor writes it itself, with its line breaks as LF: every
 * tag that is markup in the editor's own spelling (names in small letters,
 * parameters in its order, an addressee list of nobody left out), everything
 * else as typed. What the server gives the author's editor back is the typed
 * text up to exactly this: the markup it is given carries no spelling
 * (BBCODE_RENDERING.md, "Регистр имен"), and a browser holds no CR.
 */
export function canonical(text: string, context: BBCodeContext): string {
  const template = document.createElement("template");
  template.innerHTML = bbcodeToHtml(text.replace(/\r\n?/g, "\n"), {
    context,
  });
  const spelled = template.content.querySelectorAll(
    "[data-bb-open], [data-bb-close], [data-bb-source], [data-bb-character]",
  );
  for (const element of Array.from(spelled)) {
    // A hidden block spelled wrong keeps its spelling: the editor gives it
    // back as typed, and the save path refuses it by that spelling.
    const open = element.getAttribute("data-bb-open");
    const hidden = element.getAttribute("data-bb-tag") === "private";
    if (open !== null && (!hidden || /^\[private(=".*")?\]$/i.test(open))) {
      element.removeAttribute("data-bb-open");
    }
    element.removeAttribute("data-bb-close");
    element.removeAttribute("data-bb-source");
    if (element.getAttribute("data-bb-character") === "") {
      element.removeAttribute("data-bb-character");
    }
  }
  return htmlToBbcode(template.innerHTML);
}

// ============================================================================
// WHAT THE EYE GETS
// ============================================================================

/**
 * A run of text and what it is drawn with, the set written as its sorted
 * members joined by "|". Space is left out of the text (a line break and a
 * space draw differently, and a paragraph of the preview is a line break of
 * the page), and so are the characters no surface draws; the dashes are
 * written as the hyphens they stand for, since the page draws "--" as a dash
 * and the preview keeps it as typed.
 */
export interface Run {
  text: string;
  marks: string;
}

/** The leads, whom the recipients of every hidden block name first (PrivateTag). */
const LEADS = ["DungeonMaster", "Assistant"];

/**
 * The recipients a hidden block carries for its tooltip (data-recipients): the
 * leads, then the names of the block, if it has any.
 */
const RECIPIENTS = /^DungeonMaster, Assistant(?:, (.*))?$/;

/** The caption of a spoiler written without one. */
const DEFAULT_CAPTION = "Показать содержимое";

class Runs {
  readonly out: Run[] = [];

  text(value: string, marks: readonly string[]): void {
    const text = value
      .replace(/\u2014/g, "---")
      .replace(/\u2013/g, "--")
      .replace(/\s/g, "");
    this.push(text, marks);
  }

  /** Something drawn whole: an image, a link, a mention, a caption. */
  atom(text: string, marks: readonly string[]): void {
    this.push(text, marks);
  }

  private push(text: string, marks: readonly string[]): void {
    if (!text) return;
    const key = [...new Set(marks)].sort().join("|");
    const last = this.out[this.out.length - 1];
    if (last && last.marks === key) last.text += text;
    else this.out.push({ text, marks: key });
  }
}

/**
 * The names of an addressee list, as a mark: trimmed, the empty ones left out,
 * and so are the leads. The page names them first on every block and does not
 * name them twice, so whether the author wrote one of them is not on the page.
 */
function addressees(list: string | null): string {
  const names = (list ?? "")
    .split(",")
    .map((name) => name.trim())
    .filter((name) => name && !isLead(name));
  return `private:${names.join(",")}`;
}

/** A name is a lead in any case, and only A-Z folds onto a-z (PrivateTag). */
function isLead(name: string): boolean {
  const fold = (value: string) =>
    value.replace(/[A-Z]/g, (letter) => letter.toLowerCase());
  return LEADS.some((lead) => fold(lead) === fold(name));
}

function caption(text: string | null): string | null {
  return text && text !== DEFAULT_CAPTION ? text : null;
}

function image(img: Element, frame: Element | null): string {
  return `img:${img.getAttribute("src")}|${frame?.getAttribute("data-bb-width") ?? ""}|${frame?.getAttribute("data-bb-height") ?? ""}`;
}

function nodesOf(html: string): ChildNode[] {
  const template = document.createElement("template");
  template.innerHTML = html;
  return Array.from(template.content.childNodes);
}

function unknown(element: Element, where: string): never {
  throw new Error(
    `${where} drew an element this reading does not know: ${element.outerHTML.slice(0, 300)}`,
  );
}

/**
 * What a reader gets from the server's page (the display audience): its
 * elements and classes as BbParserProvider writes them.
 */
export function onThePage(html: string): Run[] {
  const runs = new Runs();
  const walk = (nodes: ChildNode[], marks: string[], list: string): void => {
    for (let i = 0; i < nodes.length; i++) {
      const node = nodes[i];
      if (node.nodeType === Node.TEXT_NODE) {
        runs.text(node.nodeValue ?? "", marks);
        continue;
      }
      if (node.nodeType !== Node.ELEMENT_NODE) continue;
      const element = node as Element;
      const classes = element.classList;
      const inside = (mark: string | null, nextList = list) =>
        walk(
          Array.from(element.childNodes),
          mark ? [...marks, mark] : marks,
          nextList,
        );
      const next = nodes[i + 1] as Element | undefined;

      switch (element.tagName) {
        case "BR":
          break;
        case "STRONG":
          inside("b");
          break;
        case "EM":
          inside("i");
          break;
        case "U":
          inside("u");
          break;
        case "S":
          inside("strike");
          break;
        case "CODE":
          if (!classes.contains("code")) unknown(element, "the page");
          inside("code");
          break;
        case "UL":
        case "OL":
          inside(null, element.tagName.toLowerCase());
          break;
        case "LI":
          inside(`li:${list}`);
          break;
        case "DIV":
          if (classes.contains("quote")) inside("quote");
          else if (classes.contains("mod-block")) inside("mod");
          // An item written inside an item with no list between them: a
          // block, so that the browser does not close the outer item on it.
          else if (classes.contains("bb-li")) inside(`li:${list}`);
          else unknown(element, "the page");
          break;
        case "A":
          if (classes.contains("spoiler-head")) {
            const body = next;
            if (!body?.classList?.contains("spoiler"))
              unknown(element, "the page");
            if (element.hasAttribute("data-bb-gate")) {
              // The spoiler the global chat puts an image behind: the reader's
              // layout opens it, and the preview has none.
              walk(Array.from(body.childNodes), marks, list);
            } else {
              const shown = caption(element.textContent);
              if (shown !== null) runs.atom(shown, [...marks, "caption"]);
              walk(Array.from(body.childNodes), [...marks, "spoiler"], list);
            }
            i++;
          } else if (classes.contains("nsfw-head")) {
            const body = next;
            if (!body?.classList?.contains("nsfw-spoiler"))
              unknown(element, "the page");
            walk(Array.from(body.childNodes), [...marks, "nsfw"], list);
            i++;
          } else if (classes.contains("bb-mention")) {
            runs.atom(element.textContent ?? "", [
              ...marks,
              `mention:${element.getAttribute("data-bb-user")}`,
            ]);
          } else {
            runs.atom(element.textContent ?? "", [
              ...marks,
              `link:${element.getAttribute("href")}`,
            ]);
          }
          break;
        case "IMG":
          runs.atom("<img>", [...marks, image(element, null)]);
          break;
        case "SPAN":
          if (classes.contains("private-message")) {
            // The hidden text, carrying who reads it for the tooltip:
            // data-recipients="DungeonMaster, Assistant, Вася, Петя".
            const recipients = RECIPIENTS.exec(
              element.getAttribute("data-recipients") ?? "",
            );
            if (!recipients) {
              unknown(element, "the page (a block without its recipients)");
            }
            walk(
              Array.from(element.childNodes),
              [...marks, addressees(recipients[1] ?? "")],
              list,
            );
            break;
          }
          if (!classes.contains("bb-image-frame")) unknown(element, "the page");
          runs.atom("<img>", [
            ...marks,
            image(element.querySelector("img") as Element, element),
          ]);
          break;
        default:
          unknown(element, "the page");
      }
    }
  };
  walk(nodesOf(html), [], "none");
  return runs.out;
}

/**
 * What the author gets from the editor's preview (bbcodeToHtml): its elements
 * read by their data-bb-tag.
 *
 * @param lines Keep the lines as well, for comparing one preview with another:
 *   a paragraph ends with "\u00b6", a line break is "\u23ce", and a block written
 *   against its neighbour without a break says so with "\u2282" and "\u2283" (glued
 *   before and after) and "\u21b5" (a break after it at the end of the text). The
 *   page draws lines by other means, so a preview is never compared with the
 *   page this way.
 */
export function inThePreview(html: string, lines = false): Run[] {
  const runs = new Runs();
  const glue = (
    element: Element,
    marks: string[],
    edge: "before" | "after",
  ) => {
    if (!lines) return;
    if (edge === "before" && element.getAttribute("data-bb-glue-before")) {
      runs.atom("\u2282", marks);
    }
    if (edge === "after" && element.getAttribute("data-bb-glue-after")) {
      runs.atom("\u2283", marks);
    }
    if (edge === "after" && element.getAttribute("data-bb-newline-after")) {
      runs.atom("\u21b5", marks);
    }
  };
  const walk = (nodes: ChildNode[], marks: string[], list: string): void => {
    for (const node of nodes) {
      if (node.nodeType === Node.TEXT_NODE) {
        runs.text(node.nodeValue ?? "", marks);
        continue;
      }
      if (node.nodeType !== Node.ELEMENT_NODE) continue;
      const element = node as Element;
      const inside = (mark: string | null, nextList = list) =>
        walk(
          Array.from(element.childNodes),
          mark ? [...marks, mark] : marks,
          nextList,
        );
      const tag = element.getAttribute("data-bb-tag");
      glue(element, marks, "before");

      switch (tag) {
        case "b":
        case "i":
        case "u":
        case "strike":
        case "code":
        case "quote":
        case "mod":
        case "nsfw":
          inside(tag);
          break;
        case "spoiler": {
          const shown = caption(element.getAttribute("data-bb-text"));
          if (shown !== null) runs.atom(shown, [...marks, "caption"]);
          inside("spoiler");
          break;
        }
        case "ul":
        case "ol":
          inside(null, tag);
          break;
        case "li":
          inside(`li:${list}`);
          break;
        case "noparse":
          inside(null);
          break;
        case "tab":
          break;
        case "link":
          runs.atom(element.textContent ?? "", [
            ...marks,
            `link:${element.getAttribute("href")}`,
          ]);
          break;
        case "mention":
          runs.atom(element.textContent ?? "", [
            ...marks,
            `mention:${element.getAttribute("data-bb-user")}`,
          ]);
          break;
        case "img":
          runs.atom("<img>", [
            ...marks,
            image(
              element,
              element.parentElement?.classList.contains("bb-image-frame")
                ? element.parentElement
                : null,
            ),
          ]);
          break;
        case "private":
          inside(addressees(element.getAttribute("data-bb-character")));
          break;
        case null:
          if (element.tagName === "P") {
            inside(null);
            if (lines) runs.atom("\u00b6", marks);
          } else if (element.tagName === "BR") {
            if (lines) runs.atom("\u23ce", marks);
          } else if (element.classList.contains("bb-image-frame")) inside(null);
          else unknown(element, "the preview");
          break;
        default:
          unknown(element, "the preview");
      }
      glue(element, marks, "after");
    }
  };
  walk(nodesOf(html), [], "none");
  return runs.out;
}

// ============================================================================
// ARBITRARIES
// ============================================================================

const ALPHABET = MARKUP_ALPHABET;

/** The piece with its placeholders filled: a list of names, a surrogate with no partner. */
function expand(piece: string): fc.Arbitrary<string> {
  const filled = piece
    .replace("{high-surrogate}", "\uD800")
    .replace("{low-surrogate}", "\uDC00");
  return filled.includes("{names}")
    ? fc
        .constantFrom(...ALPHABET.names)
        .map((names) => filled.replace("{names}", names))
    : fc.constant(filled);
}

/** One of the pieces, its placeholders filled. */
function oneOf(pieces: readonly string[]): fc.Arbitrary<string> {
  return fc.oneof(...pieces.map(expand));
}

/** A word: Q, four digits, Q. */
export const markupWord: fc.Arbitrary<string> = fc
  .integer({ min: 1, max: 9999 })
  .map((n) => `Q${String(n).padStart(4, "0")}Q`);

const MAX_DEPTH = 4;

/**
 * A soup of pieces of every kind: tags in pairs around what they hold, now and
 * then left open, among the pieces of the alphabet, broken ones included.
 *
 * @param hiding false to leave out every piece that spells [private], for a
 *   surface where the save path refuses any of it.
 */
export function markupSoup(hiding = true): fc.Arbitrary<string> {
  const allowed = (text: string) =>
    hiding || !asBothSidesRead(text).toLowerCase().includes("private");
  const piece = oneOf(ALPHABET.pieces.filter(allowed));
  const repeat = fc
    .constantFrom(...ALPHABET.repeats.filter(([text]) => allowed(text)))
    .map(([text, times]) => text.repeat(times));
  const pairs = ALPHABET.pairs.filter(([open, close]) => allowed(open + close));
  const leaf = fc
    .tuple(
      fc.constantFrom(...ALPHABET.leaves),
      fc.constantFrom(...ALPHABET.addresses),
    )
    .map(([[open, close], address]) => open + address + close);

  const level = (depth: number): fc.Arbitrary<string> => {
    const items: Array<fc.WeightedArbitrary<string>> = [
      { arbitrary: markupWord, weight: 20 },
      { arbitrary: piece, weight: 40 },
      { arbitrary: repeat, weight: 2 },
      { arbitrary: leaf, weight: 5 },
    ];
    if (depth < MAX_DEPTH) {
      const inner = level(depth + 1);
      items.push({
        weight: 30,
        arbitrary: fc
          .tuple(
            fc.oneof(
              ...pairs.map(([open, close]) =>
                expand(open).map((filled) => [filled, close] as const),
              ),
            ),
            inner,
            fc.integer({ min: 0, max: 7 }),
          )
          .map(
            ([[open, close], held, left]) =>
              open + held + (left === 0 ? "" : close),
          ),
      });
    }
    return fc
      .array(fc.oneof(...items), { minLength: 1, maxLength: 5 })
      .map((parts) => parts.join(""));
  };
  return level(0);
}

/**
 * Markup the grammar accepts whole in the context: every pair closed, lists
 * that hold items, [mod] only where the context has it, [private] only in a
 * game post and one level deep, [noparse] around pieces of any kind less its
 * own closing tag.
 */
export function markupValid(context: BBCodeContext): fc.Arbitrary<string> {
  const valid = ALPHABET.valid;
  const hasMod = context === "common" || context === "message";
  const hasPrivate = context === "post";
  const exampleable = ALPHABET.pieces.filter(
    (piece) => !asBothSidesRead(piece).toLowerCase().includes("[/noparse"),
  );
  const example = fc
    .array(fc.oneof(markupWord, oneOf(exampleable)), {
      minLength: 1,
      maxLength: 4,
    })
    .map((parts) => `[noparse]${parts.join("")}[/noparse]`);

  const memo = new Map<string, fc.Arbitrary<string>>();
  const level = (depth: number, inPrivate: boolean): fc.Arbitrary<string> => {
    const key = `${depth}:${inPrivate}`;
    const known = memo.get(key);
    if (known) return known;

    const items: Array<fc.WeightedArbitrary<string>> = [
      { arbitrary: markupWord, weight: 24 },
      { arbitrary: fc.constantFrom(...valid.text), weight: 28 },
      { arbitrary: fc.constantFrom(...valid.leaves), weight: 8 },
      { arbitrary: example, weight: 4 },
    ];
    if (depth < MAX_DEPTH) {
      const inner = level(depth + 1, inPrivate);
      const around = (pairs: Array<[string, string]>, weight: number) =>
        items.push({
          weight,
          arbitrary: fc
            .tuple(fc.constantFrom(...pairs), inner)
            .map(([[open, close], held]) => open + held + close),
        });
      around(valid.pairs, 22);
      const item = fc
        .tuple(fc.constantFrom(...valid.items), inner, fc.boolean())
        .map(
          ([[open, close], held, apart]) =>
            (apart ? "\n" : "") + open + held + close,
        );
      items.push({
        weight: 5,
        arbitrary: fc
          .tuple(
            fc.constantFrom(...valid.lists),
            fc.array(item, { minLength: 1, maxLength: 3 }),
            fc.boolean(),
          )
          .map(
            ([[open, close], held, apart]) =>
              open + held.join("") + (apart ? "\n" : "") + close,
          ),
      });
      if (hasMod) around(valid.mod, 3);
      if (hasPrivate && !inPrivate) {
        items.push({
          weight: 6,
          arbitrary: fc
            .tuple(
              oneOf(valid.private),
              level(depth + 1, true),
              fc.constantFrom(...valid.privateClosings),
            )
            .map(([open, held, close]) => open + held + close),
        });
      }
    }
    const made = fc
      .array(fc.oneof(...items), { minLength: 1, maxLength: 4 })
      .map((parts) => parts.join(""));
    memo.set(key, made);
    return made;
  };
  return level(0, false);
}

// ============================================================================
// RUNNING A CHECK
// ============================================================================

/** A seed from a name, the same on every run: FNV-1a over its code units. */
function seedOf(name: string): number {
  let hash = 0x811c9dc5;
  for (let i = 0; i < name.length; i++) {
    hash ^= name.charCodeAt(i);
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }
  return hash | 0;
}

/**
 * The parameters of one check: a seed of its own, so every run tries the same
 * texts, and the run a report asks for when DM_MARKUP_SEED (with
 * DM_MARKUP_PATH) is set. DM_MARKUP_RUNS sets how many texts a check tries.
 */
export function markupRuns(name: string, runs: number): fc.Parameters<unknown> {
  const env = process.env;
  const seed = env.DM_MARKUP_SEED ? Number(env.DM_MARKUP_SEED) : seedOf(name);
  return {
    seed,
    numRuns: env.DM_MARKUP_RUNS ? Number(env.DM_MARKUP_RUNS) : runs,
    ...(env.DM_MARKUP_PATH ? { path: env.DM_MARKUP_PATH } : {}),
  };
}

/** A string as a literal that reads back as the same string, for a report. */
export function literal(text: string): string {
  return JSON.stringify(text).replace(
    /[^\x20-\x7eА-я]/g,
    (ch) => `\\u${ch.charCodeAt(0).toString(16).padStart(4, "0")}`,
  );
}

// ============================================================================
// WHAT THE BROWSER BUILDS
// ============================================================================

/** The elements that close themselves. */
const VOID = new Set(["br", "img", "hr", "input", "wbr"]);

/** A tag of the markup, its attributes in double quotes, which may hold a ">". */
const TAG =
  /<(\/?)([a-zA-Z][a-zA-Z0-9]*)((?:\s+[^\s=>/]+(?:="[^"]*")?)*)\s*(\/?)>/g;

/**
 * The elements of a markup string as its writer nested them, "depth tag" in
 * document order, read off the tags themselves; and, for each opening, the
 * tags open around it.
 */
function written(html: string): Array<{ node: string; around: string[] }> {
  const out: Array<{ node: string; around: string[] }> = [];
  const open: string[] = [];
  for (const [, closing, name, , selfClosing] of html.matchAll(TAG)) {
    const tag = name.toLowerCase();
    if (closing) {
      if (open.pop() !== tag) return [{ node: "unbalanced", around: [] }];
      continue;
    }
    out.push({ node: `${open.length} ${tag}`, around: [...open] });
    if (!selfClosing && !VOID.has(tag)) open.push(tag);
  }
  return out;
}

/** The elements the browser builds of the same string, the same way. */
function built(html: string): string[] {
  const out: string[] = [];
  const walk = (nodes: ChildNode[], depth: number) => {
    for (const node of nodes) {
      if (node.nodeType !== Node.ELEMENT_NODE) continue;
      out.push(`${depth} ${(node as Element).tagName.toLowerCase()}`);
      walk(Array.from(node.childNodes), depth + 1);
    }
  };
  walk(nodesOf(html), 0);
  return out;
}

/**
 * Whether the browser builds another tree from the markup than its writer
 * nested: an element it closes early or moves, and a text that lands outside
 * the element it was written in.
 */
export function rebuiltByTheBrowser(html: string): boolean {
  const tree = written(html).map((element) => element.node);
  return tree.join("/") !== built(html).join("/");
}

/**
 * Whether the markup opens an <li> while another is open with no list between
 * them. The browser closes the open item there, and everything it held after
 * that point, a spoiler or an 18+ block around the new item included, with it.
 */
export function opensAnItemInsideAnItem(html: string): boolean {
  return written(html).some(({ node, around }) => {
    if (!node.endsWith(" li")) return false;
    for (let i = around.length - 1; i >= 0; i--) {
      if (around[i] === "ul" || around[i] === "ol") return false;
      if (around[i] === "li") return true;
    }
    return false;
  });
}

/**
 * Whether the text is white space alone, as the server reads it (the White_Space
 * property; U+FEFF is a letter to it).
 */
export function onlyWhiteSpace(text: string): boolean {
  return /^[\t-\r \x85\xa0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000]*$/.test(
    asBothSidesRead(text),
  );
}

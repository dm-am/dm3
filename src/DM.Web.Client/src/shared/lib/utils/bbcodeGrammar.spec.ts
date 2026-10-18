/**
 * @vitest-environment jsdom
 */

/**
 * The client's half of the grammar table (bbcodeGrammar.cases.ts).
 *
 * Every row is checked in the editor context of its surface, and a row for
 * "any" surface in all five. Three things are held to it:
 *
 * - the preview draws tags, broken spellings and hidden blocks as the row
 *   says. Typography is the reader's page and not the author's draft, so the
 *   preview keeps it as typed, and an image of the global chat has no spoiler
 *   in it. A hidden block nobody reads (an empty seenBy: hiding markup on a
 *   surface that has none) is the author's own text in the editor, so the
 *   preview shows the whole input of such a row as typed, or, where the block
 *   stands inside a tag, the text of its typed in its place. [code] holds its
 *   children drawn as anywhere else. Line breaks are left out of the
 *   comparison: the round trip below holds them exactly;
 * - bbcodeToHtml followed by htmlToBbcode gives the input back unchanged, less
 *   the characters that reorder text and the control characters, which both
 *   sides take out before reading;
 * - validateBBCode, in the same context, names the reason of a broken row
 *   and nothing on the others.
 *
 * One mapping differs from the header of the table: DirectMessage is read in
 * the "direct" context and not in "message". The row a-mod-direct has [mod]
 * stay text in private messages, and "message" is the context of the global
 * chat, where [mod] is a tag.
 *
 * The hint, the description of a game tag, has no visual mode: its preview is
 * the reader's markup, hintToHtml, and a row for it is drawn by that. Its tag
 * is text in the editor's HTML, so the round trip gives it back as typed.
 */

import { describe, expect, it } from "vitest";
import {
  bbcodeToHtml,
  hintToHtml,
  htmlToBbcode,
  validateBBCode,
  type BBCodeContext,
  type BBCodeIssueReason,
} from "./bbcode";
import {
  BBCODE_GRAMMAR_CASES,
  type GrammarCase,
  type GrammarPiece,
  type GrammarSurface,
} from "./bbcodeGrammar.cases";

const CONTEXT_OF: Record<Exclude<GrammarSurface, "any">, BBCodeContext> = {
  GamePost: "post",
  Comment: "common",
  GlobalChatMessage: "message",
  DirectMessage: "direct",
  Profile: "info",
  Hint: "hint",
};

/** The five surfaces of content "any" stands for: the hint is named by its rows. */
const CONTENT_CONTEXTS: BBCodeContext[] = [
  "post",
  "common",
  "message",
  "direct",
  "info",
];

function contextsOf(row: GrammarCase): BBCodeContext[] {
  return row.surface === "any" ? CONTENT_CONTEXTS : [CONTEXT_OF[row.surface]];
}

/** A piece as the preview can show it. */
type Seen =
  | { text: string }
  | { tag: string; params?: Record<string, string>; children?: Seen[] }
  | { hidden: string[]; children: Seen[] };

/** Join neighbouring text, drop line breaks and empty text. */
function normalise(pieces: Seen[]): Seen[] {
  const out: Seen[] = [];
  for (const piece of pieces) {
    if ("text" in piece) {
      const text = piece.text.replace(/\n/g, "");
      if (!text) continue;
      const last = out[out.length - 1];
      if (last && "text" in last) last.text += text;
      else out.push({ text });
      continue;
    }
    if ("hidden" in piece) {
      out.push({ hidden: piece.hidden, children: normalise(piece.children) });
      continue;
    }
    const tag: Seen = { tag: piece.tag };
    if (piece.params && Object.keys(piece.params).length) {
      tag.params = piece.params;
    }
    if (piece.children) {
      const children = normalise(piece.children);
      if (children.length) tag.children = children;
    }
    out.push(tag);
  }
  return out;
}

/** Whether a hidden block anywhere in the pieces is read by nobody. */
function readByNobody(pieces: GrammarPiece[]): boolean {
  return pieces.some((piece) =>
    "hidden" in piece
      ? (piece.hidden.seenBy.length === 0 && piece.typed === undefined) ||
        readByNobody(piece.children)
      : "children" in piece && !!piece.children && readByNobody(piece.children),
  );
}

/** What the author's preview owes for a row. */
function expectedRowPreview(row: GrammarCase, context: BBCodeContext): Seen[] {
  if (readByNobody(row.expect)) return [{ text: row.input }];
  const seen = expectedPreview(row.expect);
  return context === "hint" ? withoutExampleElements(seen) : seen;
}

/**
 * The reader's markup draws an example as its text and nothing around it,
 * as the page does: in the hint's preview a [noparse] is no element.
 */
function withoutExampleElements(pieces: Seen[]): Seen[] {
  return pieces.flatMap((piece): Seen[] => {
    if ("tag" in piece && piece.tag === "noparse") return piece.children ?? [];
    if ("tag" in piece && piece.children) {
      return [{ ...piece, children: withoutExampleElements(piece.children) }];
    }
    return [piece];
  });
}

/** What the author's preview owes for the pieces of a row. */
function expectedPreview(pieces: GrammarPiece[]): Seen[] {
  return pieces.map((piece): Seen => {
    if ("text" in piece) return { text: piece.text };
    if ("dash" in piece) return { text: piece.dash === "short" ? "--" : "---" };
    if ("source" in piece) throw new Error("a source piece has no preview");
    if ("hidden" in piece) {
      // A block nobody reads, inside a tag: the preview has the text it was
      // typed as in its place.
      if (piece.hidden.seenBy.length === 0 && piece.typed !== undefined) {
        return { text: piece.typed };
      }
      return {
        hidden: piece.hidden.to,
        children: expectedPreview(piece.children),
      };
    }
    return {
      tag: piece.tag,
      params: piece.params,
      children: piece.children && expectedPreview(piece.children),
    };
  });
}

/** Read the preview back into pieces. */
function seen(nodes: NodeListOf<ChildNode> | ChildNode[]): Seen[] {
  const out: Seen[] = [];
  for (const node of Array.from(nodes)) {
    if (node.nodeType === Node.TEXT_NODE) {
      out.push({ text: node.nodeValue ?? "" });
      continue;
    }
    if (node.nodeType !== Node.ELEMENT_NODE) continue;
    const el = node as Element;
    const bb = el.getAttribute("data-bb-tag");

    if (el.tagName === "BR") {
      out.push({ text: "\n" });
    } else if (el.classList.contains("bb-image-frame")) {
      out.push(...seen(el.childNodes).map((piece) => withFrame(piece, el)));
    } else if (bb === "img") {
      const params: Record<string, string> = {
        src: el.getAttribute("src") ?? "",
      };
      if (el.getAttribute("alt")) params.alt = el.getAttribute("alt") as string;
      out.push({ tag: "img", params });
    } else if (bb === "link") {
      out.push({
        tag: "link",
        params: {
          href: el.getAttribute("href") ?? "",
          text: el.textContent ?? "",
        },
      });
    } else if (bb === "mention") {
      out.push({
        tag: "mention",
        params: { login: el.getAttribute("data-bb-user") ?? "" },
      });
    } else if (el.classList.contains("bb-tip")) {
      const params: Record<string, string> = {};
      const text = el.getAttribute("data-tip-text");
      const img = el.getAttribute("data-tip-img");
      if (text !== null) params.text = text;
      if (img !== null) params.img = img;
      out.push({ tag: "tip", params, children: seen(el.childNodes) });
    } else if (bb === "private") {
      const names = (el.getAttribute("data-bb-character") ?? "")
        .split(",")
        .map((name) => name.trim())
        .filter(Boolean);
      out.push({ hidden: names, children: seen(el.childNodes) });
    } else if (bb === "noparse") {
      out.push({ tag: bb, children: [{ text: el.textContent ?? "" }] });
    } else if (bb === "tab") {
      out.push({ tag: "tab" });
    } else if (bb === "spoiler") {
      const caption = el.getAttribute("data-bb-text");
      out.push({
        tag: "spoiler",
        params: caption ? { text: caption } : undefined,
        children: seen(el.childNodes),
      });
    } else if (bb) {
      out.push({ tag: bb, children: seen(el.childNodes) });
    } else if (el.tagName === "P") {
      out.push({ text: "\n" }, ...seen(el.childNodes), { text: "\n" });
    } else {
      throw new Error(
        `the preview drew an element it has no reading for: ${el.outerHTML}`,
      );
    }
  }
  return out;
}

function withFrame(piece: Seen, frame: Element): Seen {
  if (!("tag" in piece) || piece.tag !== "img") return piece;
  const params = { ...piece.params };
  const width = frame.getAttribute("data-bb-width");
  const height = frame.getAttribute("data-bb-height");
  if (width) params.width = width;
  if (height) params.height = height;
  return { tag: "img", params };
}

function preview(input: string, context: BBCodeContext): Seen[] {
  const template = document.createElement("template");
  template.innerHTML =
    context === "hint" ? hintToHtml(input) : bbcodeToHtml(input, { context });
  return normalise(seen(template.content.childNodes));
}

/** Sort the parameters of every tag, so the order they are listed in is no difference. */
function sorted(pieces: Seen[]): unknown {
  return JSON.parse(
    JSON.stringify(pieces, (_key, value) =>
      value && typeof value === "object" && !Array.isArray(value)
        ? Object.fromEntries(
            Object.entries(value).sort(([a], [b]) => a.localeCompare(b)),
          )
        : value,
    ),
  );
}

// The reasons the table names are the reasons the check reports.
const REASONS: BBCodeIssueReason[] = BBCODE_GRAMMAR_CASES.flatMap((row) =>
  row.broken ? [row.broken] : [],
);

/**
 * The text less what both sides take out before reading it: the characters
 * that reorder text for display, U+202A..U+202E and U+2066..U+2069, and the C0
 * and C1 control characters other than tab, line feed and carriage return.
 * Written out here rather than taken from the converter, so a range the
 * converter got wrong is a failure.
 */
function withoutRemoved(text: string): string {
  return Array.from(text)
    .filter((ch) => {
      const code = ch.charCodeAt(0);
      const control =
        (code <= 0x1f && ch !== "\t" && ch !== "\n" && ch !== "\r") ||
        (code >= 0x7f && code <= 0x9f);
      const reordering =
        (code >= 0x202a && code <= 0x202e) ||
        (code >= 0x2066 && code <= 0x2069);
      return !control && !reordering;
    })
    .join("");
}

describe("the grammar table, on the client", () => {
  it("is read, and every row of it", () => {
    expect(BBCODE_GRAMMAR_CASES.length).toBeGreaterThanOrEqual(198);
    expect(new Set(BBCODE_GRAMMAR_CASES.map((row) => row.id)).size).toBe(
      BBCODE_GRAMMAR_CASES.length,
    );
    expect(REASONS.length).toBeGreaterThan(0);
  });

  const displayRows = BBCODE_GRAMMAR_CASES.filter(
    (row) => row.audience !== "author_edit",
  ).flatMap((row) =>
    contextsOf(row).map((context) => [row.id, context, row] as const),
  );

  it.each(displayRows)(
    "%s in %s: the preview draws it",
    (_id, context, row) => {
      expect(sorted(preview(row.input, context))).toEqual(
        sorted(normalise(expectedRowPreview(row, context))),
      );
    },
  );

  const allRows = BBCODE_GRAMMAR_CASES.flatMap((row) =>
    contextsOf(row).map((context) => [row.id, context, row] as const),
  );

  it.each(allRows)(
    "%s in %s: comes back from the preview unchanged",
    (_id, context, row) => {
      const source = row.expect.find((piece) => "source" in piece);
      const expected = source && "source" in source ? source.source : row.input;
      expect(htmlToBbcode(bbcodeToHtml(row.input, { context }))).toBe(
        withoutRemoved(expected),
      );
    },
  );

  it.each(allRows)(
    "%s in %s: the markup check names what is broken and nothing else",
    (_id, context, row) => {
      const reasons = validateBBCode(row.input, context).map(
        (issue) => issue.reason,
      );
      if (row.broken) expect(reasons).toContain(row.broken);
      else expect(reasons).toEqual([]);
    },
  );

  it.each(allRows.filter(([, , row]) => row.editor))(
    "%s in %s: what the editor writes itself passes the check",
    (_id, context, row) => {
      expect(validateBBCode(row.input, context)).toEqual([]);
    },
  );
});

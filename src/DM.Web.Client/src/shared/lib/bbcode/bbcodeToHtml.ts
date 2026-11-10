/**
 * The editor's preview of the author's draft: BBCode to the HTML the visual
 * mode edits; readers get the server's rendering (BBCODE_RENDERING.md). Read by
 * the server's grammar (bbcodeGrammar.cases.ts holds both sides), it differs
 * from a page by typography, by drawing hidden blocks for their author, by a
 * hidden block on a surface without them staying text, and by no spoiler on a
 * global chat image. Every input comes back as typed through htmlToBbcode;
 * only the reordering characters are taken out first, as the server does.
 */

import { DEFAULT_LINK_TEXT } from "./bbcodeConstants";
import {
  ALL_TAGS,
  BLOCK_TAGS,
  CONTEXT_TAGS,
  type BBCodeContext,
  type BbNode,
} from "./bbcodeTagSet";
import {
  asTheServerReads,
  trimWhiteSpace,
  writeImage,
  writeLink,
  writeMention,
  writeOpening,
  writePrivateOpening,
} from "./bbcodeSpelling";
import { readText } from "./bbcodeReader";

/** Escape text for the content of an element. */
export function escapeText(text: string): string {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;");
}

/** Escape text for the value of an attribute. */
function escapeHtml(text: string): string {
  return escapeText(text).replace(/"/g, "&quot;").replace(/'/g, "&#039;");
}

/** Escape text that may hold line breaks, which become <br>. */
function escapeLines(text: string): string {
  return escapeText(text).replace(/\r?\n/g, "<br>");
}

/** An attribute, or nothing when it has no value. */
function attr(name: string, value: string | null | false): string {
  if (value === null || value === false) return "";
  return ` ${name}="${escapeHtml(value)}"`;
}

/**
 * An image as the server renders it: a bare <img>, or a frame carrying a given
 * size in custom properties; alt empty, as the server's. data-bb-source keeps a
 * spelling the canonical one would change.
 */
function renderImage(node: Extract<BbNode, { t: "img" }>): string {
  const canonical = writeImage(node.src, node.width, node.height);
  const img =
    `<img src="${escapeHtml(node.src)}" alt=""` +
    `${attr("data-bb-source", node.source !== canonical && node.source)}` +
    ` class="bb-image" data-bb-tag="img" loading="lazy" decoding="async" referrerpolicy="no-referrer" />`;
  if (node.width === null && node.height === null) return img;

  const vars: string[] = [];
  let data = "";
  if (node.width !== null) {
    vars.push(`--bb-image-max-width:${node.width}px`);
    data += ` data-bb-width="${node.width}"`;
  }
  if (node.height !== null) {
    vars.push(`--bb-image-max-height:${node.height}px`);
    data += ` data-bb-height="${node.height}"`;
  }
  return `<span class="bb-image-frame"${data} style="${vars.join(";")}">${img}</span>`;
}

/**
 * A link typed without a caption shows the default one; the marker keeps that
 * caption from coming back as typed.
 */
function renderLink(node: Extract<BbNode, { t: "link" }>): string {
  const canonical = writeLink(node.href, node.text);
  const marker =
    node.text === null
      ? ' data-bb-selfref="true"'
      : attr("data-bb-text", node.text);
  return (
    `<a href="${escapeHtml(node.href)}" data-bb-tag="link"${marker}` +
    `${attr("data-bb-source", node.source !== canonical && node.source)}` +
    ` target="_blank" rel="noopener">${escapeText(node.text || DEFAULT_LINK_TEXT)}</a>`
  );
}

/** The tags that stand in a line, and the element each is drawn with. */
type InlineTag = "b" | "i" | "u" | "strike" | "code";

const INLINE_ELEMENTS: Record<InlineTag, string> = {
  b: "strong",
  i: "em",
  u: "u",
  strike: "s",
  code: "code",
};

const isInlineTag = (tag: string): tag is InlineTag =>
  Object.prototype.hasOwnProperty.call(INLINE_ELEMENTS, tag);

const BLOCK_ELEMENTS: Record<
  "spoiler" | "nsfw" | "quote" | "mod" | "ul" | "ol",
  [string, string]
> = {
  spoiler: ["div", "bb-spoiler"],
  nsfw: ["div", "bb-nsfw"],
  quote: ["blockquote", "bb-quote"],
  mod: ["div", "bb-mod"],
  ul: ["ul", ""],
  ol: ["ol", ""],
};

/** How a block stands against what is written next to it. */
interface Glue {
  before: boolean;
  after: boolean;
  newlineAfter: boolean;
}

const NO_GLUE: Glue = { before: false, after: false, newlineAfter: false };

function glueAttrs(glue: Glue): string {
  return (
    (glue.before ? ' data-bb-glue-before="true"' : "") +
    (glue.after ? ' data-bb-glue-after="true"' : "") +
    (glue.newlineAfter ? ' data-bb-newline-after="true"' : "")
  );
}

/**
 * A tag as typed, where the editor would write it otherwise: [B] for [b],
 * [/QUOTE] for [/quote]. The way back returns it (kept).
 */
function spellingAttrs(
  open: string,
  canonicalOpen: string,
  close: string | null,
  canonicalClose: string,
): string {
  return (
    attr("data-bb-open", open !== canonicalOpen && open) +
    attr("data-bb-close", close !== null && close !== canonicalClose && close)
  );
}

/** The spelling of a tag without parameters, as typed. */
function plainSpelling(node: {
  tag: string;
  open: string;
  close: string;
}): string {
  return spellingAttrs(
    node.open,
    `[${node.tag}]`,
    node.close,
    `[/${node.tag}]`,
  );
}

class HtmlWriter {
  private readonly blockish = new Map<BbNode, boolean>();

  /** Whether a node is drawn as a block: a block tag, or a tag holding one. */
  private isBlock(node: BbNode): boolean {
    if (node.t !== "tag" && node.t !== "private") return false;
    if (node.t === "tag" && BLOCK_TAGS.has(node.tag)) return true;
    const known = this.blockish.get(node);
    if (known !== undefined) return known;
    const result = node.children.some((child) => this.isBlock(child));
    this.blockish.set(node, result);
    return result;
  }

  /**
   * Splits inline runs between blocks into paragraphs at their line breaks; a
   * block with no break at its edge is marked glued, so the way back restores it.
   */
  container(nodes: BbNode[], root: boolean, itemOfAList = false): string {
    const runs: BbNode[][] = [[]];
    const blocks: BbNode[] = [];
    for (const node of nodes) {
      if (this.isBlock(node)) {
        blocks.push(node);
        runs.push([]);
      } else {
        runs[runs.length - 1].push(node);
      }
    }

    const glue: Glue[] = blocks.map(() => ({ ...NO_GLUE }));
    const paragraphs: BbNode[][][] = runs.map((run, i) => {
      let lines = splitLines(run);
      const before = i > 0 ? glue[i - 1] : null;
      const after = i < blocks.length ? glue[i] : null;
      const empty = run.length === 0;
      const lineBreakOnly =
        lines.length === 2 && lines[0].length === 0 && lines[1].length === 0;

      if (empty) {
        if (before && after) after.before = true;
        return [];
      }
      if (before && after && lineBreakOnly) return [];
      if (root && before && !after && lineBreakOnly) {
        before.newlineAfter = true;
        return [];
      }
      if (before) {
        if (lines.length >= 2 && lines[0].length === 0) lines = lines.slice(1);
        else before.after = true;
      }
      if (after) {
        if (lines.length >= 2 && lines[lines.length - 1].length === 0) {
          lines = lines.slice(0, -1);
        } else {
          after.before = true;
        }
      }
      return lines;
    });

    // The visual mode's list item starts with a paragraph, so an item starting
    // with a block gets an empty line before it, marked glued.
    let html = "";
    if (itemOfAList && blocks.length > 0 && runs[0].length === 0) {
      glue[0].before = true;
      html += "<p></p>";
    }
    paragraphs.forEach((lines, i) => {
      for (const line of lines) {
        html += `<p>${line.map((node) => this.inline(node)).join("")}</p>`;
      }
      if (i < blocks.length) html += this.block(blocks[i], glue[i], false);
    });
    return html;
  }

  /** What [ul] and [ol] hold: items, and whatever was written between them. */
  private list(nodes: BbNode[]): string {
    return nodes
      .map((node) => {
        if (node.t === "text") return escapeText(node.s);
        if (this.isBlock(node)) return this.block(node, NO_GLUE, true);
        return this.inline(node);
      })
      .join("");
  }

  private block(node: BbNode, glue: Glue, inList: boolean): string {
    const glued = glueAttrs(glue);
    if (node.t === "private") {
      return `<span${this.privateAttrs(node)}${glued}>${this.container(node.children, false)}</span>`;
    }
    if (node.t !== "tag") return this.inline(node);

    if (node.tag === "li") {
      const element = inList ? "li" : "div";
      return `<${element} data-bb-tag="li"${plainSpelling(node)}${glued}>${this.container(node.children, false, inList)}</${element}>`;
    }
    if (isInlineTag(node.tag)) {
      const element = INLINE_ELEMENTS[node.tag];
      return `<${element} data-bb-tag="${node.tag}"${plainSpelling(node)}${glued}>${this.container(node.children, false)}</${element}>`;
    }

    const [element, cls] = BLOCK_ELEMENTS[node.tag];
    const classAttr = cls ? ` class="${cls}"` : "";
    let extra = plainSpelling(node);
    if (node.tag === "spoiler") {
      const canonical = writeOpening("spoiler", [["text", node.caption]]);
      extra =
        attr("data-bb-text", node.caption) +
        spellingAttrs(node.open, canonical, node.close, "[/spoiler]");
    }
    if (node.tag === "ul" || node.tag === "ol") {
      if (!holdsOnlyItems(node.children)) {
        // A list holding more than items: drawn as a block holding what it
        // holds, or the visual mode would move the rest out, out of a hidden
        // block around it too.
        return `<div data-bb-tag="${node.tag}" data-bb-loose="true"${extra}${glued}>${this.container(node.children, false)}</div>`;
      }
      return `<${element}${classAttr} data-bb-tag="${node.tag}"${extra}${glued}>${this.list(node.children)}</${element}>`;
    }
    return `<${element}${classAttr} data-bb-tag="${node.tag}"${extra}${glued}>${this.container(node.children, false)}</${element}>`;
  }

  private privateAttrs(node: Extract<BbNode, { t: "private" }>): string {
    return ` class="bb-private" data-bb-tag="private"${attr(
      "data-bb-character",
      node.value,
    )}${spellingAttrs(
      node.open,
      writePrivateOpening(node.value),
      node.close,
      "[/private]",
    )}${node.close === null ? ' data-bb-unclosed="true"' : ""}`;
  }

  private inline(node: BbNode): string {
    switch (node.t) {
      case "text":
        return escapeLines(node.s);
      case "tab":
        return `<span class="bb-tab" data-bb-tag="tab"${attr("data-bb-open", node.open !== "[tab]" && node.open)}>&nbsp;&nbsp;&nbsp;</span>`;
      case "verbatim":
        return `<span class="bb-noparse" data-bb-tag="noparse"${plainSpelling(node)}>${escapeLines(node.s)}</span>`;
      case "img":
        return renderImage(node);
      case "link":
        return renderLink(node);
      case "mention":
        return (
          `<a class="bb-mention" href="/users/${encodeURIComponent(node.login)}"` +
          ` data-bb-tag="mention" data-bb-user="${escapeHtml(node.login)}"` +
          `${attr("data-bb-source", node.source !== writeMention(node.login) && node.source)}` +
          `>@${escapeText(node.login)}</a>`
        );
      case "private":
        return `<span${this.privateAttrs(node)}>${node.children.map((child) => this.inline(child)).join("")}</span>`;
      case "tip":
        // The hint has no visual mode: its tag is the text as typed here,
        // and comes back from it as typed. Its preview is hintToHtml.
        return escapeLines(node.source);
      case "tag": {
        if (this.isBlock(node)) return this.block(node, NO_GLUE, false);
        const element = INLINE_ELEMENTS[node.tag as InlineTag];
        return `<${element} data-bb-tag="${node.tag}"${plainSpelling(node)}>${node.children.map((child) => this.inline(child)).join("")}</${element}>`;
      }
    }
  }
}

/** Items and nothing else, white space aside; an empty list holds something else. */
function holdsOnlyItems(nodes: BbNode[]): boolean {
  return (
    nodes.some((node) => node.t === "tag" && node.tag === "li") &&
    nodes.every(
      (node) =>
        (node.t === "tag" && node.tag === "li") ||
        (node.t === "text" && !trimWhiteSpace(node.s)),
    )
  );
}

/** Split a run of inline content into lines at the line breaks of its text. */
function splitLines(run: BbNode[]): BbNode[][] {
  const lines: BbNode[][] = [[]];
  for (const node of run) {
    if (node.t !== "text") {
      lines[lines.length - 1].push(node);
      continue;
    }
    node.s.split(/\r?\n/).forEach((part, index) => {
      if (index > 0) lines.push([]);
      if (part) lines[lines.length - 1].push({ t: "text", s: part });
    });
  }
  return lines;
}

/** Options of bbcodeToHtml. */
export interface BbcodeToHtmlOptions {
  /** The tag set of the surface the text is for; every tag when absent. */
  context?: BBCodeContext;
}

/**
 * Every element carries the data-bb-* attributes htmlToBbcode reads back.
 *
 * @example
 * bbcodeToHtml("[b]Hello[/b]")
 * // => '<p><strong data-bb-tag="b">Hello</strong></p>'
 *
 * bbcodeToHtml('[link text="Home"]https://example.com/[/link]')
 * // => '<p><a href="https://example.com/" data-bb-tag="link" data-bb-text="Home" ...>Home</a></p>'
 */
export function bbcodeToHtml(
  bbcode: string,
  options: BbcodeToHtmlOptions = {},
): string {
  if (!bbcode) return "";
  const tags = options.context
    ? new Set(CONTEXT_TAGS[options.context])
    : ALL_TAGS;
  const nodes = readText(asTheServerReads(bbcode), tags, null);
  return new HtmlWriter().container(nodes, true);
}

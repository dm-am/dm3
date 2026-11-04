// The way back: the visual mode's HTML to BBCode, every text as it was typed.

import { DEFAULT_LINK_TEXT } from "./bbcodeConstants";
import { ALL_TAGS, isPixels, type BbNode } from "./bbcodeTagSet";
import {
  asTheServerReads,
  asciiLower,
  closingBrackets,
  findCloser,
  isPrivateToken,
  isSpace,
  isWritablePrivateValue,
  isWritableValue,
  readPrivateOpening,
  trimWhiteSpace,
  writeImage,
  writeLink,
  writeMention,
  writeOpening,
  writePrivateOpening,
} from "./bbcodeSpelling";
import { readText } from "./bbcodeReader";
import { isSafeAddress } from "./bbcodeAddresses";

import {
  EXAMPLE_CLOSING,
  EXAMPLE_OPENING,
  keptInside,
  markNotIn,
  settleExamples,
} from "./bbcodeExamples";

/** Elements that stand on lines of their own. */
const BLOCK_HTML: ReadonlySet<string> = new Set([
  "DIV",
  "BLOCKQUOTE",
  "UL",
  "OL",
  "LI",
  "PRE",
  "HR",
  "TABLE",
  "SECTION",
  "ARTICLE",
  "HEADER",
  "FOOTER",
  "H1",
  "H2",
  "H3",
  "H4",
  "H5",
  "H6",
]);

type Kind = "p" | "block" | "inline";

function kindOf(node: Node): Kind | null {
  if (node.nodeType === Node.TEXT_NODE) return "inline";
  if (node.nodeType !== Node.ELEMENT_NODE) return null;
  const element = node as Element;
  if (element.tagName === "P") return "p";
  if (BLOCK_HTML.has(element.tagName)) return "block";
  return element.querySelector("p, div, blockquote, ul, ol, li, pre")
    ? "block"
    : "inline";
}

function flag(node: Node, name: string): boolean {
  return (node as Element).getAttribute?.(name) === "true";
}

/**
 * The line break between two neighbours: paragraphs are lines and a block
 * stands on its own line unless glued; text next to a block is read as written.
 */
function separator(
  prev: { node: Node; kind: Kind },
  next: { node: Node; kind: Kind },
): string {
  if (prev.kind === "inline" || next.kind === "inline") return "";
  if (prev.kind === "p" && next.kind === "p") return "\n";
  const glued =
    (prev.kind === "block" && flag(prev.node, "data-bb-glue-after")) ||
    (next.kind === "block" && flag(next.node, "data-bb-glue-before"));
  return glued ? "" : "\n";
}

type Mode = "root" | "container" | "list";

/** Text with its <br> as breaks; a typed no-break space stays (pasted ones are cleaned on paste). */
function textOf(node: Node): string {
  if (node.nodeType === Node.TEXT_NODE) return node.nodeValue ?? "";
  if ((node as Element).tagName === "BR") return "\n";
  let out = "";
  node.childNodes.forEach((child) => {
    out += textOf(child);
  });
  return out;
}

class BbcodeWriter {
  /**
   * @param mark Put before every example and every hidden block the writer
   *   opens (settleExamples): a character the markup does not hold.
   */
  constructor(private readonly mark: string) {}

  children(parent: Node, mode: Mode): string {
    const items: Array<{ node: Node; kind: Kind }> = [];
    parent.childNodes.forEach((node) => {
      const kind = kindOf(node);
      if (kind) items.push({ node, kind });
    });

    if (mode === "root") {
      // The visual mode keeps an empty paragraph after a closing block, for
      // the caret to go to, and leaves it there when the block is taken off.
      // Empty lines at the very end are nothing a reader sees, and the text
      // ends where its last written line does.
      while (
        items.length > 0 &&
        items[items.length - 1].kind === "p" &&
        !items[items.length - 1].node.hasChildNodes()
      ) {
        items.pop();
      }
    }

    let out = "";
    items.forEach((item, index) => {
      if (mode !== "list" && index > 0)
        out += separator(items[index - 1], item);
      out += this.node(item.node);
    });

    const last = items[items.length - 1];
    if (
      mode === "root" &&
      last?.kind === "block" &&
      flag(last.node, "data-bb-newline-after")
    ) {
      out += "\n";
    }
    return out;
  }

  private wrap(
    tag: string,
    element: Element,
    mode: Mode = "container",
  ): string {
    const open = kept(element, "data-bb-open", `[${tag}]`);
    const close = kept(element, "data-bb-close", `[/${tag}]`);
    return `${open}${this.children(element, mode)}${close}`;
  }

  /**
   * [noparse]: its content as typed. A closing tag in the text is split after
   * its "[" into two examples, or it would end the example early and, inside a
   * hidden block, leak the rest.
   */
  private example(element: Element): string {
    const open = kept(element, "data-bb-open", EXAMPLE_OPENING);
    const close = kept(element, "data-bb-close", EXAMPLE_CLOSING);
    const text = textOf(element);
    let held = "";
    let from = 0;
    for (
      let at = findCloser(text, "noparse", 0);
      at >= 0;
      at = findCloser(text, "noparse", at + 1)
    ) {
      held += `${text.slice(from, at + 1)}${EXAMPLE_CLOSING}${this.mark}${EXAMPLE_OPENING}`;
      from = at + 1;
    }
    return `${this.mark}${open}${held}${text.slice(from)}${close}`;
  }

  private node(node: Node): string {
    if (node.nodeType === Node.TEXT_NODE) return node.nodeValue ?? "";
    const element = node as Element;

    switch (element.getAttribute("data-bb-tag")) {
      case "b":
      case "i":
      case "u":
      case "strike":
      case "nsfw":
      case "mod":
      case "quote":
        return this.wrap(
          element.getAttribute("data-bb-tag") as string,
          element,
        );
      case "ul":
      case "ol":
        return this.wrap(
          element.getAttribute("data-bb-tag") as string,
          element,
          flag(element, "data-bb-loose") ? "container" : "list",
        );
      case "li":
        return this.wrap("li", element);
      case "code":
        return this.wrap("code", element);
      case "noparse":
        return this.example(element);
      case "tab":
        return kept(element, "data-bb-open", "[tab]");
      case "spoiler":
        return this.spoiler(element);
      case "link":
        return this.markedLink(element);
      case "mention":
        return this.mention(element);
      case "img":
        return this.image(element, null);
      case "private":
        return this.hidden(element);
    }

    const classes = element.classList;
    switch (element.tagName) {
      case "P":
        return this.children(element, "container");
      case "BR":
        return "\n";
      case "STRONG":
      case "B":
        return this.wrap("b", element);
      case "EM":
      case "I":
        return this.wrap("i", element);
      case "U":
        return this.wrap("u", element);
      case "S":
      case "STRIKE":
      case "DEL":
        return this.wrap("strike", element);
      case "CODE":
      case "PRE":
        return this.wrap("code", element);
      case "UL":
        return this.wrap("ul", element, "list");
      case "OL":
        return this.wrap("ol", element, "list");
      case "LI":
        return this.wrap("li", element);
      case "BLOCKQUOTE":
        return this.wrap("quote", element);
      case "A":
        return classes.contains("bb-mention")
          ? this.mention(element)
          : this.plainLink(element);
      case "IMG":
        return this.plainImage(element);
      case "SCRIPT":
      case "STYLE":
      case "TEMPLATE":
        return "";
      case "DIV":
        if (classes.contains("spoiler") || classes.contains("bb-spoiler")) {
          return this.spoiler(element);
        }
        if (classes.contains("nsfw-spoiler") || classes.contains("bb-nsfw")) {
          return this.wrap("nsfw", element);
        }
        if (classes.contains("mod-block") || classes.contains("bb-mod")) {
          return this.wrap("mod", element);
        }
        if (classes.contains("private-message")) return this.hidden(element);
        return this.children(element, "container");
      case "SPAN":
        if (classes.contains("bb-image-frame")) {
          const img = element.querySelector("img");
          return img ? this.image(img, element) : "";
        }
        if (classes.contains("bb-tab")) {
          return kept(element, "data-bb-open", "[tab]");
        }
        if (classes.contains("bb-noparse")) {
          return this.example(element);
        }
        if (
          classes.contains("bb-private") ||
          classes.contains("private-text")
        ) {
          return this.hidden(element);
        }
        return this.children(element, "container");
      default:
        return this.children(element, "container");
    }
  }

  private spoiler(element: Element): string {
    let caption = element.getAttribute("data-bb-text");
    if (caption !== null && !isWritableValue(caption)) caption = null;
    const canonical = writeOpening("spoiler", [["text", caption]]);
    const raw = element.getAttribute("data-bb-open");
    const open = raw !== null && readsAsSpoiler(raw, caption) ? raw : canonical;
    const close = kept(element, "data-bb-close", "[/spoiler]");
    return `${open}${this.children(element, "container")}${close}`;
  }

  private hidden(element: Element): string {
    let value: string | null;
    if (element.hasAttribute("data-bb-character")) {
      value = element.getAttribute("data-bb-character");
    } else {
      value = element.getAttribute("data-character");
    }
    // An addressee list that cannot be written is dropped, never the block:
    // a block addressed to nobody hides more, not less.
    if (value !== null && !isWritablePrivateValue(value)) value = null;

    const raw = element.getAttribute("data-bb-open");
    const open =
      raw !== null && readsAsPrivate(raw, value)
        ? raw
        : writePrivateOpening(value);
    const close = flag(element, "data-bb-unclosed")
      ? ""
      : kept(element, "data-bb-close", "[/private]");
    const body = keptInside(this.children(element, "container"), this.mark);
    // A spelling ending right after the name is a block only while what
    // follows ends the token; the visual mode can put a tag there ("[private"
    // and "[u]"), which would leak the text. A space keeps it the block it was.
    const endsAtName = /[A-Za-z]$/.test(open);
    const tokenEnds =
      body === ""
        ? close === ""
        : body[0] === "]" || body[0] === "=" || isSpace(body[0]);
    return `${this.mark}${open}${endsAtName && !tokenEnds ? " " : ""}${body}${close}`;
  }

  private markedLink(element: Element): string {
    const href = element.getAttribute("href") ?? "";
    let text: string | null;
    if (flag(element, "data-bb-selfref")) text = null;
    else if (element.hasAttribute("data-bb-text")) {
      text = element.getAttribute("data-bb-text");
    } else {
      const shown = element.textContent ?? "";
      text = shown === DEFAULT_LINK_TEXT ? null : shown;
    }
    if (!isSafeAddress(href)) return text ?? "";
    if (text !== null && !isWritableValue(text)) text = null;
    const canonical = writeLink(trimWhiteSpace(href), text);
    const link = fromSource(
      element,
      canonical,
      (node) =>
        node.t === "link" &&
        node.href === trimWhiteSpace(href) &&
        node.text === text,
    );
    // Two links side by side with the same address and caption are one run
    // of text under one mark in the visual mode, which joins neighbours that
    // carry the same mark, and came back as one link. What such an anchor
    // shows is the caption as many times as there were links.
    const caption = text ?? DEFAULT_LINK_TEXT;
    const shown = element.textContent ?? "";
    const times =
      caption && shown.length % caption.length === 0
        ? shown.length / caption.length
        : 1;
    return times > 1 && caption.repeat(times) === shown
      ? link.repeat(times)
      : link;
  }

  private plainLink(element: Element): string {
    const href = trimWhiteSpace(element.getAttribute("href") ?? "");
    const text = trimWhiteSpace(element.textContent ?? "");
    if (!isSafeAddress(href)) return text || href;
    if (
      !text ||
      text === href ||
      text === DEFAULT_LINK_TEXT ||
      !isWritableValue(text)
    ) {
      return writeLink(href, null);
    }
    return writeLink(href, text);
  }

  private mention(element: Element): string {
    const login = element.getAttribute("data-bb-user") ?? "";
    if (!trimWhiteSpace(login) || /[[\r\n]/.test(login))
      return element.textContent ?? "";
    return fromSource(
      element,
      writeMention(trimWhiteSpace(login)),
      (node) => node.t === "mention" && node.login === trimWhiteSpace(login),
    );
  }

  private image(img: Element, frame: Element | null): string {
    const src = trimWhiteSpace(img.getAttribute("src") ?? "");
    if (!isSafeAddress(src)) return "";
    const size = (name: string): string | null => {
      const value = frame?.getAttribute(name) ?? null;
      return value !== null && isPixels(value) ? value : null;
    };
    const width = size("data-bb-width");
    const height = size("data-bb-height");
    const canonical = writeImage(src, width, height);
    return fromSource(
      img,
      canonical,
      (node) =>
        node.t === "img" &&
        node.src === src &&
        node.width === width &&
        node.height === height,
    );
  }

  private plainImage(img: Element): string {
    const src = trimWhiteSpace(img.getAttribute("src") ?? "");
    return isSafeAddress(src) ? `[img]${src}[/img]` : "";
  }
}

/** Whether a kept spoiler opening still says what the element says. */
function readsAsSpoiler(raw: string, caption: string | null): boolean {
  const nodes = readText(`${raw}[/spoiler]`, ALL_TAGS, null);
  const node = nodes[0];
  return (
    nodes.length === 1 &&
    node.t === "tag" &&
    node.tag === "spoiler" &&
    node.caption === caption &&
    node.children.length === 0
  );
}

/** Whether a kept [private] opening still says what the element says. */
function readsAsPrivate(raw: string, value: string | null): boolean {
  if (!isPrivateToken(raw, 0)) return false;
  const opening = readPrivateOpening(raw, closingBrackets(raw), 8);
  return opening.end === raw.length && opening.value === value;
}

/** A tag without parameters as typed ([B] for [b]), else the editor's spelling. */
function kept(element: Element, name: string, canonical: string): string {
  const raw = element.getAttribute(name);
  return raw !== null && asciiLower(raw) === canonical ? raw : canonical;
}

/**
 * An image, a link or a mention as the author typed it, when the element
 * still says what that spelling says; the canonical spelling otherwise.
 */
function fromSource(
  element: Element,
  canonical: string,
  same: (node: BbNode) => boolean,
): string {
  const source = element.getAttribute("data-bb-source");
  if (source === null) return canonical;
  const nodes = readText(source, ALL_TAGS, null);
  return nodes.length === 1 && same(nodes[0]) ? source : canonical;
}

/**
 * Every text and attribute as the server reads it (asTheServerReads): taking
 * out its characters can join a closing tag ("[/pri" U+0001 "vate]"). Only
 * pasted or typed text holds them.
 */
function readAsTheServerDoes(root: Node): void {
  const walker = document.createTreeWalker(
    root,
    NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT,
  );
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    if (node.nodeType === Node.TEXT_NODE) {
      const text = node.nodeValue ?? "";
      const read = asTheServerReads(text);
      if (read !== text) node.nodeValue = read;
      continue;
    }
    for (const attribute of Array.from((node as Element).attributes)) {
      const read = asTheServerReads(attribute.value);
      if (read !== attribute.value) attribute.value = read;
    }
  }
}

/**
 * The visual mode's HTML back to BBCode; read by data-bb-tag first, then tag
 * and class for pasted HTML. An address the server refuses does not come back
 * as markup. A closing tag typed in a hidden block or an example, and an
 * example around a hidden block, are written as examples of themselves, or
 * they would leak the block.
 *
 * @example
 * htmlToBbcode('<p><strong data-bb-tag="b">Hello</strong></p>')
 * // => "[b]Hello[/b]"
 *
 * htmlToBbcode('<a href="https://example.com/">Home</a>')
 * // => '[link text="Home"]https://example.com/[/link]'
 */
export function htmlToBbcode(html: string): string {
  if (!html) return "";
  const template = document.createElement("template");
  template.innerHTML = html;
  readAsTheServerDoes(template.content);
  const mark = markNotIn(template.innerHTML);
  return settleExamples(
    new BbcodeWriter(mark).children(template.content, "root"),
    mark,
  );
}

/**
 * Rendered markup read into what the eye gets: runs of text with the set they
 * are drawn with, where the server's page and the editor's preview meet; and
 * what the browser builds of an HTML string against what its writer nested.
 * Spec-only; docs/guides/TESTING.md, "Генеративные проверки разметки".
 */

/**
 * A run of text and what it is drawn with (sorted, joined by "|"). Space and
 * undrawn characters are left out, and dashes read as the hyphens they stand
 * for: the page draws "--" as a dash, the preview keeps it typed.
 */
export interface Run {
  text: string;
  marks: string;
}

/**
 * The leads, whom the recipients of every block of private text name first
 * (PrivateTag).
 */
const LEADS = ["DungeonMaster", "Assistant"];

/**
 * The recipients a block of private text carries for its tooltip
 * (data-recipients): the leads, then the names of the block, if it has any.
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

/** An image of the preview, with the size its frame carries when it has one. */
function framedImage(img: Element): string {
  return image(
    img,
    img.parentElement?.classList.contains("bb-image-frame")
      ? img.parentElement
      : null,
  );
}

/** The nodes the browser builds of a string of HTML. */
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
 * The author's preview (bbcodeToHtml) read by data-bb-tag.
 *
 * @param lines Keep the lines too, for one preview against another: "¶"
 *   ends a paragraph, "⏎" is a break, "⊂"/"⊃" a block glued
 *   before/after, "↵" a break after it at the end. Never against the page.
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
  // The layout the preview draws around the tags: paragraphs, breaks, image frames.
  const untagged = (element: Element, marks: string[], inside: () => void) => {
    if (element.tagName === "P") {
      inside();
      if (lines) runs.atom("\u00b6", marks);
    } else if (element.tagName === "BR") {
      if (lines) runs.atom("\u23ce", marks);
    } else if (element.classList.contains("bb-image-frame")) inside();
    else unknown(element, "the preview");
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
          runs.atom("<img>", [...marks, framedImage(element)]);
          break;
        case "private":
          inside(addressees(element.getAttribute("data-bb-character")));
          break;
        case null:
          untagged(element, marks, () => inside(null));
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

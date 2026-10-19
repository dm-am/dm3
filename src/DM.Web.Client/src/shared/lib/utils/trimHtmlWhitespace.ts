/** A <br>, or an empty <p>/<div> without media. */
function isTrimmableEmptyElement(elem: Element): boolean {
  const tagName = elem.tagName.toUpperCase();
  if (tagName === "BR") return true;
  if (tagName === "P" || tagName === "DIV") {
    return (
      elem.textContent?.trim() === "" &&
      !elem.querySelector("img, iframe, video, audio, svg")
    );
  }
  return false;
}

/**
 * Strips leading and trailing empty content (<br>, empty <p>/<div>,
 * whitespace text) of an HTML fragment; empty lines in the middle are the
 * author's. Parsed in a detached <template>, for a computed before v-html.
 */
export function trimHtmlWhitespace(html: string | null | undefined): string {
  if (!html) return "";
  const template = document.createElement("template");
  template.innerHTML = html;
  trimEdge(template.content, "start");
  trimEdge(template.content, "end");
  return template.innerHTML;
}

function trimEdge(
  root: DocumentFragment | Element,
  side: "start" | "end",
): void {
  const edge = () => (side === "start" ? root.firstChild : root.lastChild);
  for (let node = edge(); node; node = edge()) {
    if (node.nodeType === Node.TEXT_NODE) {
      const text = node.textContent || "";
      if (text.trim() === "") {
        node.remove();
        continue;
      }
      node.textContent = side === "start" ? text.trimStart() : text.trimEnd();
      break;
    }
    if (node.nodeType === Node.ELEMENT_NODE) {
      const elem = node as Element;
      if (isTrimmableEmptyElement(elem)) {
        elem.remove();
        continue;
      }
      trimEdge(elem, side);
      break;
    }
    break;
  }
}

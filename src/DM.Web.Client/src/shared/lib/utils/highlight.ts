/** Escape HTML special characters to prevent XSS */
function escapeHtml(text: string): string {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

/** A search query as a case-insensitive pattern that captures each match. */
function matchesOf(query: string): RegExp {
  return new RegExp(`(${query.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")})`, "gi");
}

/**
 * Highlight matching portions of text by wrapping them in <mark> tags.
 *
 * @param text - The text to search in
 * @param query - The search query to highlight
 * @returns HTML string with matches wrapped in <mark> tags
 */
export function highlightMatch(text: string, query: string): string {
  if (!query || !text) {
    return escapeHtml(text || "");
  }

  const parts = text.split(matchesOf(query));

  return parts
    .map((part) => {
      if (part.toLowerCase() === query.toLowerCase()) {
        return `<mark class="search-highlight">${escapeHtml(part)}</mark>`;
      }
      return escapeHtml(part);
    })
    .join("");
}

/**
 * Highlights matches in rendered content, text nodes only, so it is safe for
 * BBCode. Call `clearDomHighlight(el)` before highlighting again.
 */
export function highlightDom(el: HTMLElement, query: string): void {
  if (!query) return;

  const regex = matchesOf(query);
  const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);

  const matches: { node: Text; index: number; length: number }[] = [];

  // Collected first: the DOM cannot change during the walk. A .bb-private node
  // in the DOM is one the server let the viewer see.
  let node: Text | null;
  while ((node = walker.nextNode() as Text | null)) {
    if (node.parentElement?.classList.contains("search-highlight")) continue;

    let match: RegExpExecArray | null;
    regex.lastIndex = 0;
    while ((match = regex.exec(node.textContent ?? "")) !== null) {
      matches.push({ node, index: match.index, length: match[1].length });
    }
  }

  // In reverse to keep the indices; a range across an element boundary throws
  // HierarchyRequestError and is skipped.
  for (let i = matches.length - 1; i >= 0; i--) {
    try {
      const { node: textNode, index, length } = matches[i];
      const range = document.createRange();
      range.setStart(textNode, index);
      range.setEnd(textNode, index + length);
      const mark = document.createElement("mark");
      mark.className = "search-highlight";
      range.surroundContents(mark);
    } catch {
      // Skip matches that cross element boundaries
    }
  }
}

/** Unwraps every <mark class="search-highlight"> back to plain text. */
export function clearDomHighlight(el: HTMLElement): void {
  const marks = el.querySelectorAll("mark.search-highlight");
  marks.forEach((mark) => {
    const parent = mark.parentNode;
    if (parent) {
      parent.replaceChild(
        document.createTextNode(mark.textContent ?? ""),
        mark,
      );
      parent.normalize(); // Merge adjacent text nodes
    }
  });
}

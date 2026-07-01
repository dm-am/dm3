/**
 * Utility for highlighting search matches in text.
 * Escapes HTML and wraps matched portions in <mark> tags.
 */

/**
 * Escape HTML special characters to prevent XSS
 */
function escapeHtml(text: string): string {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
}

/**
 * Highlight matching portions of text by wrapping them in <mark> tags.
 * Returns HTML string with highlighted matches.
 *
 * @param text - The text to search in
 * @param query - The search query to highlight
 * @returns HTML string with matches wrapped in <mark> tags
 */
export function highlightMatch(text: string, query: string): string {
  if (!query || !text) {
    return escapeHtml(text || "");
  }

  // Escape special regex characters in query
  const escapedQuery = query.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

  // Case-insensitive match
  const regex = new RegExp(`(${escapedQuery})`, "gi");

  // Split text into matched and unmatched parts
  const parts = text.split(regex);

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
 * Check if text contains the search query (case-insensitive)
 */
export function containsMatch(text: string, query: string): boolean {
  if (!query || !text) return false;
  return text.toLowerCase().includes(query.toLowerCase());
}

/**
 * Highlight search matches in already-rendered DOM content.
 * Walks text nodes and wraps matches in <mark class="search-highlight">.
 * Safe for BBCode/HTML content — operates on text nodes only.
 *
 * Call `clearDomHighlight(el)` before re-highlighting to remove previous marks.
 */
export function highlightDom(el: HTMLElement, query: string): void {
  if (!query) return;

  const escapedQuery = query.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  const regex = new RegExp(`(${escapedQuery})`, "gi");
  const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);

  const matches: { node: Text; index: number; length: number }[] = [];

  // Collect matches first (can't modify DOM during walk)
  let node: Text | null;
  while ((node = walker.nextNode() as Text | null)) {
    // Skip nodes inside <mark> (already highlighted)
    if (node.parentElement?.classList.contains("search-highlight")) continue;
    // NOTE: .bb-private nodes are NOT skipped — if present in DOM, the server
    // already confirmed the viewer has access, so highlighting is correct.

    let match: RegExpExecArray | null;
    regex.lastIndex = 0;
    while ((match = regex.exec(node.textContent ?? "")) !== null) {
      matches.push({ node, index: match.index, length: match[1].length });
    }
  }

  // Apply highlights in reverse order to preserve indices.
  // try/catch guards against HierarchyRequestError if a range
  // crosses an element boundary (rare in practice with BBCode).
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

/**
 * Remove all search highlights from a DOM element.
 * Unwraps <mark class="search-highlight"> back to plain text.
 */
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

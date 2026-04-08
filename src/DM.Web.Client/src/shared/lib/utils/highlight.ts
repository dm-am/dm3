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

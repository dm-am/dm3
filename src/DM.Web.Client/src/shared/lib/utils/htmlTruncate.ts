/**
 * Truncates HTML content to approximately fit within maxChars characters.
 * Properly handles HTML tags by walking the DOM tree and closing tags correctly.
 */
export function truncateHtml(
  html: string,
  maxChars: number,
): { html: string; truncated: boolean } {
  if (!html) return { html: "", truncated: false };

  const container = document.createElement("div");
  container.innerHTML = html;

  const totalLength = container.textContent?.length || 0;
  if (totalLength <= maxChars) {
    return { html, truncated: false };
  }

  let charCount = 0;
  let done = false;

  const walkAndTruncate = (node: Node): boolean => {
    if (done) return false;

    if (node.nodeType === Node.TEXT_NODE) {
      const text = node.textContent || "";
      if (charCount + text.length > maxChars) {
        const available = maxChars - charCount;
        // Find word boundary for cleaner cut
        let cutPoint = available;
        const lastSpace = text.lastIndexOf(" ", available);
        if (lastSpace > available * 0.3) {
          cutPoint = lastSpace;
        }
        node.textContent = text.substring(0, cutPoint).trimEnd();
        done = true;
        return false;
      }
      charCount += text.length;
      return true;
    }

    if (node.nodeType === Node.ELEMENT_NODE) {
      const el = node as Element;
      const children = Array.from(el.childNodes);

      for (let i = 0; i < children.length; i++) {
        if (!walkAndTruncate(children[i])) {
          // Remove all following siblings
          for (let j = children.length - 1; j > i; j--) {
            el.removeChild(children[j]);
          }
          return false;
        }
      }
      return true;
    }

    return true;
  };

  walkAndTruncate(container);

  return { html: container.innerHTML, truncated: true };
}

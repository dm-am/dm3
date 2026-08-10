/**
 * Free-text URL fields coming from the backend (e.g. UserAward.workUrl,
 * ContestSeries.topicUrl — admin-entered "link to the forum topic with the
 * work/results") may be stored as either an absolute URL on one of the site's
 * own addresses or an already-relative path (`/forum/general/1`). The site
 * answers on more than one address, so the origin a link was written on is not
 * the origin the reader is on, and neither can be hardcoded here — instead we
 * strip whatever origin the browser reports if it matches.
 *
 * Returns a router-relative path when the URL is internal (safe to render
 * as a `<router-link>` instead of a real `target="_blank"` anchor), or
 * `null` when the URL points elsewhere (external link — keep it a plain
 * anchor, opened in the same tab per the site's no-hijack convention).
 */
export function toInternalPath(url: string | null | undefined): string | null {
  if (!url) return null;
  const trimmed = url.trim();
  if (!trimmed) return null;

  // Already a relative path.
  if (trimmed.startsWith("/")) return trimmed;

  try {
    const parsed = new URL(trimmed, window.location.origin);
    if (parsed.origin === window.location.origin) {
      return `${parsed.pathname}${parsed.search}${parsed.hash}`;
    }
  } catch {
    // Not a parseable absolute URL — treat as external/unknown.
  }
  return null;
}

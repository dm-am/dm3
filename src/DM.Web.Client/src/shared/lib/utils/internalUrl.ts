/**
 * Free-text URL fields (UserAward.workUrl, ContestSeries.topicUrl) may hold an
 * absolute URL on one of the site's addresses or a relative path. Nothing
 * validates them on the way in, so both the internal path and the external href
 * are decided here.
 */

/** The value resolved against the page the reader is on, or null if it does not parse. */
function parse(url: string | null | undefined): URL | null {
  if (!url) return null;
  const trimmed = url.trim();
  if (!trimmed) return null;

  try {
    return new URL(trimmed, window.location.origin);
  } catch {
    // Not a parseable URL — external/unknown, and not renderable either.
    return null;
  }
}

/**
 * A router-relative path for an internal URL, or null. Every value is parsed:
 * "//host/x" and "/\host/x" start with "/" and lead elsewhere.
 */
export function toInternalPath(url: string | null | undefined): string | null {
  const parsed = parse(url);
  return parsed && parsed.origin === window.location.origin
    ? `${parsed.pathname}${parsed.search}${parsed.hash}`
    : null;
}

/**
 * The href an external value may be rendered with: http and https only, or
 * null. javascript: and data: parse too and would run script in our origin.
 */
export function toExternalHref(url: string | null | undefined): string | null {
  const parsed = parse(url);
  if (!parsed || parsed.origin === window.location.origin) return null;

  return parsed.protocol === "http:" || parsed.protocol === "https:"
    ? parsed.href
    : null;
}

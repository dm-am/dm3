/**
 * Free-text URL fields coming from the backend (e.g. UserAward.workUrl,
 * ContestSeries.topicUrl — admin-entered "link to the forum topic with the
 * work/results") may be stored as either an absolute URL on one of the site's
 * own addresses or an already-relative path (`/forum/general/1`). The site
 * answers on more than one address, so the origin a link was written on is not
 * the origin the reader is on, and neither can be hardcoded here — instead we
 * strip whatever origin the browser reports if it matches.
 *
 * Nothing validates these fields on the way in: the grant stores what a
 * moderator typed, trimmed and nothing else. Both halves of the decision
 * therefore live here — the internal path AND the href an external value is
 * allowed to be rendered with — because a caller that only asks "is it
 * internal?" still has to do something with the "no", and the obvious something
 * is to drop the raw string into an anchor.
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
 * Returns a router-relative path when the URL is internal (safe to render as a
 * `<router-link>` instead of a real anchor), or `null` when it points anywhere
 * else.
 *
 * Every value goes through the parser, relative ones included. Handing a string
 * back because it starts with "/" was the hole: "//evil.example/x" starts with
 * one and resolves to another host, and so does "/\evil.example/x". The router
 * would have taken the left click to our own 404 while the href it rendered —
 * the one the status bar shows, a middle click follows and "copy link address"
 * hands over — pointed at the other host.
 */
export function toInternalPath(url: string | null | undefined): string | null {
  const parsed = parse(url);
  return parsed && parsed.origin === window.location.origin
    ? `${parsed.pathname}${parsed.search}${parsed.hash}`
    : null;
}

/**
 * Returns the href an external value may be rendered with, or `null` when it
 * may not be rendered as a link at all.
 *
 * http and https only. A URL parses with any scheme, so "javascript:..." and
 * "data:text/html,..." arrive here as perfectly valid non-internal URLs, and an
 * anchor carrying either runs script in our own origin the moment it is
 * clicked — a worse outcome than the disguised-internal link above, reached
 * through the same field. There is no schemeless "link to a topic": everything
 * this field is for is one of the two.
 */
export function toExternalHref(url: string | null | undefined): string | null {
  const parsed = parse(url);
  if (!parsed || parsed.origin === window.location.origin) return null;

  return parsed.protocol === "http:" || parsed.protocol === "https:"
    ? parsed.href
    : null;
}

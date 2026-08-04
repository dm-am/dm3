/**
 * Where a refused viewer is sent, and how they get back.
 *
 * The route guard and the 401 handler both take someone off a page they may not
 * see. The page they were going to is the whole point of the trip, and both of
 * them threw it away: the guard REPLACES the navigation, so the address never
 * entered the history either, and after signing in the reader stood on the home
 * page with the link they had followed nowhere to be found.
 *
 * One key, one shape, read back in exactly one place — the login dialog host.
 */
import type { RouteLocationRaw } from "vue-router";

/** Query key carrying the address the viewer was refused. */
export const REDIRECT_QUERY_KEY = "redirect";

/**
 * The home page with the login dialog open, remembering where the viewer was
 * going.
 *
 * `fullPath` rather than a route name: the query and the hash are part of the
 * address — a page of comments, an anchor on a post. The home page itself is
 * not worth remembering, so it is not written down.
 */
export function loginLocation(intendedFullPath?: string): RouteLocationRaw {
  const query: Record<string, string> = { action: "login" };
  if (intendedFullPath && intendedFullPath !== "/") {
    query[REDIRECT_QUERY_KEY] = intendedFullPath;
  }
  return { name: "home", query };
}

/**
 * The address to resume, or null.
 *
 * The value travels through the URL, so it is attacker-writable by definition:
 * anyone can hand out a link to the home page carrying a redirect of their
 * choosing. Only a path on this site is resumed. "//host" is protocol-relative
 * and leaves the site; "/\\host" is the same trick through a backslash, which
 * several browsers normalise into it.
 */
export function resumeTarget(value: unknown): string | null {
  if (typeof value !== "string" || !value.startsWith("/")) return null;
  if (value.startsWith("//") || value.startsWith("/\\")) return null;
  return value;
}

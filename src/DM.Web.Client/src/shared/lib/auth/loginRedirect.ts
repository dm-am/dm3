// Where a refused viewer is sent and how he gets back: one key, read back in
// one place, the login dialog host.
import type { RouteLocationRaw } from "vue-router";

/** Query key carrying the address the viewer was refused. */
export const REDIRECT_QUERY_KEY = "redirect";

/**
 * The home page with the login dialog open, remembering where the viewer was
 * going: fullPath, since the query and the hash are part of the address.
 */
export function loginLocation(intendedFullPath?: string): RouteLocationRaw {
  const query: Record<string, string> = { action: "login" };
  if (intendedFullPath && intendedFullPath !== "/") {
    query[REDIRECT_QUERY_KEY] = intendedFullPath;
  }
  return { name: "home", query };
}

/**
 * The address to resume, or null. Anyone can hand out a link with a redirect,
 * so only a path on this site is resumed: "//host" and "/\host" leave it.
 */
export function resumeTarget(value: unknown): string | null {
  if (typeof value !== "string" || !value.startsWith("/")) return null;
  if (value.startsWith("//") || value.startsWith("/\\")) return null;
  return value;
}

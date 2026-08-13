/**
 * The confirmation value out of a mailed link.
 *
 * It travels in the fragment rather than in the path. A fragment is never sent to
 * a server: it stays out of the access log of the edge, out of the Referer of every
 * request the page then makes, and out of anything a proxy records. In the path it
 * was in all three, and in the browser history besides — for a value that is the
 * single factor of a password reset.
 *
 * Reading it also clears it, so a screenshot, a shared window or the back button
 * do not carry it further than the moment it is used.
 */

/** Fragment key the mailed links use. */
const KEY = "token";

/**
 * Take the confirmation value from the current address and remove it from there.
 *
 * @returns The value, or an empty string when the link carries none
 */
export function readConfirmationToken(): string {
  const fragment = window.location.hash.startsWith("#")
    ? window.location.hash.slice(1)
    : window.location.hash;

  const token = new URLSearchParams(fragment).get(KEY) ?? "";
  if (!token) return "";

  // replaceState rather than assigning location.hash: the second one adds a
  // history entry holding exactly the value being hidden.
  window.history.replaceState(
    window.history.state,
    "",
    window.location.pathname + window.location.search,
  );

  return token;
}

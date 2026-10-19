/**
 * The confirmation value of a mailed link travels in the fragment, which no
 * server, Referer or proxy sees, and reading it clears it from the address.
 */

/** Fragment key the mailed links use. */
const KEY = "token";

/** @returns The value, or an empty string when the link carries none */
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

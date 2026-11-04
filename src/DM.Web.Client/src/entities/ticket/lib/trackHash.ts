// The token is a credential, so it travels in the fragment, which no server sees.

/** The key the mailed links use for their values. */
const KEY = "token";

export const trackHash = (token: string) =>
  `#${new URLSearchParams({ [KEY]: token })}`;

/** An empty string when the fragment names none. */
export const trackToken = (hash: string) =>
  new URLSearchParams(hash.replace(/^#/, "")).get(KEY) ?? "";

// What every lexer of the comment comparison shares: the token list it fills,
// the error it stops with, and the comparison of two lists.

export class LexError extends Error {
  constructor(at, message) {
    super(message);
    this.at = at;
  }
}

/** A marker key cannot collide with a token: no token starts with NUL. */
export const MARK = "\u0000";

/**
 * The tokens of one file: `keys` are what is compared, `ats` the offset in
 * `text` each key came from (for the report only).
 */
export class Tokens {
  constructor(text) {
    this.text = text;
    this.keys = [];
    this.ats = [];
  }

  push(key, at) {
    this.keys.push(key);
    this.ats.push(at);
  }

  /** Keeps a comment only when it matches one of the directive patterns. */
  comment(comment, at, directives) {
    if (directives.some((pattern) => pattern.test(comment))) {
      this.push(comment.trim(), at);
    }
  }
}

/** A file's text without a byte order mark and with LF line ends. */
export function sourceText(content) {
  return content
    .toString("utf8")
    .replace(/^\uFEFF/, "")
    .replace(/\r\n/g, "\n");
}

/** Line and column (both from 1) of an offset. */
export function position(text, at) {
  let line = 1;
  let lineStart = 0;
  for (
    let i = text.indexOf("\n");
    i !== -1 && i < at;
    i = text.indexOf("\n", i + 1)
  ) {
    line++;
    lineStart = i + 1;
  }
  return `${line}:${at - lineStart + 1}`;
}

function around(tokens, index) {
  const shown = [];
  const end = Math.min(tokens.keys.length, index + 4);
  for (let i = Math.max(0, index - 3); i < end; i++) {
    const key = tokens.keys[i];
    if (key.startsWith(MARK)) continue;
    const text = key.length > 60 ? `${key.slice(0, 57)}...` : key;
    shown.push(i === index ? `>>${text}<<` : text);
  }
  return shown.join(" ");
}

/**
 * Where two token lists first part, or null when they do not. A difference in
 * the syntax tree alone (the same tokens grouped differently) is flagged.
 */
export function firstDifference(base, head) {
  const a = base.keys;
  const b = head.keys;
  let i = 0;
  while (i < a.length && i < b.length && a[i] === b[i]) i++;
  if (i === a.length && i === b.length) return null;
  const plainA = a.filter((key) => !key.startsWith(MARK));
  const plainB = b.filter((key) => !key.startsWith(MARK));
  const treeOnly =
    plainA.length === plainB.length &&
    plainA.every((key, k) => key === plainB[k]);
  const describe = (tokens) =>
    i >= tokens.keys.length
      ? "end of file"
      : `${position(tokens.text, tokens.ats[i])}  ${around(tokens, i)}`;
  return { treeOnly, base: describe(base), head: describe(head) };
}

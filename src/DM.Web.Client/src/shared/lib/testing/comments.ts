/**
 * Drop line, block and HTML comments; keep string literals whole and keep the
 * newlines, so the offset of a match still maps to a line of the file.
 *
 * Only `"` and a backtick open a literal. The codebase writes strings in double
 * quotes, and treating an apostrophe as a delimiter would swallow Russian text
 * after the first one.
 */
export function stripComments(text: string): string {
  let out = "";
  let i = 0;
  while (i < text.length) {
    const comment = COMMENTS.find(([opens]) => text.startsWith(opens, i));
    if (comment) {
      const end = commentEnd(text, i, comment);
      out += text.slice(i, end).replace(/[^\n]/g, "");
      i = end;
    } else {
      const end = QUOTES.includes(text[i]) ? literalEnd(text, i) : i + 1;
      out += text.slice(i, end);
      i = end;
    }
  }
  return out;
}

const COMMENTS: [opens: string, closes: string][] = [
  ["//", "\n"],
  ["/*", "*/"],
  ["<!--", "-->"],
];

const QUOTES = ['"', "`"];

function commentEnd(
  text: string,
  start: number,
  [opens, closes]: [string, string],
): number {
  const closed = text.indexOf(closes, start + opens.length);
  return closed === -1 ? text.length : closed + closes.length;
}

function literalEnd(text: string, start: number): number {
  let i = start + 1;
  while (i < text.length && text[i] !== text[start]) {
    i += text[i] === "\\" ? 2 : 1;
  }
  return Math.min(i + 1, text.length);
}

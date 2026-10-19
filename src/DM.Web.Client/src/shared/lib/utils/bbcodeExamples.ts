// The examples of the way back: a written "[noparse]" and closing tags written
// as examples of themselves, so what the visual mode shows as text stays text.

import { findCloser, isPrivateToken, spellsName } from "./bbcodeSpelling";

/** Whether "[noparse]" in any case stands at this "[": an example may open here. */
function isExampleOpening(src: string, at: number): boolean {
  return (
    src[at] === "[" && spellsName(src, at + 1, "noparse") && src[at + 8] === "]"
  );
}

/** Whether "[/private]" in any case stands at this "[": a hidden block ends here. */
function isHiddenClosing(src: string, at: number): boolean {
  return (
    src[at] === "[" &&
    src[at + 1] === "/" &&
    spellsName(src, at + 2, "private") &&
    src[at + 9] === "]"
  );
}

export const EXAMPLE_OPENING = "[noparse]";
export const EXAMPLE_CLOSING = "[/noparse]";
const HIDDEN_CLOSING_LENGTH = "[/private]".length;

/**
 * A private-use character the markup carries nowhere, put by the writer before
 * what it opens itself.
 */
export function markNotIn(markup: string): string {
  for (let code = 0xe000; code <= 0xf8ff; code++) {
    const mark = String.fromCharCode(code);
    if (!markup.includes(mark)) return mark;
  }
  throw new Error("htmlToBbcode: no free mark for the writer");
}

/** Where the closing tags of an example stand in a text, in order. */
function closingsOf(src: string): number[] {
  const at: number[] = [];
  for (
    let i = findCloser(src, "noparse", 0);
    i >= 0;
    i = findCloser(src, "noparse", i + 1)
  ) {
    at.push(i);
  }
  return at;
}

/** The first of the ordered positions at or after a position, or -1. */
function firstFrom(ordered: readonly number[], from: number): number {
  let low = 0;
  let high = ordered.length;
  while (low < high) {
    const middle = (low + high) >> 1;
    if (ordered[middle] < from) low = middle + 1;
    else high = middle;
  }
  return low < ordered.length ? ordered[low] : -1;
}

/**
 * An example runs from its "[noparse]" to the first closing tag after it. A
 * "[noparse]" from the text followed by a closing tag is one too, unless a
 * hidden block starts before that tag: then it is escaped, or it would print
 * the hidden text to every reader.
 *
 * @returns The closing tag the example ends on, or -1 when there is no
 *   example here; and whether the "[noparse]" is to be written as an example
 *   of itself.
 */
function readExample(
  at: number,
  own: boolean,
  closings: readonly number[],
  hiddenStarts: readonly number[],
): { closing: number; escape: boolean } {
  const closing = firstFrom(closings, at + EXAMPLE_OPENING.length);
  if (closing < 0 || own) return { closing, escape: false };
  const hidden = firstFrom(hiddenStarts, at + 1);
  return hidden >= 0 && hidden < closing
    ? { closing: -1, escape: true }
    : { closing, escape: false };
}

/**
 * The written text with its examples settled (readExample) and the marks
 * taken out.
 */
export function settleExamples(written: string, mark: string): string {
  const pieces = written.split(mark);
  let raw = pieces[0];
  const own = new Set<number>();
  const hiddenStarts: number[] = [];
  for (let i = 1; i < pieces.length; i++) {
    const at = raw.length;
    raw += pieces[i];
    if (isExampleOpening(raw, at)) own.add(at);
    else hiddenStarts.push(at);
  }
  const closings = closingsOf(raw);
  let out = "";
  let from = 0;
  let at = raw.indexOf("[");
  while (at >= 0) {
    if (!isExampleOpening(raw, at)) {
      at = raw.indexOf("[", at + 1);
      continue;
    }
    const end = at + EXAMPLE_OPENING.length;
    const { closing, escape } = readExample(
      at,
      own.has(at),
      closings,
      hiddenStarts,
    );
    if (escape) {
      out +=
        raw.slice(from, at) +
        EXAMPLE_OPENING +
        raw.slice(at, end) +
        EXAMPLE_CLOSING;
      from = end;
    }
    at = raw.indexOf("[", closing < 0 ? end : closing + EXAMPLE_CLOSING.length);
  }
  return out + raw.slice(from);
}

/**
 * The body of a hidden block with every closing tag that would end it early
 * written as an example of itself, or "[/private]" typed inside would leak
 * the rest. A closing tag of a block opened in the body stays.
 *
 * @param mark The writer's mark (markNotIn), put before every example and
 *   every hidden block it opens.
 */
export function keptInside(body: string, mark: string): string {
  const hiddenStarts: number[] = [];
  for (let i = body.indexOf(mark); i >= 0; i = body.indexOf(mark, i + 1)) {
    if (!isExampleOpening(body, i + 1)) hiddenStarts.push(i + 1);
  }
  const closings = closingsOf(body);
  let out = "";
  let from = 0;
  let depth = 0;
  let at = body.indexOf("[");
  while (at >= 0) {
    if (isExampleOpening(body, at)) {
      // Inside an example nothing is a tag. A "[noparse]" whose closing tag
      // lies past the body cannot be told here, and is read as no example:
      // it would take the end of this block along, which hides more.
      const { closing } = readExample(
        at,
        body[at - 1] === mark,
        closings,
        hiddenStarts,
      );
      at = body.indexOf(
        "[",
        closing < 0
          ? at + EXAMPLE_OPENING.length
          : closing + EXAMPLE_CLOSING.length,
      );
      continue;
    }
    if (isPrivateToken(body, at)) {
      depth++;
    } else if (isHiddenClosing(body, at)) {
      if (depth > 0) {
        depth--;
      } else {
        const end = at + HIDDEN_CLOSING_LENGTH;
        out +=
          body.slice(from, at) +
          mark +
          EXAMPLE_OPENING +
          body.slice(at, end) +
          EXAMPLE_CLOSING;
        from = end;
      }
    }
    at = body.indexOf("[", at + 1);
  }
  return out + body.slice(from);
}

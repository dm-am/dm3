// How a tag is written, as the server reads it, and the spelling the editor
// writes it back in.

import {
  NUMBER_PARAMS,
  isPixels,
  TAG_PARAMS,
  type TagName,
} from "./bbcodeTagSet";

/** Why a spelling is not markup, as the markup check names it. */
export type BBCodeBrokenReason =
  | "value-after-name"
  | "unknown-param"
  | "unquoted-value"
  | "repeated-param"
  | "bracket-in-value"
  | "newline-in-value"
  | "unterminated-value"
  | "stray-space"
  | "bad-number"
  | "bad-content"
  | "missing-param"
  | "value-on-closing-tag"
  | "private-spelling";

/** The result of reading an opening tag. */
interface Opening {
  ok: boolean;
  /** A failed spelling ends after its name: an unclosed value must not swallow the text. */
  end: number;
  params: Map<string, string>;
  reason?: BBCodeBrokenReason;
  param?: string;
}

export const isLetter = (code: number): boolean =>
  (code >= 65 && code <= 90) || (code >= 97 && code <= 122);

/**
 * Lowercase by the ASCII codes of A-Z only: case folding differs between .NET
 * and JavaScript (the Kelvin sign), and both sides must read one text alike
 * (SECURITY.md).
 */
export function asciiLower(name: string): string {
  let out = "";
  for (let i = 0; i < name.length; i++) {
    const code = name.charCodeAt(i);
    out += String.fromCharCode(code >= 65 && code <= 90 ? code + 32 : code);
  }
  return out;
}

/** Whether the text at a position spells a name, in any case of A-Z. */
export function spellsName(src: string, at: number, name: string): boolean {
  if (at + name.length > src.length) return false;
  for (let i = 0; i < name.length; i++) {
    let code = src.charCodeAt(at + i);
    if (code >= 65 && code <= 90) code += 32;
    if (code !== name.charCodeAt(i)) return false;
  }
  return true;
}

/**
 * Taken out by the server before reading (BbParser.Normalise): C0 and C1
 * controls but tab and line breaks, and U+202A..U+202E, U+2066..U+2069.
 */
function isRemoved(code: number): boolean {
  return (
    (code <= 0x1f && code !== 0x09 && code !== 0x0a && code !== 0x0d) ||
    (code >= 0x7f && code <= 0x9f) ||
    (code >= 0x202a && code <= 0x202e) ||
    (code >= 0x2066 && code <= 0x2069)
  );
}

const isHighSurrogate = (code: number): boolean =>
  code >= 0xd800 && code <= 0xdbff;
const isLowSurrogate = (code: number): boolean =>
  code >= 0xdc00 && code <= 0xdfff;

/**
 * The text as the server reads it: those characters removed, U+FFFD for a lone
 * surrogate. A removal can join a tag ("[nopa" U+0001 "rse]"), so the editor
 * reads what the server reads.
 */
export function asTheServerReads(text: string): string {
  let out: string[] | null = null;
  let from = 0;
  for (let i = 0; i < text.length; i++) {
    const code = text.charCodeAt(i);
    if (isHighSurrogate(code) && isLowSurrogate(text.charCodeAt(i + 1))) {
      i++;
      continue;
    }
    const removed = isRemoved(code);
    if (!removed && !isHighSurrogate(code) && !isLowSurrogate(code)) continue;
    out ??= [];
    out.push(text.slice(from, i));
    if (!removed) out.push("\uFFFD");
    from = i + 1;
  }
  if (out === null) return text;
  out.push(text.slice(from));
  return out.join("");
}

/**
 * White space as .NET's char.IsWhiteSpace reads it, where a tag name and a
 * trimmed address end. Neither \s nor trim() is this: they count U+FEFF, and
 * ASCII alone misses the no-break and ideographic spaces, either of which made
 * the server and the editor read one tag two ways.
 */
function isWhiteSpace(code: number): boolean {
  return (
    (code >= 0x09 && code <= 0x0d) ||
    code === 0x20 ||
    code === 0x85 ||
    code === 0xa0 ||
    code === 0x1680 ||
    (code >= 0x2000 && code <= 0x200a) ||
    code === 0x2028 ||
    code === 0x2029 ||
    code === 0x202f ||
    code === 0x205f ||
    code === 0x3000
  );
}

export const isSpace = (ch: string | undefined): boolean =>
  ch !== undefined && isWhiteSpace(ch.charCodeAt(0));

/** The text without white space at either end, as the server trims it. */
export function trimWhiteSpace(text: string): string {
  let start = 0;
  let end = text.length;
  while (start < end && isWhiteSpace(text.charCodeAt(start))) start++;
  while (end > start && isWhiteSpace(text.charCodeAt(end - 1))) end--;
  return text.slice(start, end);
}

/** Whether the text holds white space anywhere, as the server reads it. */
export function hasWhiteSpace(text: string): boolean {
  for (let i = 0; i < text.length; i++) {
    if (isWhiteSpace(text.charCodeAt(i))) return true;
  }
  return false;
}

/** For every position, where the first "]" at or after it is, or -1. */
export function closingBrackets(src: string): Int32Array {
  const next = new Int32Array(src.length + 1);
  let found = -1;
  next[src.length] = -1;
  for (let i = src.length - 1; i >= 0; i--) {
    if (src.charCodeAt(i) === 93) found = i;
    next[i] = found;
  }
  return next;
}

/** Where a value after a tag name ([b=1], [code="x"]) ends; null before "[" or a line break. */
function valueAfterNameEnd(src: string, from: number): number | null {
  const quoted = src[from] === '"';
  for (let j = quoted ? from + 1 : from; j < src.length; j++) {
    const ch = src[j];
    if (ch === "[" || ch === "\n" || ch === "\r") return null;
    if (quoted ? ch === '"' && src[j + 1] === "]" : ch === "]") {
      return quoted ? j + 2 : j + 1;
    }
  }
  return null;
}

/** A quoted value read from just past its opening quote, or why it is not one. */
interface QuotedValue {
  value: string;
  /** Past the closing quote and "]", or at the space before the next name. */
  end: number;
  closesTag: boolean;
}

/**
 * Why a value stops at a "[" or a line break: one that runs over it has its
 * closing quote before the next "]", one that never closes does not.
 */
function valueBrokenAt(
  src: string,
  closes: Int32Array,
  at: number,
): BBCodeBrokenReason {
  const close = closes[at];
  const wouldEnd = close > 0 && src[close - 1] === '"';
  if (!wouldEnd) return "unterminated-value";
  return src[at] === "[" ? "bracket-in-value" : "newline-in-value";
}

/** Ends on a quote that "]" or " name=" follows, never on "[" or a line break. */
function readQuotedValue(
  src: string,
  closes: Int32Array,
  start: number,
): QuotedValue | BBCodeBrokenReason {
  for (let j = start; j < src.length; j++) {
    const ch = src[j];
    if (ch === "[" || ch === "\n" || ch === "\r") {
      return valueBrokenAt(src, closes, j);
    }
    if (ch !== '"') continue;
    if (src[j + 1] === "]") {
      return { value: src.slice(start, j), end: j + 2, closesTag: true };
    }
    if (src[j + 1] !== " ") continue;
    let q = j + 1;
    while (src[q] === " ") q++;
    if (src[q] === "]") return "stray-space";
    let r = q;
    while (isLetter(src.charCodeAt(r))) r++;
    if (r > q && src[r] === "=") {
      return { value: src.slice(start, j), end: j + 1, closesTag: false };
    }
  }
  return "unterminated-value";
}

/**
 * Reads the opening of a tag other than [private]: name="value" pairs, Latin
 * names in any case, each once, values in straight quotes ending on a quote
 * that "]" or " name=" follows, never on "[" or a line break. Bounded work per
 * character, nothing read past the next "[".
 */
export function readOpening(
  src: string,
  closes: Int32Array,
  nameEnd: number,
  tag: TagName,
): Opening {
  const params = new Map<string, string>();
  const first = src[nameEnd];

  if (first === "]") return { ok: true, end: nameEnd + 1, params };

  if (first === "=") {
    const end = valueAfterNameEnd(src, nameEnd + 1);
    return {
      ok: false,
      end: end ?? nameEnd,
      params,
      reason: "value-after-name",
    };
  }

  const allowed = TAG_PARAMS[tag] ?? [];
  let semantic: { reason: BBCodeBrokenReason; param: string } | null = null;
  const fail = (reason: BBCodeBrokenReason, param = ""): Opening => ({
    ok: false,
    end: nameEnd,
    params,
    reason: semantic?.reason ?? reason,
    param: semantic?.param ?? param,
  });

  let j = nameEnd;
  for (;;) {
    // The separator: one space or more, and nothing else.
    if (src[j] !== " ") return fail("stray-space");
    while (src[j] === " ") j++;
    if (src[j] === "]") return fail("stray-space");

    const nameStart = j;
    while (isLetter(src.charCodeAt(j))) j++;
    const typed = src.slice(nameStart, j);
    const name = asciiLower(typed);
    if (!name) return fail("unknown-param");
    if (!semantic && !allowed.includes(name)) {
      semantic = { reason: "unknown-param", param: typed };
    } else if (!semantic && params.has(name)) {
      semantic = { reason: "repeated-param", param: typed };
    }

    if (src[j] === " ") return fail("stray-space", typed);
    if (src[j] !== "=") return fail("unquoted-value", typed);
    j++;
    if (src[j] === " ") return fail("stray-space", typed);
    if (src[j] !== '"') return fail("unquoted-value", typed);

    const scan = readQuotedValue(src, closes, j + 1);
    if (typeof scan === "string") return fail(scan, typed);
    const { value } = scan;
    j = scan.end;

    if (!semantic && NUMBER_PARAMS.has(name) && !isPixels(value)) {
      semantic = { reason: "bad-number", param: typed };
    }
    if (!params.has(name)) params.set(name, value);

    if (scan.closesTag) {
      return semantic
        ? {
            ok: false,
            end: j,
            params,
            reason: semantic.reason,
            param: semantic.param,
          }
        : { ok: true, end: j, params };
    }
  }
}

/** The result of reading a [private] opening, in whatever case and shape. */
interface PrivateOpening {
  valid: boolean;
  end: number;
  value: string | null;
}

/**
 * Reads a [private] opening: [private] and [private="names"] are the tag; any
 * other spelling is still a hidden block addressed to nobody.
 */
export function readPrivateOpening(
  src: string,
  closes: Int32Array,
  nameEnd: number,
): PrivateOpening {
  if (src[nameEnd] === "]") {
    return { valid: true, end: nameEnd + 1, value: null };
  }
  if (src[nameEnd] === "=" && src[nameEnd + 1] === '"') {
    for (let j = nameEnd + 2; j < src.length; j++) {
      const ch = src[j];
      if (ch === "[" || ch === "\n" || ch === "\r") break;
      if (ch === '"' && src[j + 1] === "]") {
        return { valid: true, end: j + 2, value: src.slice(nameEnd + 2, j) };
      }
    }
  }
  const close = closes[nameEnd];
  const bracket = src.indexOf("[", nameEnd);
  const end =
    close >= 0 && (bracket < 0 || close < bracket) ? close + 1 : nameEnd;
  return { valid: false, end, value: null };
}

/** Whether "[private" in any case stands at this "[" as a tag name. */
export function isPrivateToken(src: string, at: number): boolean {
  if (!spellsName(src, at + 1, "private")) return false;
  const follow = src[at + 8];
  return (
    follow === undefined || follow === "]" || follow === "=" || isSpace(follow)
  );
}

/**
 * Where the closing tag of a [noparse] stands at or after a position, in any
 * case of its name, or -1.
 */
export function findCloser(src: string, tag: string, from: number): number {
  for (
    let at = src.indexOf("[/", from);
    at >= 0;
    at = src.indexOf("[/", at + 2)
  ) {
    if (spellsName(src, at + 2, tag) && src[at + 2 + tag.length] === "]") {
      return at;
    }
  }
  return -1;
}

/** Markup has no escaping: a value with "[", a line break or a closing quote cannot be written. */
export function isWritableValue(value: string): boolean {
  return !/[[\r\n]|" *\]|" +[A-Za-z]+=/.test(value);
}

/**
 * Whether the addressees of [private] can be written back: that value ends on
 * the first '"]' and nothing else, and holds neither "[" nor a line break.
 */
export function isWritablePrivateValue(value: string): boolean {
  return !/[[\r\n]|"\]/.test(value);
}

/** The opening a tag with these parameters is written with. */
export function writeOpening(
  tag: string,
  params: Array<[string, string | null]>,
): string {
  let out = `[${tag}`;
  for (const [name, value] of params) {
    if (value !== null) out += ` ${name}="${value}"`;
  }
  return `${out}]`;
}

export function writePrivateOpening(value: string | null): string {
  return value === null ? "[private]" : `[private="${value}"]`;
}

export function writeImage(
  src: string,
  width: string | null,
  height: string | null,
): string {
  const opening = writeOpening("img", [
    ["width", width],
    ["height", height],
  ]);
  return `${opening}${src}[/img]`;
}

export function writeLink(href: string, text: string | null): string {
  return `${writeOpening("link", [["text", text]])}${href}[/link]`;
}

export function writeMention(login: string): string {
  return `[mention]${login}[/mention]`;
}

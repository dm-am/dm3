/**
 * The editor's BBCode converter.
 *
 * Four things live here: BBCode to the HTML the visual mode edits
 * (bbcodeToHtml), that HTML back to BBCode (htmlToBbcode), the markup check
 * of the BBCode mode (validateBBCode), and the preview of a tag description
 * (hintToHtml). Readers never get this HTML: the server renders every page
 * (docs/architecture/BBCODE_RENDERING.md), and this module exists for the
 * author's own draft.
 *
 * The markup is read by the grammar the server reads it by, section
 * "Грамматика разметки" of that document, and the table in
 * bbcodeGrammar.cases.ts holds both sides to the same rows. What the draft
 * shows differs from a reader's page in four deliberate ways: typography
 * stays as typed, a hidden block is drawn for its author, a hidden
 * block on a surface that has none stays the text it was typed as (the page
 * erases it for everyone, and the save refuses it with a reason), and an image
 * of the global chat has no spoiler. What it owes is exactness: every input
 * comes back from bbcodeToHtml and htmlToBbcode as it was typed, broken
 * spellings and names in capitals included, so switching modes never rewrites
 * the author's text. The one thing taken out is the characters that reorder
 * text for display, before anything is read, as the server does.
 *
 * @module bbcode
 */

import { DEFAULT_LINK_TEXT } from "./bbcodeConstants";

// ============================================================================
// TAG SETS
// ============================================================================

/**
 * The tag set an editor works with. common: comments and every prose field
 * over them; post: game posts; info: profiles and descriptions; message: the
 * global chat; direct: private messages. They follow the server's surfaces,
 * and direct differs from message by [mod], which private messages do not
 * render. hint: the description of a game tag, whose one tag is [tip] (the
 * server's BbSurface.Hint); it has no visual mode, and its preview is
 * hintToHtml.
 */
export type BBCodeContext =
  | "common"
  | "post"
  | "info"
  | "message"
  | "direct"
  | "hint";

/** The tags of the grammar. */
type TagName =
  | "b"
  | "i"
  | "u"
  | "strike"
  | "code"
  | "spoiler"
  | "nsfw"
  | "ul"
  | "ol"
  | "li"
  | "img"
  | "link"
  | "private"
  | "quote"
  | "tab"
  | "noparse"
  | "mod"
  | "mention"
  | "tip";

/** Tags available in every context but the hint. */
const BASE_TAGS: readonly TagName[] = [
  "b",
  "i",
  "strike",
  "u",
  "code",
  "spoiler",
  "nsfw",
  "ul",
  "ol",
  "li",
  "img",
  "link",
  "quote",
  "tab",
  "noparse",
  "mention",
];

/**
 * The tags each context reads as markup; anything else stays text. The hint
 * reads [tip] and, as every surface, [noparse], which shows an example as
 * typed; none of the tags of a post is markup there.
 */
export const CONTEXT_TAGS: Record<BBCodeContext, readonly string[]> = {
  common: [...BASE_TAGS, "mod"],
  post: [...BASE_TAGS, "private"],
  info: [...BASE_TAGS],
  message: [...BASE_TAGS, "mod"],
  direct: [...BASE_TAGS],
  hint: ["tip", "noparse"],
};

/**
 * Every tag of the surfaces of content: the markup check and a caller with no
 * context. [tip] is the hint's alone.
 */
const ALL_TAGS: ReadonlySet<string> = new Set([...BASE_TAGS, "mod", "private"]);

/**
 * The parameters a tag takes. A tag missing here takes none. An image has no
 * alt: a reader never sees it, and text nobody sees on the page is where an
 * insult goes unnoticed (BBCODE_RENDERING.md, "Набор тегов").
 */
const TAG_PARAMS: Partial<Record<TagName, readonly string[]>> = {
  spoiler: ["text"],
  img: ["width", "height"],
  link: ["text"],
  tip: ["text", "img"],
};

/** Parameters whose value is a size in pixels. */
const NUMBER_PARAMS: ReadonlySet<string> = new Set(["width", "height"]);

/** A size in pixels: a whole number from 1 to 9999, written without zeros ahead. */
const PIXELS = /^[1-9]\d{0,3}$/;

/** The tags drawn as a block of their own in the visual mode. */
const BLOCK_TAGS: ReadonlySet<string> = new Set([
  "spoiler",
  "nsfw",
  "quote",
  "mod",
  "ul",
  "ol",
  "li",
]);

/**
 * How deep tags may nest. The server refuses a deeper tree and shows the text
 * as typed (BbParser.TreeMaxDepth); the visual mode does the same.
 */
const MAX_DEPTH = 512;

// ============================================================================
// THE PARSED TEXT
// ============================================================================

/** A tag that holds other nodes and has nothing of its own but its caption. */
type ContainerTag =
  | "b"
  | "i"
  | "u"
  | "strike"
  | "code"
  | "spoiler"
  | "nsfw"
  | "quote"
  | "mod"
  | "ul"
  | "ol"
  | "li";

/**
 * What a text is made of once read. A spelling that is not markup is text.
 * open and close are a tag as typed, which the way back returns when it
 * differs from how the editor writes that tag.
 */
type BbNode =
  | { t: "text"; s: string }
  | { t: "tab"; open: string }
  | {
      t: "verbatim";
      tag: "noparse";
      s: string;
      open: string;
      close: string;
    }
  | {
      t: "img";
      src: string;
      width: string | null;
      height: string | null;
      source: string;
    }
  | { t: "link"; href: string; text: string | null; source: string }
  | { t: "mention"; login: string; source: string }
  | {
      t: "tip";
      /** The word, as typed. */
      word: string;
      /** The text it shows, as typed, or null for none. */
      text: string | null;
      /** The address of the picture it shows, trimmed, or null for none. */
      img: string | null;
      source: string;
    }
  | {
      t: "private";
      value: string | null;
      open: string;
      /** null for a block its closing tag never ended. */
      close: string | null;
      children: BbNode[];
    }
  | {
      t: "tag";
      tag: ContainerTag;
      caption: string | null;
      open: string;
      close: string;
      children: BbNode[];
    };

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

/**
 * Everything the markup check reports: a broken spelling, an address the page
 * refuses, a loose tag, a hidden block inside another, or a hidden block on a
 * surface that has none. The list, and the words for each, are
 * BBCODE_RENDERING.md, section "Предупреждения редактора".
 */
export type BBCodeIssueReason =
  | BBCodeBrokenReason
  | "bad-address"
  | "private-nested"
  | "unclosed"
  | "stray-closing"
  | "too-deep"
  | "private-outside-game";

/** One finding of the markup check. */
export interface BBCodeIssue {
  reason: BBCodeIssueReason;
  /** The line of the text it is on, from 1. */
  line: number;
  /** What is wrong and how to fix it, one sentence without the line. */
  text: string;
  /** What the author reads: the line, then the text. */
  message: string;
}

/**
 * Which of the texts of one reason a finding gets, when the reason has more
 * than one.
 */
type IssueCase =
  /** private-spelling: a closing tag that carries a value. */
  | "closing-value"
  /** private-spelling: a block its closing tag never ended. */
  | "unclosed"
  /** private-spelling: a token after a [noparse] left open. */
  | "after-left-open"
  /** bad-address: an address on neither http nor https. */
  | "scheme"
  /** bad-content: a [tip] inside another. */
  | "nested"
  /** stray-closing: the open tag of that name is outside a hidden block. */
  | "sealed";

/** A finding as the reader records it, before it is worded. */
interface RawIssue {
  reason: BBCodeIssueReason;
  at: number;
  tag?: string;
  /** The name of a parameter, as typed. */
  param?: string;
  /** A tag as typed. */
  typed?: string;
  case?: IssueCase;
}

/** A tag the reader has entered and not left yet. */
interface Frame {
  /** null for the text itself. */
  tag: TagName | null;
  /** The opening matched the grammar. */
  valid: boolean;
  /** The opening as typed, which a broken tag prints. */
  open: string;
  params: Map<string, string>;
  children: BbNode[];
  at: number;
  /** [private] only: the addressees, null for none. */
  value: string | null;
  /**
   * The first tag the reader met inside this one, whatever became of it: the
   * server's parser made a node of it, and a [tip] holding a node is the tag
   * as typed there, even when the node itself prints as typed.
   */
  inner?: string;
}

/** The result of reading an opening tag. */
interface Opening {
  ok: boolean;
  /**
   * Where the tag ends. A spelling that fails its syntax ends right after its
   * name, and what follows is read again as text: a value that did not close
   * must not carry the rest of the text into the tag.
   */
  end: number;
  params: Map<string, string>;
  reason?: BBCodeBrokenReason;
  param?: string;
}

class TooDeep extends Error {}

const isLetter = (code: number): boolean =>
  (code >= 65 && code <= 90) || (code >= 97 && code <= 122);

/**
 * A name in small letters, by the ASCII codes of A-Z and nothing else.
 *
 * Names of tags and parameters compare in any case, and only the Latin A-Z
 * fold: the case folding of a language is not the same on both sides (.NET
 * folds the Kelvin sign U+212A into k, a JavaScript /i without u does not), and
 * the server and the editor would then read one text two ways
 * (docs/conventions/SECURITY.md). A name is read from Latin letters only, so
 * folding them is all a name ever needs.
 */
function asciiLower(name: string): string {
  let out = "";
  for (let i = 0; i < name.length; i++) {
    const code = name.charCodeAt(i);
    out += String.fromCharCode(code >= 65 && code <= 90 ? code + 32 : code);
  }
  return out;
}

/** Whether the text at a position spells a name, in any case of A-Z. */
function spellsName(src: string, at: number, name: string): boolean {
  if (at + name.length > src.length) return false;
  for (let i = 0; i < name.length; i++) {
    let code = src.charCodeAt(at + i);
    if (code >= 65 && code <= 90) code += 32;
    if (code !== name.charCodeAt(i)) return false;
  }
  return true;
}

/**
 * Whether the server takes the character out of a text before it reads it
 * (BbParser.Normalise, ParserInput): a C0 or C1 control character other than
 * tab, line feed and carriage return, or one that reorders text for display,
 * U+202A..U+202E and U+2066..U+2069.
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
 * The text as the server reads it: without the characters it removes, and
 * with U+FFFD for a surrogate that has no partner.
 *
 * Removing a character joins what stood on either side of it, and the join
 * can be a tag: "[nopa" U+0001 "rse]" is an example to the server, and an
 * editor that read the stored text would draw a hidden block the page prints
 * as an example to the whole room. A reordering character in the middle of a name
 * would split it for one side and not for the other. So the editor reads the
 * same text the server does, and the way back returns it without them.
 */
function asTheServerReads(text: string): string {
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
 * White space as the server reads it (char.IsWhiteSpace in .NET): the
 * characters Unicode gives the White_Space property.
 *
 * A tag name ends at one of them, and so does the text of an address or a
 * login when it is trimmed. JavaScript has two answers of its own and neither
 * is this one. A space after "[" and a name used to be only the ASCII ones
 * here, so "[quote" and an ideographic space was a broken [quote] to the
 * server and text to the editor, and a "[/quote]" further on closed a
 * different tag on each side; "[private" and a no-break space was a hidden
 * block to the server and text to the editor, which then drew it to its author
 * as nothing hidden. \s and trim() count U+FEFF as well, which the server reads
 * as a letter: "\uFEFFhttps://..." was a link in the preview and a refused
 * address on the page.
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

const isSpace = (ch: string | undefined): boolean =>
  ch !== undefined && isWhiteSpace(ch.charCodeAt(0));

/** The text without white space at either end, as the server trims it. */
function trimWhiteSpace(text: string): string {
  let start = 0;
  let end = text.length;
  while (start < end && isWhiteSpace(text.charCodeAt(start))) start++;
  while (end > start && isWhiteSpace(text.charCodeAt(end - 1))) end--;
  return text.slice(start, end);
}

/** Whether the text holds white space anywhere, as the server reads it. */
function hasWhiteSpace(text: string): boolean {
  for (let i = 0; i < text.length; i++) {
    if (isWhiteSpace(text.charCodeAt(i))) return true;
  }
  return false;
}

/** For every position, where the first "]" at or after it is, or -1. */
function closingBrackets(src: string): Int32Array {
  const next = new Int32Array(src.length + 1);
  let found = -1;
  next[src.length] = -1;
  for (let i = src.length - 1; i >= 0; i--) {
    if (src.charCodeAt(i) === 93) found = i;
    next[i] = found;
  }
  return next;
}

/**
 * Read the value written right after a tag name, as in [b=1] or [code="x"],
 * to find where that spelling ends. Null when it does not end before the next
 * "[" or line break.
 */
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

/**
 * Read the opening of a tag other than [private], from right after its name.
 *
 * A parameter is name="value": the name in Latin letters of any case, the
 * value in straight double quotes, pairs apart by spaces, in any order and
 * each once, width and WIDTH being the same name. A failure names the
 * parameter as it was typed.
 * The value ends on a quote that "]" follows, or spaces and the next name=;
 * any other quote belongs to it. "[" and a line break never do. Every
 * character is looked at a bounded number of times, and nothing is read past
 * the next "[".
 */
function readOpening(
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

    const valueStart = ++j;
    let value: string | null = null;
    let closesTag = false;
    while (value === null) {
      if (j >= src.length) return fail("unterminated-value", typed);
      const ch = src[j];
      if (ch === "[" || ch === "\n" || ch === "\r") {
        // Tell a value that runs over one of them from one that never closes:
        // the first has its closing quote before the next "]".
        const close = closes[j];
        const wouldEnd = close > 0 && src[close - 1] === '"';
        if (!wouldEnd) return fail("unterminated-value", typed);
        return fail(
          ch === "[" ? "bracket-in-value" : "newline-in-value",
          typed,
        );
      }
      if (ch === '"') {
        if (src[j + 1] === "]") {
          value = src.slice(valueStart, j);
          j += 2;
          closesTag = true;
          break;
        }
        if (src[j + 1] === " ") {
          let q = j + 1;
          while (src[q] === " ") q++;
          if (src[q] === "]") return fail("stray-space", typed);
          let r = q;
          while (isLetter(src.charCodeAt(r))) r++;
          if (r > q && src[r] === "=") {
            value = src.slice(valueStart, j);
            j += 1;
            break;
          }
        }
      }
      j++;
    }

    if (!semantic && NUMBER_PARAMS.has(name) && !PIXELS.test(value)) {
      semantic = { reason: "bad-number", param: typed };
    }
    if (!params.has(name)) params.set(name, value);

    if (closesTag) {
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
 * Read a [private] opening, whose name is known to be "private" in some case.
 * Two spellings are the tag: [private] and [private="names"], whose value ends
 * on the first '"]' and holds neither "[" nor a line break. Every other
 * spelling is still a hidden block, addressed to nobody: its opening reaches
 * the first "]" when that comes before the next "[", and ends right after the
 * name otherwise.
 */
function readPrivateOpening(
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
function isPrivateToken(src: string, at: number): boolean {
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
function findCloser(src: string, tag: string, from: number): number {
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

/** A tag as the reader meets it: "[", maybe "/", a name, and what follows. */
interface Token {
  at: number;
  closing: boolean;
  /** The name in small letters, whatever case it was typed in. */
  lower: string;
  nameEnd: number;
  follow: string | undefined;
}

/**
 * The reader: one pass over the text, one stack of open tags.
 *
 * A name is a tag only when "]", "=", a space or the end of the text follows
 * it; otherwise it is a word in prose. Names compare in any case of A-Z
 * (asciiLower), and a tag keeps the spelling it was typed with, which the way
 * back returns.
 *
 * A tag is markup only once its own closing tag ends it. A tag left open
 * prints its opening as typed and what it holds as it is, and a closing tag
 * with nothing to close is text as typed. [code] is a tag like [b]: it changes
 * the font and nothing else. [noparse] follows the same rule: with its closing
 * tag ahead it is an example, without it it is a tag left open, and what
 * follows it is read like any other text. The one exception is [private]: left
 * open, it stays a hidden block to the end of the text, because a doubt about
 * hiding is decided towards hiding more.
 */
class Reader {
  private readonly closes: Int32Array;
  private readonly stack: Frame[];
  private pos = 0;
  /** The last search for the closing tag of an example. */
  private closer: { from: number; at: number } | null = null;
  /**
   * Where the first [noparse] of the text left without its closing tag
   * stands, or null. Every token of a hidden block after it, opening or
   * closing, is refused on save, to the end of the text and not only to the
   * end of the tag it stands in: the save path reads no tag but these two
   * (PrivateBlockMarkup), so where that tag ends is not known to it, and
   * refusing too much is the safe way to be wrong.
   */
  private leftOpen: number | null = null;
  /** The first token of a hidden block has been named as off its surface. */
  private outsideNamed = false;

  /**
   * @param privateOutside The text is for a surface that has no hidden block,
   *   and the first of its tokens is named as such: the save path of that
   *   surface refuses the text on it (PrivateBlockMarkup.DescribeSurfaceRefusal).
   */
  constructor(
    private readonly src: string,
    private readonly tags: ReadonlySet<string>,
    private readonly issues: RawIssue[] | null,
    private readonly privateOutside = false,
  ) {
    this.closes = closingBrackets(src);
    this.stack = [
      {
        tag: null,
        valid: true,
        open: "",
        params: new Map(),
        children: [],
        at: 0,
        value: null,
      },
    ];
  }

  read(): BbNode[] {
    const src = this.src;
    let scan = 0;
    for (;;) {
      const at = src.indexOf("[", scan);
      if (at < 0) break;
      const token = this.token(at);
      const next = token ? this.take(token) : -1;
      scan = next < 0 ? at + 1 : next;
    }
    this.flush(src.length);
    while (this.stack.length > 1) this.leaveUnclosed();
    return this.stack[0].children;
  }

  private report(issue: RawIssue): void {
    this.issues?.push(issue);
  }

  private get top(): Frame {
    return this.stack[this.stack.length - 1];
  }

  private token(at: number): Token | null {
    const src = this.src;
    let i = at + 1;
    const closing = src[i] === "/";
    if (closing) i++;
    const nameStart = i;
    while (isLetter(src.charCodeAt(i))) i++;
    if (i === nameStart) return null;
    const follow = src[i];
    if (
      !(
        follow === undefined ||
        follow === "]" ||
        follow === "=" ||
        isSpace(follow)
      )
    ) {
      return null;
    }
    const lower = asciiLower(src.slice(nameStart, i));
    return { at, closing, lower, nameEnd: i, follow };
  }

  /** Act on a token. Returns where to read on, or -1 when it is text. */
  private take(token: Token): number {
    const { lower } = token;
    if (!this.tags.has(lower)) return -1;
    if (lower === "private") {
      // Any token counts, opening or closing, well spelled or not, as it
      // does on the save path: one is enough for the refusal, so the first
      // is named and the rest are not.
      if (this.privateOutside && !this.outsideNamed) {
        this.outsideNamed = true;
        this.report({ reason: "private-outside-game", at: token.at });
      }
      return token.closing ? this.closePrivate(token) : this.openPrivate(token);
    }
    return token.closing ? this.closeTag(token) : this.openTag(token);
  }

  private openTag(token: Token): number {
    const tag = token.lower as TagName;
    const { at, nameEnd } = token;
    // No "]" anywhere ahead: nothing was written as a tag here.
    if (this.closes[nameEnd] < 0) return -1;

    if (tag === "noparse" && token.follow === "]") {
      const end = this.closerOf(nameEnd + 1);
      if (end >= 0) return this.verbatim(at, nameEnd + 1, end);
    }

    const opening = readOpening(this.src, this.closes, nameEnd, tag);
    if (!opening.ok) {
      this.report({
        reason: opening.reason ?? "unknown-param",
        at,
        tag,
        param: opening.param,
      });
    }

    if (tag === "tab") {
      if (!opening.ok) return -1;
      this.flush(at);
      this.top.inner ??= tag;
      this.top.children.push({
        t: "tab",
        open: this.src.slice(at, opening.end),
      });
      this.pos = opening.end;
      return opening.end;
    }

    this.flush(at);
    this.enter({
      tag,
      valid: opening.ok,
      open: this.src.slice(at, opening.end),
      params: opening.params,
      children: [],
      at,
      value: null,
    });
    // A [noparse] read this far has no closing tag ahead.
    if (opening.ok && tag === "noparse") this.leftOpen ??= at;
    this.pos = opening.end;
    return opening.end;
  }

  private openPrivate(token: Token): number {
    const { at, nameEnd } = token;
    const opening = readPrivateOpening(this.src, this.closes, nameEnd);
    const typed = this.src.slice(at, opening.end);
    if (!opening.valid) {
      this.report({ reason: "private-spelling", at, typed });
    }
    // After a [noparse] left open the block is hidden like any other, and the
    // save refuses it all the same: whoever wrote it almost always meant an
    // example and forgot the closing tag of the example.
    this.reportAfterLeftOpen(at, typed);
    // The save refuses a block inside another: whoever wrote it almost always
    // forgot to close the first one.
    if (this.stack.some((frame) => frame.tag === "private")) {
      this.report({ reason: "private-nested", at, typed });
    }
    this.flush(at);
    this.enter({
      tag: "private",
      valid: opening.valid,
      open: this.src.slice(at, opening.end),
      params: new Map(),
      children: [],
      at,
      value: opening.value,
    });
    this.pos = opening.end;
    return opening.end;
  }

  private closePrivate(token: Token): number {
    if (token.follow === "]") {
      // The closing tag of a block opened before a [noparse] left open is
      // refused as well: one the author wrote as part of an example would end
      // the block early and show the rest. A block opened after it was named
      // at its opening already.
      const end = token.nameEnd + 1;
      let depth = this.stack.length - 1;
      while (depth > 0 && this.stack[depth].tag !== "private") depth--;
      if (
        this.leftOpen !== null &&
        depth > 0 &&
        this.stack[depth].at < this.leftOpen
      ) {
        this.reportAfterLeftOpen(token.at, this.src.slice(token.at, end));
      }
      return this.close("private", token.at, end);
    }
    // A closing tag with a value does not end the block. It is named as far as
    // its "]" when one comes before the next "[", and by its name otherwise.
    const bracket = this.closes[token.nameEnd];
    const next = this.src.indexOf("[", token.nameEnd);
    const typedEnd =
      bracket >= 0 && (next < 0 || bracket < next)
        ? bracket + 1
        : token.nameEnd;
    this.report({
      reason: "private-spelling",
      at: token.at,
      typed: this.src.slice(token.at, typedEnd),
      case: "closing-value",
    });
    return -1;
  }

  private closeTag(token: Token): number {
    if (token.follow === "]") {
      return this.close(token.lower, token.at, token.nameEnd + 1);
    }
    if (token.follow === "=" && this.closes[token.nameEnd] >= 0) {
      this.report({
        reason: "value-on-closing-tag",
        at: token.at,
        tag: token.lower,
      });
    }
    return -1;
  }

  /**
   * A [noparse] with its closing tag ahead is an example: it shows what it
   * holds as typed, nothing opens inside it, and only its own closing tag ends
   * it.
   */
  private verbatim(at: number, openEnd: number, end: number): number {
    const src = this.src;
    this.flush(at);
    const closeEnd = end + "[/noparse]".length;
    this.top.inner ??= "noparse";
    this.top.children.push({
      t: "verbatim",
      tag: "noparse",
      s: src.slice(openEnd, end),
      open: src.slice(at, openEnd),
      close: src.slice(end, closeEnd),
    });
    this.pos = closeEnd;
    return closeEnd;
  }

  /**
   * Where the closing tag of an example stands, from a position on, or -1.
   * The reader only moves forward, so a later search finds the tag the last
   * one found while it has not passed it, and nothing when the last one found
   * nothing: the text is looked through once, however many openings are left
   * without a closing tag.
   */
  private closerOf(from: number): number {
    const last = this.closer;
    if (last && from >= last.from && (last.at < 0 || from <= last.at)) {
      return last.at;
    }
    const at = findCloser(this.src, "noparse", from);
    this.closer = { from, at };
    return at;
  }

  private enter(frame: Frame): void {
    if (this.stack.length > MAX_DEPTH) throw new TooDeep();
    this.top.inner ??= frame.tag ?? undefined;
    this.stack.push(frame);
  }

  /** Name a token of a hidden block written after a [noparse] left open. */
  private reportAfterLeftOpen(at: number, typed: string): void {
    if (this.leftOpen !== null) {
      this.report({
        reason: "private-spelling",
        at,
        typed,
        case: "after-left-open",
      });
    }
  }

  /** Move the text between the last token and this one into the open tag. */
  private flush(to: number): void {
    if (to > this.pos)
      pushText(this.top.children, this.src.slice(this.pos, to));
    this.pos = Math.max(this.pos, to);
  }

  /**
   * A closing tag closes the nearest open tag of its name and whatever was
   * opened inside it, which stays unclosed. It never reaches past a [private]:
   * only [/private] ends a hidden block. A closing tag with nothing to close
   * is text, as typed.
   */
  private close(tag: string, at: number, end: number): number {
    let depth = this.stack.length - 1;
    let sealed = false;
    while (depth > 0) {
      const frame = this.stack[depth];
      if (frame.tag === tag) break;
      if (frame.tag === "private") {
        // The tag it would close is outside the hidden block it stands in.
        sealed = this.stack.slice(1, depth).some((outer) => outer.tag === tag);
        depth = 0;
        break;
      }
      depth--;
    }
    if (depth === 0) {
      this.report({
        reason: "stray-closing",
        at,
        tag,
        case: sealed ? "sealed" : undefined,
      });
      return -1;
    }
    this.flush(at);
    while (this.stack.length - 1 > depth) this.leaveUnclosed();
    this.leave(this.src.slice(at, end));
    this.pos = end;
    return end;
  }

  /**
   * Leave a tag that its closing tag never ended: its opening is text as
   * typed, and what it holds stays as it is. A hidden block stays hidden.
   */
  private leaveUnclosed(): void {
    const frame = this.stack.pop() as Frame;
    const into = this.top.children;
    if (frame.tag === "private") {
      this.report({
        reason: "private-spelling",
        at: frame.at,
        case: "unclosed",
      });
      into.push({
        t: "private",
        value: frame.value,
        open: frame.open,
        close: null,
        children: frame.children,
      });
      return;
    }
    if (frame.valid && frame.tag) {
      this.report({ reason: "unclosed", at: frame.at, tag: frame.tag });
    }
    flatten(into, frame.open, frame.children, "");
  }

  /** Leave a tag at its own closing tag. */
  private leave(close: string): void {
    const frame = this.stack.pop() as Frame;
    const into = this.top.children;
    const tag = frame.tag as TagName;

    if (tag === "private") {
      into.push({
        t: "private",
        value: frame.value,
        open: frame.open,
        close,
        children: frame.children,
      });
      return;
    }
    if (!frame.valid) {
      flatten(into, frame.open, frame.children, close);
      return;
    }

    if (tag === "tip") {
      this.leaveTip(frame, close, into);
      return;
    }

    if (tag === "img" || tag === "link" || tag === "mention") {
      const leaf = this.leaf(frame, close);
      if (leaf === "refused") {
        // The server refuses the address and prints the tag as typed, the
        // address in its place: the author and the reader see what did not
        // work instead of an empty place, and the check says why.
        const address = trimWhiteSpace(contentOf(frame)).toLowerCase();
        this.report({
          reason: "bad-address",
          at: frame.at,
          tag,
          case:
            address.startsWith("http://") || address.startsWith("https://")
              ? undefined
              : "scheme",
        });
        flatten(into, frame.open, frame.children, close);
      } else if (leaf) {
        into.push(leaf);
      } else {
        this.report({ reason: "bad-content", at: frame.at, tag });
        flatten(into, frame.open, frame.children, close);
      }
      return;
    }

    into.push({
      t: "tag",
      tag: tag as ContainerTag,
      caption: frame.params.get("text") ?? null,
      open: frame.open,
      close,
      children: frame.children,
    });
  }

  /**
   * A [tip] holds a word on one line and shows a text, a picture or both: the
   * rule of the server's TipTag. Anything else is the tag as typed, and the
   * check says why: a tag in the word (another [tip] above all), a line break
   * in it, an address the page refuses, or nothing to show. The server asks in
   * this order: the word first, as a content tag, then the parameters.
   */
  private leaveTip(frame: Frame, close: string, into: BbNode[]): void {
    const refuse = (issue: Omit<RawIssue, "at" | "tag">): void => {
      this.report({ ...issue, at: frame.at, tag: "tip" });
      flatten(into, frame.open, frame.children, close);
    };

    const word = contentOf(frame);
    if (
      frame.inner !== undefined ||
      !trimWhiteSpace(word) ||
      /[\r\n]/.test(word)
    ) {
      refuse({
        reason: "bad-content",
        case: frame.inner === "tip" ? "nested" : undefined,
      });
      return;
    }

    const written = frame.params.get("text");
    const text =
      written !== undefined && trimWhiteSpace(written) ? written : null;
    const address = frame.params.get("img");
    const img = address === undefined ? null : tipImageAddress(address);
    if (address !== undefined && img === null) {
      const lower = trimWhiteSpace(address).toLowerCase();
      refuse({
        reason: "bad-address",
        case: /^(https?:\/\/|\/)/.test(lower) ? undefined : "scheme",
      });
      return;
    }
    if (text === null && img === null) {
      refuse({ reason: "missing-param" });
      return;
    }
    into.push({
      t: "tip",
      word,
      text,
      img,
      source: frame.open + word + close,
    });
  }

  /**
   * An image, a link or a mention holds one address or one login and nothing
   * else. Null when it holds something else, which leaves the tag broken;
   * "refused" for an address the server does not show.
   */
  private leaf(frame: Frame, close: string): BbNode | "refused" | null {
    const only = frame.children;
    if (only.length > 1 || (only.length === 1 && only[0].t !== "text")) {
      return null;
    }
    const content = contentOf(frame);
    const source = frame.open + content + close;
    const value = trimWhiteSpace(content);

    if (frame.tag === "mention") {
      if (!value || /[[\r\n]/.test(value)) return null;
      return { t: "mention", login: value, source };
    }

    if (!value || hasWhiteSpace(value)) return null;
    if (!isSafeAddress(value)) return "refused";
    if (frame.tag === "link") {
      return {
        t: "link",
        href: value,
        text: frame.params.get("text") ?? null,
        source,
      };
    }
    return {
      t: "img",
      src: value,
      width: frame.params.get("width") ?? null,
      height: frame.params.get("height") ?? null,
      source,
    };
  }
}

/** The text a tag holds when it holds nothing but text, and "" otherwise. */
function contentOf(frame: Frame): string {
  const only = frame.children;
  return only.length === 1 && only[0].t === "text" ? only[0].s : "";
}

/** Append text, joined to the text before it. */
function pushText(into: BbNode[], s: string): void {
  if (!s) return;
  const last = into[into.length - 1];
  if (last && last.t === "text") last.s += s;
  else into.push({ t: "text", s });
}

/** A tag that is not markup: its own spellings as text, its content as it is. */
function flatten(
  into: BbNode[],
  open: string,
  children: BbNode[],
  close: string,
): void {
  pushText(into, open);
  for (const child of children) {
    if (child.t === "text") pushText(into, child.s);
    else into.push(child);
  }
  pushText(into, close);
}

/** Read a text by a tag set. A tree deeper than the server takes is text. */
function readText(
  bbcode: string,
  tags: ReadonlySet<string>,
  issues: RawIssue[] | null,
  privateOutside = false,
): BbNode[] {
  try {
    return new Reader(bbcode, tags, issues, privateOutside).read();
  } catch (error) {
    if (!(error instanceof TooDeep)) throw error;
    issues?.push({ reason: "too-deep", at: 0 });
    return [{ t: "text", s: bbcode }];
  }
}

// ============================================================================
// ADDRESSES
// ============================================================================

/**
 * Whether the server shows an image or a link at this address.
 *
 * The same rule as BbAddress.Accept on the server
 * (DM.Infrastructure.Core/Parsing/Tags/BbAddress.cs): http or https only, no
 * space or control character anywhere, and no host that names the reader's own
 * machine or network. The server does not fetch these addresses, the reader's
 * browser does, which is why a post must not point it at its own network.
 */
export function isSafeAddress(url: string): boolean {
  const trimmed = trimWhiteSpace(url);
  if (!trimmed) return false;
  if (hasSpaceOrControl(trimmed)) return false;
  const lower = trimmed.toLowerCase();
  if (!lower.startsWith("http://") && !lower.startsWith("https://")) {
    return false;
  }
  if (!writtenAsTheServerReadsIt(trimmed)) return false;
  let host: string;
  try {
    host = new URL(trimmed).hostname.toLowerCase();
  } catch {
    return false;
  }
  return !isBlockedHost(host);
}

/**
 * The picture of a [tip], trimmed, or null when the server refuses it: an
 * address isSafeAddress takes, or a path on this site written with one slash,
 * "/images/a.gif". The same rule as BbAddress.ReadTipImage on the server: the
 * path takes no second slash after the first and no backslash anywhere, which
 * a browser reads as another host, and neither white space nor a control
 * character, which no address carries.
 */
function tipImageAddress(value: string): string | null {
  const trimmed = trimWhiteSpace(value);
  const sitePath =
    trimmed.length > 1 &&
    trimmed[0] === "/" &&
    trimmed[1] !== "/" &&
    !trimmed.includes("\\") &&
    !hasSpaceOrControl(trimmed);
  return sitePath || isSafeAddress(value) ? trimmed : null;
}

/** Whether the page shows the picture of a [tip] from this address. */
export function isTipImageAddress(value: string): boolean {
  return tipImageAddress(value) !== null;
}

/**
 * Whether the address is written in a shape the server's parser of addresses
 * (Uri in .NET) takes. The browser's URL reads more shapes than it does, each
 * its own way, and there the preview drew a link where the page prints the
 * tag as typed: a backslash before the path, which the browser reads as a
 * slash, so that "https://a.com\@b" is a.com to it; nothing between "//" and
 * the path, which it skips, so that "https:///a.com/" is a.com; a percent sign
 * in the host, which it decodes; and an empty label, "a..b.com". The server
 * refuses all four, and so does the editor. A backslash in the path the server
 * takes.
 */
function writtenAsTheServerReadsIt(address: string): boolean {
  const rest = address.slice(address.indexOf("//") + 2);
  const end = rest.search(/[/?#\\]/);
  if (end >= 0 && rest[end] === "\\") return false;
  const authority = end < 0 ? rest : rest.slice(0, end);
  const host = authority.slice(authority.lastIndexOf("@") + 1);
  return host.length > 0 && !host.includes("%") && !host.includes("..");
}

/**
 * A space or a control character, which no address carries (RFC 3986): the
 * C0 and C1 controls and DEL by their codes, and white space as the server
 * reads it (isWhiteSpace).
 */
function hasSpaceOrControl(text: string): boolean {
  for (let index = 0; index < text.length; index++) {
    const code = text.charCodeAt(index);
    if (code <= 0x1f || (code >= 0x7f && code <= 0x9f)) return true;
  }
  return hasWhiteSpace(text);
}

/** The loopback, private and link-local ranges, as the server lists them. */
function isBlockedIPv4(octets: number[]): boolean {
  const [a, b] = octets;
  return (
    a === 0 ||
    a === 10 ||
    a === 127 ||
    (a === 100 && b >= 64 && b <= 127) ||
    (a === 169 && b === 254) ||
    (a === 172 && b >= 16 && b <= 31) ||
    (a === 192 && b === 168)
  );
}

/** Eight groups of an IPv6 literal, or null when it is not one. */
function ipv6Groups(literal: string): number[] | null {
  let text = literal;
  const tail: number[] = [];
  const dotted = /(\d+)\.(\d+)\.(\d+)\.(\d+)$/.exec(text);
  if (dotted) {
    const [a, b, c, d] = dotted.slice(1).map(Number);
    tail.push((a << 8) | b, (c << 8) | d);
    text = text.slice(0, dotted.index) + "0";
    text = text.replace(/:0$/, ":");
  }
  const halves = text.split("::");
  if (halves.length > 2) return null;
  const parse = (part: string): number[] | null => {
    if (!part) return [];
    const groups = part.split(":").filter((group) => group !== "");
    const values = groups.map((group) => parseInt(group, 16));
    return values.some((value) => Number.isNaN(value) || value > 0xffff)
      ? null
      : values;
  };
  const head = parse(halves[0]);
  const rest = halves.length === 2 ? parse(halves[1]) : [];
  if (!head || !rest) return null;
  const known = head.length + rest.length + tail.length;
  if (known > 8 || (halves.length === 1 && known !== 8)) return null;
  const zeros = new Array<number>(8 - known).fill(0);
  return [...head, ...zeros, ...rest, ...tail];
}

function isBlockedHost(hostname: string): boolean {
  const host = hostname.replace(/\.$/, "");
  if (host === "localhost" || host.endsWith(".localhost")) return true;

  const v4 = /^(\d+)\.(\d+)\.(\d+)\.(\d+)$/.exec(host);
  if (v4) return isBlockedIPv4(v4.slice(1).map(Number));

  if (!host.startsWith("[") || !host.endsWith("]")) return false;
  const groups = ipv6Groups(host.slice(1, -1));
  if (!groups) return true;
  const [g0, , , , , g5, g6, g7] = groups;
  const allZeroUpTo = (n: number) => groups.slice(0, n).every((g) => g === 0);
  if (allZeroUpTo(5) && g5 === 0xffff) {
    return isBlockedIPv4([g6 >> 8, g6 & 0xff, g7 >> 8, g7 & 0xff]);
  }
  if (allZeroUpTo(7) && (g7 === 0 || g7 === 1)) return true;
  return (
    (g0 & 0xffc0) === 0xfe80 ||
    (g0 & 0xffc0) === 0xfec0 ||
    (g0 & 0xfe00) === 0xfc00
  );
}

/**
 * The address a link may point at, or "#" when the server would refuse it.
 *
 * @example
 * sanitizeUrl("javascript:alert(1)") // => "#"
 * sanitizeUrl(" https://example.com/ ")    // => "https://example.com/"
 */
export function sanitizeUrl(url: string): string {
  return isSafeAddress(url ?? "") ? trimWhiteSpace(url) : "#";
}

/** The address an image is drawn from, or "#": the same rule as a link's. */
export function sanitizeImageUrl(url: string): string {
  return sanitizeUrl(url);
}

// ============================================================================
// WRITING VALUES
// ============================================================================

/**
 * Whether a parameter value can be written back: markup has no escaping, so a
 * value holding "[", a line break, or a quote that "]" or " name=" follows
 * would end sooner than it was meant to.
 */
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
function writeOpening(
  tag: string,
  params: Array<[string, string | null]>,
): string {
  let out = `[${tag}`;
  for (const [name, value] of params) {
    if (value !== null) out += ` ${name}="${value}"`;
  }
  return `${out}]`;
}

function writePrivateOpening(value: string | null): string {
  return value === null ? "[private]" : `[private="${value}"]`;
}

function writeImage(
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

function writeLink(href: string, text: string | null): string {
  return `${writeOpening("link", [["text", text]])}${href}[/link]`;
}

function writeMention(login: string): string {
  return `[mention]${login}[/mention]`;
}

// ============================================================================
// BBCODE -> HTML
// ============================================================================

/** Escape text for the content of an element. */
function escapeText(text: string): string {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;");
}

/** Escape text for the value of an attribute. */
function escapeHtml(text: string): string {
  return escapeText(text).replace(/"/g, "&quot;").replace(/'/g, "&#039;");
}

/** Escape text that may hold line breaks, which become <br>. */
function escapeLines(text: string): string {
  return escapeText(text).replace(/\r?\n/g, "<br>");
}

/** An attribute, or nothing when it has no value. */
function attr(name: string, value: string | null | false): string {
  if (value === null || value === false) return "";
  return ` ${name}="${escapeHtml(value)}"`;
}

/**
 * An image in the shape the server renders it: a bare <img> at its default
 * size, and a frame carrying the size through custom properties when one is
 * given. The alt is empty, as the server's is: markup has no way to write
 * one. data-bb-source keeps a spelling the canonical one would change.
 */
function renderImage(node: Extract<BbNode, { t: "img" }>): string {
  const canonical = writeImage(node.src, node.width, node.height);
  const img =
    `<img src="${escapeHtml(node.src)}" alt=""` +
    `${attr("data-bb-source", node.source !== canonical && node.source)}` +
    ` class="bb-image" data-bb-tag="img" loading="lazy" decoding="async" referrerpolicy="no-referrer" />`;
  if (node.width === null && node.height === null) return img;

  const vars: string[] = [];
  let data = "";
  if (node.width !== null) {
    vars.push(`--bb-image-max-width:${node.width}px`);
    data += ` data-bb-width="${node.width}"`;
  }
  if (node.height !== null) {
    vars.push(`--bb-image-max-height:${node.height}px`);
    data += ` data-bb-height="${node.height}"`;
  }
  return `<span class="bb-image-frame"${data} style="${vars.join(";")}">${img}</span>`;
}

/** The tags that stand in a line, and the element each is drawn with. */
type InlineTag = "b" | "i" | "u" | "strike" | "code";

const INLINE_ELEMENTS: Record<InlineTag, string> = {
  b: "strong",
  i: "em",
  u: "u",
  strike: "s",
  code: "code",
};

const isInlineTag = (tag: string): tag is InlineTag =>
  Object.prototype.hasOwnProperty.call(INLINE_ELEMENTS, tag);

const BLOCK_ELEMENTS: Record<
  "spoiler" | "nsfw" | "quote" | "mod" | "ul" | "ol",
  [string, string]
> = {
  spoiler: ["div", "bb-spoiler"],
  nsfw: ["div", "bb-nsfw"],
  quote: ["blockquote", "bb-quote"],
  mod: ["div", "bb-mod"],
  ul: ["ul", ""],
  ol: ["ol", ""],
};

/** How a block stands against what is written next to it. */
interface Glue {
  before: boolean;
  after: boolean;
  newlineAfter: boolean;
}

const NO_GLUE: Glue = { before: false, after: false, newlineAfter: false };

function glueAttrs(glue: Glue): string {
  return (
    (glue.before ? ' data-bb-glue-before="true"' : "") +
    (glue.after ? ' data-bb-glue-after="true"' : "") +
    (glue.newlineAfter ? ' data-bb-newline-after="true"' : "")
  );
}

/**
 * A tag as typed, where the editor would write it otherwise: [B] for [b],
 * [/QUOTE] for [/quote]. The way back returns it (kept).
 */
function spellingAttrs(
  open: string,
  canonicalOpen: string,
  close: string | null,
  canonicalClose: string,
): string {
  return (
    attr("data-bb-open", open !== canonicalOpen && open) +
    attr("data-bb-close", close !== null && close !== canonicalClose && close)
  );
}

/** The spelling of a tag without parameters, as typed. */
function plainSpelling(node: {
  tag: string;
  open: string;
  close: string;
}): string {
  return spellingAttrs(
    node.open,
    `[${node.tag}]`,
    node.close,
    `[/${node.tag}]`,
  );
}

class HtmlWriter {
  private readonly blockish = new Map<BbNode, boolean>();

  /** Whether a node is drawn as a block: a block tag, or a tag holding one. */
  private isBlock(node: BbNode): boolean {
    if (node.t !== "tag" && node.t !== "private") return false;
    if (node.t === "tag" && BLOCK_TAGS.has(node.tag)) return true;
    const known = this.blockish.get(node);
    if (known !== undefined) return known;
    const result = node.children.some((child) => this.isBlock(child));
    this.blockish.set(node, result);
    return result;
  }

  /**
   * Lay out what a block holds: a run of inline content between two blocks is
   * split into paragraphs at its line breaks, and the break that puts a block
   * on a line of its own is the paragraph edge itself. Where the author wrote
   * no break at a block's edge the block is marked as glued, so the way back
   * restores what was typed.
   */
  container(nodes: BbNode[], root: boolean, itemOfAList = false): string {
    const runs: BbNode[][] = [[]];
    const blocks: BbNode[] = [];
    for (const node of nodes) {
      if (this.isBlock(node)) {
        blocks.push(node);
        runs.push([]);
      } else {
        runs[runs.length - 1].push(node);
      }
    }

    const glue: Glue[] = blocks.map(() => ({ ...NO_GLUE }));
    const paragraphs: BbNode[][][] = runs.map((run, i) => {
      let lines = splitLines(run);
      const before = i > 0 ? glue[i - 1] : null;
      const after = i < blocks.length ? glue[i] : null;
      const empty = run.length === 0;
      const lineBreakOnly =
        lines.length === 2 && lines[0].length === 0 && lines[1].length === 0;

      if (empty) {
        if (before && after) after.before = true;
        return [];
      }
      if (before && after && lineBreakOnly) return [];
      if (root && before && !after && lineBreakOnly) {
        before.newlineAfter = true;
        return [];
      }
      if (before) {
        if (lines.length >= 2 && lines[0].length === 0) lines = lines.slice(1);
        else before.after = true;
      }
      if (after) {
        if (lines.length >= 2 && lines[lines.length - 1].length === 0) {
          lines = lines.slice(0, -1);
        } else {
          after.before = true;
        }
      }
      return lines;
    });

    // An item of a list starts with a line in the visual mode: its schema
    // holds a paragraph first and blocks after it, and an item that started
    // with a block, a list inside the item included, had the block lifted out
    // of the list. So such an item gets an empty line in front of the block,
    // and the block says it is glued to it, which the way back reads as
    // nothing written between them.
    let html = "";
    if (itemOfAList && blocks.length > 0 && runs[0].length === 0) {
      glue[0].before = true;
      html += "<p></p>";
    }
    paragraphs.forEach((lines, i) => {
      for (const line of lines) {
        html += `<p>${line.map((node) => this.inline(node)).join("")}</p>`;
      }
      if (i < blocks.length) html += this.block(blocks[i], glue[i], false);
    });
    return html;
  }

  /** What [ul] and [ol] hold: items, and whatever was written between them. */
  private list(nodes: BbNode[]): string {
    return nodes
      .map((node) => {
        if (node.t === "text") return escapeText(node.s);
        if (this.isBlock(node)) return this.block(node, NO_GLUE, true);
        return this.inline(node);
      })
      .join("");
  }

  private block(node: BbNode, glue: Glue, inList: boolean): string {
    const glued = glueAttrs(glue);
    if (node.t === "private") {
      return `<span${this.privateAttrs(node)}${glued}>${this.container(node.children, false)}</span>`;
    }
    if (node.t !== "tag") return this.inline(node);

    if (node.tag === "li") {
      const element = inList ? "li" : "div";
      return `<${element} data-bb-tag="li"${plainSpelling(node)}${glued}>${this.container(node.children, false, inList)}</${element}>`;
    }
    if (isInlineTag(node.tag)) {
      const element = INLINE_ELEMENTS[node.tag];
      return `<${element} data-bb-tag="${node.tag}"${plainSpelling(node)}${glued}>${this.container(node.children, false)}</${element}>`;
    }

    const [element, cls] = BLOCK_ELEMENTS[node.tag];
    const classAttr = cls ? ` class="${cls}"` : "";
    let extra = plainSpelling(node);
    if (node.tag === "spoiler") {
      const canonical = writeOpening("spoiler", [["text", node.caption]]);
      extra =
        attr("data-bb-text", node.caption) +
        spellingAttrs(node.open, canonical, node.close, "[/spoiler]");
    }
    if (node.tag === "ul" || node.tag === "ol") {
      if (!holdsOnlyItems(node.children)) {
        // A list that holds something besides its items. The visual mode
        // holds items and nothing else in a list, and put whatever else stood
        // there into items of its own or out of the list, and out of a
        // hidden block around the list with it. So such a list is drawn as a
        // block holding what it holds, in the order it holds it, and comes
        // back as typed.
        return `<div data-bb-tag="${node.tag}" data-bb-loose="true"${extra}${glued}>${this.container(node.children, false)}</div>`;
      }
      return `<${element}${classAttr} data-bb-tag="${node.tag}"${extra}${glued}>${this.list(node.children)}</${element}>`;
    }
    return `<${element}${classAttr} data-bb-tag="${node.tag}"${extra}${glued}>${this.container(node.children, false)}</${element}>`;
  }

  private privateAttrs(node: Extract<BbNode, { t: "private" }>): string {
    return (
      ` class="bb-private" data-bb-tag="private"` +
      attr("data-bb-character", node.value) +
      spellingAttrs(
        node.open,
        writePrivateOpening(node.value),
        node.close,
        "[/private]",
      ) +
      (node.close === null ? ' data-bb-unclosed="true"' : "")
    );
  }

  private inline(node: BbNode): string {
    switch (node.t) {
      case "text":
        return escapeLines(node.s);
      case "tab":
        return `<span class="bb-tab" data-bb-tag="tab"${attr("data-bb-open", node.open !== "[tab]" && node.open)}>&nbsp;&nbsp;&nbsp;</span>`;
      case "verbatim":
        return `<span class="bb-noparse" data-bb-tag="noparse"${plainSpelling(node)}>${escapeLines(node.s)}</span>`;
      case "img":
        return renderImage(node);
      case "link": {
        const canonical = writeLink(node.href, node.text);
        const marker =
          node.text === null
            ? ' data-bb-selfref="true"'
            : attr("data-bb-text", node.text);
        return (
          `<a href="${escapeHtml(node.href)}" data-bb-tag="link"${marker}` +
          `${attr("data-bb-source", node.source !== canonical && node.source)}` +
          ` target="_blank" rel="noopener">${escapeText(node.text || DEFAULT_LINK_TEXT)}</a>`
        );
      }
      case "mention":
        return (
          `<a class="bb-mention" href="/users/${encodeURIComponent(node.login)}"` +
          ` data-bb-tag="mention" data-bb-user="${escapeHtml(node.login)}"` +
          `${attr("data-bb-source", node.source !== writeMention(node.login) && node.source)}` +
          `>@${escapeText(node.login)}</a>`
        );
      case "private":
        return `<span${this.privateAttrs(node)}>${node.children.map((child) => this.inline(child)).join("")}</span>`;
      case "tip":
        // The hint has no visual mode: its tag is the text as typed here,
        // and comes back from it as typed. Its preview is hintToHtml.
        return escapeLines(node.source);
      case "tag": {
        if (this.isBlock(node)) return this.block(node, NO_GLUE, false);
        const element = INLINE_ELEMENTS[node.tag as InlineTag];
        return `<${element} data-bb-tag="${node.tag}"${plainSpelling(node)}>${node.children.map((child) => this.inline(child)).join("")}</${element}>`;
      }
    }
  }
}

/**
 * Whether a list holds items and nothing else: the spaces and line breaks
 * between them do not count, the page does not show them. A list with no item
 * in it holds something else, since the visual mode would give it one.
 */
function holdsOnlyItems(nodes: BbNode[]): boolean {
  return (
    nodes.some((node) => node.t === "tag" && node.tag === "li") &&
    nodes.every(
      (node) =>
        (node.t === "tag" && node.tag === "li") ||
        (node.t === "text" && !trimWhiteSpace(node.s)),
    )
  );
}

/** Split a run of inline content into lines at the line breaks of its text. */
function splitLines(run: BbNode[]): BbNode[][] {
  const lines: BbNode[][] = [[]];
  for (const node of run) {
    if (node.t !== "text") {
      lines[lines.length - 1].push(node);
      continue;
    }
    node.s.split(/\r?\n/).forEach((part, index) => {
      if (index > 0) lines.push([]);
      if (part) lines[lines.length - 1].push({ t: "text", s: part });
    });
  }
  return lines;
}

/** Options of bbcodeToHtml. */
export interface BbcodeToHtmlOptions {
  /** The tag set of the surface the text is for; every tag when absent. */
  context?: BBCodeContext;
}

/**
 * Warn in a development console when a conversion is slow. Vitest runs with
 * DEV set as well and is left out by hand: the warning is meant for a person
 * watching a browser, not for a test log.
 */
const PERF_ENABLED = import.meta.env.DEV && import.meta.env.MODE !== "test";
const PERF_THRESHOLD_MS = 10;

function measurePerformance<T>(name: string, length: number, fn: () => T): T {
  if (!PERF_ENABLED) return fn();
  const start = performance.now();
  const result = fn();
  const duration = performance.now() - start;
  if (duration > PERF_THRESHOLD_MS) {
    console.warn(
      `[bbcode] Slow ${name}: ${duration.toFixed(2)}ms for ${length} chars`,
    );
  }
  return result;
}

/**
 * Convert BBCode to the HTML the visual mode edits.
 *
 * Every element carries data-bb-* attributes that htmlToBbcode reads back;
 * a spelling that is not markup is text, as typed. The characters that
 * reorder text for display are taken out first, as the server does.
 *
 * @example
 * bbcodeToHtml("[b]Hello[/b]")
 * // => '<p><strong data-bb-tag="b">Hello</strong></p>'
 *
 * bbcodeToHtml('[link text="Home"]https://example.com/[/link]')
 * // => '<p><a href="https://example.com/" data-bb-tag="link" data-bb-text="Home" ...>Home</a></p>'
 */
export function bbcodeToHtml(
  bbcode: string,
  options: BbcodeToHtmlOptions = {},
): string {
  if (!bbcode) return "";
  return measurePerformance("bbcodeToHtml", bbcode.length, () => {
    const tags = options.context
      ? new Set(CONTEXT_TAGS[options.context])
      : ALL_TAGS;
    const nodes = readText(asTheServerReads(bbcode), tags, null);
    return new HtmlWriter().container(nodes, true);
  });
}

// ============================================================================
// THE HINT
// ============================================================================

/** A value inside a quoted attribute, encoded as the server's TipTag encodes it. */
function tipAttr(name: string, value: string | null): string {
  if (value === null) return "";
  const encoded = escapeText(value)
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#39;");
  return ` ${name}="${encoded}"`;
}

/**
 * The description of a game tag as a reader gets it in the tag's tooltip: the
 * markup the server renders for the hint surface (BbSurface.Hint), character
 * for character, for the moderator's preview in the window of the tag.
 *
 * The one exception BBCODE_RENDERING.md makes for the editor's converter is
 * made for this preview too: the page takes the server's markup, and the
 * preview draws what the page will. The pairs of hintMarkup.cases.ts hold the
 * two to one string. Text is escaped as the server escapes it, a line break
 * is a break, and nothing else is substituted: the hint has no typography. A
 * [tip] is the span the tooltip wires its small tooltip to, and a [noparse]
 * its content as typed. A hidden block is the text it was typed as: the save
 * refuses it with a reason, and the preview shows the moderator the text the
 * reason is about rather than hide it.
 */
export function hintToHtml(bbcode: string): string {
  if (!bbcode) return "";
  const nodes = readText(
    asTheServerReads(bbcode),
    new Set(CONTEXT_TAGS.hint),
    null,
  );
  return nodes
    .map((node) => {
      switch (node.t) {
        case "text":
          return escapeText(node.s).replace(/\r?\n/g, "<br />");
        case "verbatim":
          return escapeText(node.s);
        case "tip":
          return (
            `<span class="bb-tip" tabindex="0"` +
            `${tipAttr("data-tip-text", node.text)}${tipAttr("data-tip-img", node.img)}>` +
            `${escapeText(node.word)}</span>`
          );
        default:
          // The hint reads [tip] and [noparse] and nothing else, so no other
          // node comes out of the reader.
          throw new Error(`the hint read a node it has no tag for: ${node.t}`);
      }
    })
    .join("");
}

// ============================================================================
// HTML -> BBCODE
// ============================================================================

/** Elements that stand on lines of their own. */
const BLOCK_HTML: ReadonlySet<string> = new Set([
  "DIV",
  "BLOCKQUOTE",
  "UL",
  "OL",
  "LI",
  "PRE",
  "HR",
  "TABLE",
  "SECTION",
  "ARTICLE",
  "HEADER",
  "FOOTER",
  "H1",
  "H2",
  "H3",
  "H4",
  "H5",
  "H6",
]);

type Kind = "p" | "block" | "inline";

function kindOf(node: Node): Kind | null {
  if (node.nodeType === Node.TEXT_NODE) return "inline";
  if (node.nodeType !== Node.ELEMENT_NODE) return null;
  const element = node as Element;
  if (element.tagName === "P") return "p";
  if (BLOCK_HTML.has(element.tagName)) return "block";
  return element.querySelector("p, div, blockquote, ul, ol, li, pre")
    ? "block"
    : "inline";
}

function flag(node: Node, name: string): boolean {
  return (node as Element).getAttribute?.(name) === "true";
}

/**
 * The line break between two neighbours of a container. Paragraphs are lines,
 * and a block starts and ends a line: the visual mode has no other way to put
 * a block apart. A block that the author glued to its neighbour says so. Text
 * next to a block is read as written, which is the shape of the server's own
 * markup, where every line break is a <br>.
 */
function separator(
  prev: { node: Node; kind: Kind },
  next: { node: Node; kind: Kind },
): string {
  if (prev.kind === "inline" || next.kind === "inline") return "";
  if (prev.kind === "p" && next.kind === "p") return "\n";
  const glued =
    (prev.kind === "block" && flag(prev.node, "data-bb-glue-after")) ||
    (next.kind === "block" && flag(next.node, "data-bb-glue-before"));
  return glued ? "" : "\n";
}

type Mode = "root" | "container" | "list";

/**
 * Text a node holds, with its <br> as line breaks. A no-break space stays one:
 * the author typed it, and the way back returns what was typed. The spaces a
 * paste brings in as no-break ones are made plain on the way in
 * (cleanPastedHtml).
 */
function textOf(node: Node): string {
  if (node.nodeType === Node.TEXT_NODE) return node.nodeValue ?? "";
  if ((node as Element).tagName === "BR") return "\n";
  let out = "";
  node.childNodes.forEach((child) => {
    out += textOf(child);
  });
  return out;
}

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

const EXAMPLE_OPENING = "[noparse]";
const EXAMPLE_CLOSING = "[/noparse]";
const HIDDEN_CLOSING_LENGTH = "[/private]".length;

/**
 * A character the markup does not hold: the first of the private use area
 * that it carries nowhere. The writer puts it before what it opens itself, an
 * example or a hidden block, to tell those from what came from the text.
 */
function markNotIn(markup: string): string {
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
 * What the reader makes of a "[noparse]" of the written text.
 *
 * An example runs from its "[noparse]" to the first closing tag after it,
 * wherever that stands. The writer's own examples hold no closing tag and end
 * on their own. A "[noparse]" that came from the text, typed in the visual
 * mode or standing in an address, is an example as well when a closing tag
 * follows it, and it takes along everything up to that tag: "[noparse]", a
 * hidden block, "[/noparse]", typed around the block in the visual mode,
 * printed the hidden text to every reader. So when a hidden block of the
 * document starts before that closing tag, the "[noparse]" is written as an
 * example of itself (escape) and is no example. Otherwise it stays as typed,
 * whatever it does to text around it, which the visual mode showed as text
 * anyway: a text read from BBCode, where such a "[noparse]" was an example
 * already or had no closing tag ahead, comes back as typed.
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
function settleExamples(written: string, mark: string): string {
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

class BbcodeWriter {
  /**
   * @param breaksWritten The markup writes every line break as a <br> and
   *   has none between two blocks otherwise: the server's markup for the
   *   author (BbParserWrapper.LineBreaksWritten). The visual mode's own markup
   *   has no break between blocks, and a block stands on a line of its own
   *   there unless it says it is glued (separator).
   * @param mark Put before every example and every hidden block the writer
   *   opens (settleExamples): a character the markup does not hold.
   */
  constructor(
    private readonly breaksWritten: boolean,
    private readonly mark: string,
  ) {}

  children(parent: Node, mode: Mode): string {
    const items: Array<{ node: Node; kind: Kind }> = [];
    parent.childNodes.forEach((node) => {
      const kind = kindOf(node);
      if (kind) items.push({ node, kind });
    });

    if (mode === "root") {
      // The visual mode keeps an empty paragraph after a closing block, for
      // the caret to go to, and leaves it there when the block is taken off.
      // Empty lines at the very end are nothing a reader sees, and the text
      // ends where its last written line does.
      while (
        items.length > 0 &&
        items[items.length - 1].kind === "p" &&
        !items[items.length - 1].node.hasChildNodes()
      ) {
        items.pop();
      }
    }

    let out = "";
    items.forEach((item, index) => {
      if (mode !== "list" && index > 0 && !this.breaksWritten)
        out += separator(items[index - 1], item);
      out += this.node(item.node);
    });

    const last = items[items.length - 1];
    if (
      mode === "root" &&
      last?.kind === "block" &&
      flag(last.node, "data-bb-newline-after")
    ) {
      out += "\n";
    }
    return out;
  }

  private wrap(
    tag: string,
    element: Element,
    mode: Mode = "container",
  ): string {
    const open = kept(element, "data-bb-open", `[${tag}]`);
    const close = kept(element, "data-bb-close", `[/${tag}]`);
    return `${open}${this.children(element, mode)}${close}`;
  }

  /**
   * [noparse]: what it holds is text, as typed.
   *
   * An example ends on the first closing tag after its opening, so one the
   * text holds, typed into a [noparse] of the visual mode, ended it early and
   * the rest of the text was read as markup: inside a hidden block, a
   * "[/private]" after it showed the rest of the block to every reader. A
   * closing tag in the text is split after its "[" into two examples, which
   * show it as typed. A text read from BBCode holds none.
   */
  private example(element: Element): string {
    const open = kept(element, "data-bb-open", EXAMPLE_OPENING);
    const close = kept(element, "data-bb-close", EXAMPLE_CLOSING);
    const text = textOf(element);
    let held = "";
    let from = 0;
    for (
      let at = findCloser(text, "noparse", 0);
      at >= 0;
      at = findCloser(text, "noparse", at + 1)
    ) {
      held += `${text.slice(from, at + 1)}${EXAMPLE_CLOSING}${this.mark}${EXAMPLE_OPENING}`;
      from = at + 1;
    }
    return `${this.mark}${open}${held}${text.slice(from)}${close}`;
  }

  /**
   * The body of a hidden block, with every closing tag that would end the
   * block early written as an example of itself.
   *
   * The visual mode shows typed text as text, and "[/private]" typed inside a
   * hidden block stood inside it on the screen: written out as it was, it
   * ended the block there, and what followed it went to every reader, while
   * the save took the text whole. A closing tag that ends a block opened in
   * the body itself (one typed there, or one the author's markup gives as
   * typed) is left as it is, as is anything inside an example of the writer.
   * A text read from BBCode has no other: the first of them closed its block.
   */
  private keptInside(body: string): string {
    const mark = this.mark;
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

  private node(node: Node): string {
    if (node.nodeType === Node.TEXT_NODE) return node.nodeValue ?? "";
    const element = node as Element;

    switch (element.getAttribute("data-bb-tag")) {
      case "b":
      case "i":
      case "u":
      case "strike":
      case "nsfw":
      case "mod":
      case "quote":
        return this.wrap(
          element.getAttribute("data-bb-tag") as string,
          element,
        );
      case "ul":
      case "ol":
        return this.wrap(
          element.getAttribute("data-bb-tag") as string,
          element,
          flag(element, "data-bb-loose") ? "container" : "list",
        );
      case "li":
        return this.wrap("li", element);
      case "code":
        return this.wrap("code", element);
      case "noparse":
        return this.example(element);
      case "tab":
        return kept(element, "data-bb-open", "[tab]");
      case "spoiler":
        return this.spoiler(element);
      case "link":
        return this.markedLink(element);
      case "mention":
        return this.mention(element);
      case "img":
        return this.image(element, null);
      case "private":
        return this.hidden(element);
    }

    const classes = element.classList;
    switch (element.tagName) {
      case "P":
        return this.children(element, "container");
      case "BR":
        return "\n";
      case "STRONG":
      case "B":
        return this.wrap("b", element);
      case "EM":
      case "I":
        return this.wrap("i", element);
      case "U":
        return this.wrap("u", element);
      case "S":
      case "STRIKE":
      case "DEL":
        return this.wrap("strike", element);
      case "CODE":
      case "PRE":
        return this.wrap("code", element);
      case "UL":
        return this.wrap("ul", element, "list");
      case "OL":
        return this.wrap("ol", element, "list");
      case "LI":
        return this.wrap("li", element);
      case "BLOCKQUOTE":
        return this.wrap("quote", element);
      case "A":
        return classes.contains("bb-mention")
          ? this.mention(element)
          : this.plainLink(element);
      case "IMG":
        return this.plainImage(element);
      case "SCRIPT":
      case "STYLE":
      case "TEMPLATE":
        return "";
      case "DIV":
        if (classes.contains("spoiler") || classes.contains("bb-spoiler")) {
          return this.spoiler(element);
        }
        if (classes.contains("nsfw-spoiler") || classes.contains("bb-nsfw")) {
          return this.wrap("nsfw", element);
        }
        if (classes.contains("mod-block") || classes.contains("bb-mod")) {
          return this.wrap("mod", element);
        }
        if (classes.contains("private-message")) return this.hidden(element);
        return this.children(element, "container");
      case "SPAN":
        if (classes.contains("bb-image-frame")) {
          const img = element.querySelector("img");
          return img ? this.image(img, element) : "";
        }
        if (classes.contains("bb-tab")) {
          return kept(element, "data-bb-open", "[tab]");
        }
        if (classes.contains("bb-noparse")) {
          return this.example(element);
        }
        if (
          classes.contains("bb-private") ||
          classes.contains("private-text")
        ) {
          return this.hidden(element);
        }
        return this.children(element, "container");
      default:
        return this.children(element, "container");
    }
  }

  private spoiler(element: Element): string {
    let caption = element.getAttribute("data-bb-text");
    if (caption !== null && !isWritableValue(caption)) caption = null;
    const canonical = writeOpening("spoiler", [["text", caption]]);
    const raw = element.getAttribute("data-bb-open");
    const open = raw !== null && readsAsSpoiler(raw, caption) ? raw : canonical;
    const close = kept(element, "data-bb-close", "[/spoiler]");
    return `${open}${this.children(element, "container")}${close}`;
  }

  private hidden(element: Element): string {
    let value: string | null;
    if (element.hasAttribute("data-bb-character")) {
      value = element.getAttribute("data-bb-character");
    } else if (element.hasAttribute("data-bb-addressees")) {
      value = element.getAttribute("data-bb-addressees") || null;
    } else {
      value =
        element.getAttribute("data-users") ??
        element.getAttribute("data-character") ??
        element.getAttribute("data-bb-users");
    }
    // An addressee list that cannot be written is dropped, never the block:
    // a block addressed to nobody hides more, not less.
    if (value !== null && !isWritablePrivateValue(value)) value = null;

    const raw = element.getAttribute("data-bb-open");
    const open =
      raw !== null && readsAsPrivate(raw, value)
        ? raw
        : writePrivateOpening(value);
    const close = flag(element, "data-bb-unclosed")
      ? ""
      : kept(element, "data-bb-close", "[/private]");
    const body = this.keptInside(this.children(element, "container"));
    // A spelling that ends right after the name ("[private" before a "[" of
    // the text, or at its end) is a block only while what follows the name
    // ends the token. What followed it was the block's own text, and the
    // visual mode can put a tag in front of that text: "[private" and "[u]"
    // are no block at all, and the block's text went to every reader of the
    // page. A space ends the token as the text did, and the block stays the
    // block it was, spelled wrong and refused on save.
    const endsAtName = /[A-Za-z]$/.test(open);
    const tokenEnds =
      body === ""
        ? close === ""
        : body[0] === "]" || body[0] === "=" || isSpace(body[0]);
    return `${this.mark}${open}${endsAtName && !tokenEnds ? " " : ""}${body}${close}`;
  }

  private markedLink(element: Element): string {
    const href = element.getAttribute("href") ?? "";
    let text: string | null;
    if (flag(element, "data-bb-selfref")) text = null;
    else if (element.hasAttribute("data-bb-text")) {
      text = element.getAttribute("data-bb-text");
    } else {
      const shown = element.textContent ?? "";
      text = shown === DEFAULT_LINK_TEXT ? null : shown;
    }
    if (!isSafeAddress(href)) return text ?? "";
    if (text !== null && !isWritableValue(text)) text = null;
    const canonical = writeLink(trimWhiteSpace(href), text);
    const link = fromSource(
      element,
      canonical,
      (node) =>
        node.t === "link" &&
        node.href === trimWhiteSpace(href) &&
        node.text === text,
    );
    // Two links side by side with the same address and caption are one run
    // of text under one mark in the visual mode, which joins neighbours that
    // carry the same mark, and came back as one link. What such an anchor
    // shows is the caption as many times as there were links.
    const caption = text ?? DEFAULT_LINK_TEXT;
    const shown = element.textContent ?? "";
    const times =
      caption && shown.length % caption.length === 0
        ? shown.length / caption.length
        : 1;
    return times > 1 && caption.repeat(times) === shown
      ? link.repeat(times)
      : link;
  }

  private plainLink(element: Element): string {
    const href = trimWhiteSpace(element.getAttribute("href") ?? "");
    const text = trimWhiteSpace(element.textContent ?? "");
    if (!isSafeAddress(href)) return text || href;
    if (
      !text ||
      text === href ||
      text === DEFAULT_LINK_TEXT ||
      !isWritableValue(text)
    ) {
      return writeLink(href, null);
    }
    return writeLink(href, text);
  }

  private mention(element: Element): string {
    const login = element.getAttribute("data-bb-user") ?? "";
    if (!trimWhiteSpace(login) || /[[\r\n]/.test(login))
      return element.textContent ?? "";
    return fromSource(
      element,
      writeMention(trimWhiteSpace(login)),
      (node) => node.t === "mention" && node.login === trimWhiteSpace(login),
    );
  }

  private image(img: Element, frame: Element | null): string {
    const src = trimWhiteSpace(img.getAttribute("src") ?? "");
    if (!isSafeAddress(src)) return "";
    const size = (name: string): string | null => {
      const value = frame?.getAttribute(name) ?? null;
      return value !== null && PIXELS.test(value) ? value : null;
    };
    const width = size("data-bb-width");
    const height = size("data-bb-height");
    const canonical = writeImage(src, width, height);
    return fromSource(
      img,
      canonical,
      (node) =>
        node.t === "img" &&
        node.src === src &&
        node.width === width &&
        node.height === height,
    );
  }

  private plainImage(img: Element): string {
    const src = trimWhiteSpace(img.getAttribute("src") ?? "");
    return isSafeAddress(src) ? `[img]${src}[/img]` : "";
  }
}

/** Whether a kept spoiler opening still says what the element says. */
function readsAsSpoiler(raw: string, caption: string | null): boolean {
  const nodes = readText(`${raw}[/spoiler]`, ALL_TAGS, null);
  const node = nodes[0];
  return (
    nodes.length === 1 &&
    node.t === "tag" &&
    node.tag === "spoiler" &&
    node.caption === caption &&
    node.children.length === 0
  );
}

/** Whether a kept [private] opening still says what the element says. */
function readsAsPrivate(raw: string, value: string | null): boolean {
  if (!isPrivateToken(raw, 0)) return false;
  const opening = readPrivateOpening(raw, closingBrackets(raw), 8);
  return opening.end === raw.length && opening.value === value;
}

/**
 * A tag without parameters as the author typed it, kept in an attribute, when
 * it still spells the tag the element is ([B] for [b]); the editor's own
 * spelling otherwise.
 */
function kept(element: Element, name: string, canonical: string): string {
  const raw = element.getAttribute(name);
  return raw !== null && asciiLower(raw) === canonical ? raw : canonical;
}

/**
 * An image, a link or a mention as the author typed it, when the element
 * still says what that spelling says; the canonical spelling otherwise.
 */
function fromSource(
  element: Element,
  canonical: string,
  same: (node: BbNode) => boolean,
): string {
  const source = element.getAttribute("data-bb-source");
  if (source === null) return canonical;
  const nodes = readText(source, ALL_TAGS, null);
  return nodes.length === 1 && same(nodes[0]) ? source : canonical;
}

/**
 * Every text and attribute under a node as the server reads it (asTheServerReads).
 *
 * The server takes these characters out before it reads a tag, and what stood
 * on either side of one joins: "[/pri" U+0001 "vate]" pasted into a hidden
 * block in the visual mode is a closing tag to the server and nothing to a
 * writer that looks for one. The editor's own text holds none (bbcodeToHtml
 * reads the text the server reads), and neither does the server's markup for
 * the author, so this changes only what was pasted or typed, and nothing a
 * reader sees.
 */
function readAsTheServerDoes(root: Node): void {
  const walker = document.createTreeWalker(
    root,
    NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT,
  );
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    if (node.nodeType === Node.TEXT_NODE) {
      const text = node.nodeValue ?? "";
      const read = asTheServerReads(text);
      if (read !== text) node.nodeValue = read;
      continue;
    }
    for (const attribute of Array.from((node as Element).attributes)) {
      const read = asTheServerReads(attribute.value);
      if (read !== attribute.value) attribute.value = read;
    }
  }
}

/**
 * Convert the HTML of the visual mode, or the server's rendering for the
 * author (the author_edit audience), back to BBCode.
 *
 * Elements are read by their data-bb-tag first, and by their tag and class
 * otherwise, which covers HTML pasted from elsewhere. An address the server
 * would refuse does not come back as markup.
 *
 * What the visual mode shows as text comes back as text wherever the way it
 * reads decides who reads it: a closing tag typed inside a hidden block
 * (BbcodeWriter.keptInside) or inside an example (BbcodeWriter.example), and
 * an example opened in the text around a hidden block (settleExamples), are
 * written as examples of themselves. Written as typed, each of them ended the
 * hidden block or swallowed it, and the save took the text.
 *
 * @example
 * htmlToBbcode('<p><strong data-bb-tag="b">Hello</strong></p>')
 * // => "[b]Hello[/b]"
 *
 * htmlToBbcode('<a href="https://example.com/">Home</a>')
 * // => '[link text="Home"]https://example.com/[/link]'
 */
export function htmlToBbcode(html: string): string {
  if (!html) return "";
  return measurePerformance("htmlToBbcode", html.length, () => {
    const template = document.createElement("template");
    template.innerHTML = html;
    const first = template.content.firstChild;
    const breaksWritten =
      first?.nodeType === Node.ELEMENT_NODE &&
      (first as Element).getAttribute("data-bb-breaks") === "written";
    if (breaksWritten) first.remove();
    readAsTheServerDoes(template.content);
    const mark = markNotIn(template.innerHTML);
    return settleExamples(
      new BbcodeWriter(breaksWritten, mark).children(template.content, "root"),
      mark,
    );
  });
}

// ============================================================================
// PASTED HTML
// ============================================================================

/**
 * Clean up HTML pasted from Word, Google Docs, and other rich text editors.
 *
 * Removes junk markup (Office XML, styles, scripts) while preserving
 * semantic formatting tags that can be converted to BBCode.
 *
 * @param html - Raw pasted HTML
 * @returns Cleaned HTML suitable for conversion
 *
 * @example
 * ```typescript
 * cleanPastedHtml('<p class="MsoNormal" style="font-size:12pt">Hello</p>')
 * // => 'Hello\n'
 * ```
 */
export function cleanPastedHtml(html: string): string {
  if (!html) return "";

  let cleaned = html;

  // Remove Microsoft Office specific tags and namespaces
  cleaned = cleaned.replace(/<o:p[^>]*>[\s\S]*?<\/o:p>/gi, "");
  cleaned = cleaned.replace(/<w:[^>]+>[\s\S]*?<\/w:[^>]+>/gi, "");
  cleaned = cleaned.replace(/<m:[^>]+>[\s\S]*?<\/m:[^>]+>/gi, "");
  cleaned = cleaned.replace(/<!--\[if[^>]*>[\s\S]*?<!\[endif\]-->/gi, "");
  cleaned = cleaned.replace(/<!--[\s\S]*?-->/g, "");

  // Remove XML declarations and processing instructions
  cleaned = cleaned.replace(/<\?xml[^>]*>/gi, "");
  cleaned = cleaned.replace(/<!\[CDATA\[[\s\S]*?\]\]>/gi, "");

  // Remove style, script, and meta tags
  cleaned = cleaned.replace(/<style[^>]*>[\s\S]*?<\/style>/gi, "");
  cleaned = cleaned.replace(/<script[^>]*>[\s\S]*?<\/script>/gi, "");
  cleaned = cleaned.replace(/<meta[^>]*\/?>/gi, "");
  cleaned = cleaned.replace(/<link[^>]*\/?>/gi, "");
  cleaned = cleaned.replace(/<title[^>]*>[\s\S]*?<\/title>/gi, "");
  cleaned = cleaned.replace(/<head[^>]*>[\s\S]*?<\/head>/gi, "");
  cleaned = cleaned.replace(/<html[^>]*>/gi, "");
  cleaned = cleaned.replace(/<\/html>/gi, "");
  cleaned = cleaned.replace(/<body[^>]*>/gi, "");
  cleaned = cleaned.replace(/<\/body>/gi, "");

  // Remove all inline styles
  cleaned = cleaned.replace(/\s*style="[^"]*"/gi, "");
  cleaned = cleaned.replace(/\s*class="[^"]*"/gi, "");
  cleaned = cleaned.replace(/\s*id="[^"]*"/gi, "");

  // Remove font tags (deprecated HTML)
  cleaned = cleaned.replace(/<font[^>]*>/gi, "");
  cleaned = cleaned.replace(/<\/font>/gi, "");

  // Remove empty spans
  cleaned = cleaned.replace(/<span[^>]*>\s*<\/span>/gi, "");
  // Unwrap non-empty spans
  cleaned = cleaned.replace(/<span[^>]*>([\s\S]*?)<\/span>/gi, "$1");

  // Remove div wrappers
  cleaned = cleaned.replace(/<div[^>]*>([\s\S]*?)<\/div>/gi, "$1\n");

  // Normalize whitespace. A document pasted from elsewhere spaces its words
  // with no-break spaces as often as not, and the way back keeps a no-break
  // space as typed, so here, on the way in, they become plain ones.
  cleaned = cleaned.replace(/&nbsp;|\u00a0/gi, " ");
  cleaned = cleaned.replace(/\r\n/g, "\n");
  cleaned = cleaned.replace(/\r/g, "\n");
  cleaned = cleaned.replace(/\t/g, " ");
  cleaned = cleaned.replace(/ {2,}/g, " ");
  cleaned = cleaned.replace(/\n{3,}/g, "\n\n");

  // Remove zero-width characters
  cleaned = cleaned.replace(/[\u200B-\u200D\uFEFF]/g, "");

  return cleaned.trim();
}

// ============================================================================
// THE MARKUP CHECK
// ============================================================================

/**
 * How a tag that takes a value is written, for the finding of an "=" after its
 * name. A tag missing here takes no value at all.
 */
const WRITTEN_WITHOUT_EQUALS: Record<string, string> = {
  img: '[img width="300"]адрес[/img]',
  link: '[link text="Текст"]адрес[/link]',
  spoiler: '[spoiler text="Подпись"]',
  mention: "[mention]Логин[/mention]",
  tip: '[tip text="Текст"]слово[/tip]',
};

/** The parameters of a tag, as a sentence names them. */
const PARAMS_IN_WORDS: Record<string, string> = {
  img: "width и height",
  link: "text",
  spoiler: "text",
  tip: "text и img",
};

/** One parameter of a tag, written right. */
const PARAM_EXAMPLE: Record<string, string> = {
  img: 'width="300"',
  link: 'text="Текст"',
  spoiler: 'text="Подпись"',
  tip: 'text="Текст"',
};

/** A tag as typed, at most 40 characters of it, and three dots when cut. */
function shown(typed: string | undefined): string {
  const text = (typed ?? "").replace(/\s+/g, " ");
  return text.length > 40 ? `${text.slice(0, 40)}...` : text;
}

/**
 * The words for a finding: what is wrong and how to fix it, one sentence
 * without a period. Every text here is the table of BBCODE_RENDERING.md,
 * section "Предупреждения редактора", word for word.
 */
function wordIssue(issue: RawIssue): string {
  const tag = issue.tag ?? "";
  const param = issue.param ?? "";
  const takesParams = tag in PARAMS_IN_WORDS;
  switch (issue.reason) {
    case "value-after-name":
      return tag in WRITTEN_WITHOUT_EQUALS
        ? `[${tag}] пишется без = после имени: ${WRITTEN_WITHOUT_EQUALS[tag]}`
        : `у [${tag}] нет значения, пишется просто [${tag}]`;
    case "unknown-param":
      if (!takesParams)
        return `у [${tag}] нет параметров, пишется просто [${tag}]`;
      return param
        ? `у [${tag}] нет параметра ${param}, есть только ${PARAMS_IN_WORDS[tag]}`
        : `в [${tag}] параметр пишется как имя="значение", например ${PARAM_EXAMPLE[tag]}`;
    case "unquoted-value":
      return `значение ${param} пишется в прямых кавычках: ${param}="..."`;
    case "repeated-param":
      return `параметр ${param} указан дважды, оставьте один`;
    case "bracket-in-value":
      return `символ [ в значении ${param} не допускается, уберите его`;
    case "newline-in-value":
      return `значение ${param} пишется в одну строку`;
    case "unterminated-value":
      return `у значения ${param} нет закрывающей кавычки перед ]`;
    case "stray-space":
      return takesParams
        ? `лишний пробел в [${tag}]: пробел ставится только перед именем параметра`
        : `лишний пробел в [${tag}], пишется просто [${tag}]`;
    case "bad-number":
      return `значение ${param} должно быть целым числом от 1 до 9999`;
    case "bad-content":
      if (tag === "tip") {
        return issue.case === "nested"
          ? "[tip] внутри [tip] не работает"
          : "между [tip] и [/tip] пишутся слова в одну строку, без тегов";
      }
      return tag === "mention"
        ? "между [mention] и [/mention] пишется только логин"
        : `между [${tag}] и [/${tag}] пишется только адрес`;
    case "missing-param":
      return 'у [tip] нужен text или img: [tip text="Текст"]слово[/tip]';
    case "bad-address":
      if (issue.case === "scheme") {
        return tag === "tip"
          ? "адрес в [tip] должен начинаться с http://, https:// или /"
          : `адрес в [${tag}] должен начинаться с http:// или https://`;
      }
      return `адрес в [${tag}] указывает на локальную сеть или записан с ошибкой`;
    case "value-on-closing-tag":
      return `закрывающий тег пишется без значения: [/${tag}]`;
    case "private-spelling":
      switch (issue.case) {
        case "unclosed":
          return "скрытый текст не закрыт, добавьте [/private]";
        case "after-left-open":
          return `${shown(issue.typed)} не станет примером: [noparse] выше не закрыт тегом [/noparse]`;
        case "closing-value":
          return `${shown(issue.typed)} не закрывает скрытый текст, закрывает только [/private]`;
        default:
          return `${shown(issue.typed)} записан неверно, скрытый текст пишется [private="Имя"] или [private]`;
      }
    case "private-nested":
      return "скрытый текст внутри скрытого не работает, закройте первый тегом [/private]";
    case "private-outside-game":
      return "скрытый текст работает только в игровых постах, для примера оберните его в [noparse]";
    case "unclosed":
      return `[${tag}] не закрыт, добавьте [/${tag}]`;
    case "stray-closing":
      return issue.case === "sealed"
        ? `[/${tag}] не закрывает [${tag}] снаружи скрытого текста, перенесите его за [/private]`
        : `[/${tag}] ничего не закрывает, уберите его или откройте [${tag}] выше`;
    case "too-deep":
      return "теги вложены глубже 512 уровней, такой текст покажется без оформления";
  }
}

/**
 * Check BBCode the way the server reads it, and say what is wrong where.
 *
 * Silent on every correct spelling, on everything the editor writes itself,
 * and on whatever stands inside a closed [noparse]. A spelling the server
 * shows as text is named by its reason (BBCodeBrokenReason), and so are an
 * address the page refuses, a tag left open, a closing tag with nothing to
 * close and a hidden block inside another. The text is read as the server
 * reads it, without the characters that reorder text for display; taking them
 * out moves no line break, so the lines named are the author's.
 *
 * Given a context, the check reads the tags of that surface, as the page does:
 * a tag the surface does not have ([mod] in a game post) is text there, and
 * nothing to warn about. A hidden block is read on every surface all the same,
 * as the save path reads it, and on a surface that has none the check names
 * the first token of one, which the save path refuses: the server's reason,
 * said before the author presses send. Without a context every tag is read.
 *
 * @example
 * validateBBCode("[b]текст[/b]")           // => []
 * validateBBCode('[img width=300]...[/img]') // => [{ reason: "unquoted-value", ... }]
 * validateBBCode("[private]x[/private]", "common") // => [{ reason: "private-outside-game", ... }]
 */
export function validateBBCode(
  bbcode: string,
  context?: BBCodeContext,
): BBCodeIssue[] {
  const text = asTheServerReads(bbcode ?? "");
  const raw: RawIssue[] = [];
  const privateOutside =
    context !== undefined && !CONTEXT_TAGS[context].includes("private");
  const tags =
    context === undefined
      ? ALL_TAGS
      : new Set([...CONTEXT_TAGS[context], "private"]);
  readText(text, tags, raw, privateOutside);
  const lineStarts = [0];
  for (let i = text.indexOf("\n"); i >= 0; i = text.indexOf("\n", i + 1)) {
    lineStarts.push(i + 1);
  }
  const lineOf = (at: number): number => {
    let low = 0;
    let high = lineStarts.length - 1;
    while (low < high) {
      const mid = (low + high + 1) >> 1;
      if (lineStarts[mid] <= at) low = mid;
      else high = mid - 1;
    }
    return low + 1;
  };
  return raw
    .sort((a, b) => a.at - b.at)
    .map((issue) => {
      const line = lineOf(issue.at);
      const words = wordIssue(issue);
      return {
        reason: issue.reason,
        line,
        text: words,
        message: `Строка ${line}: ${words}`,
      };
    });
}

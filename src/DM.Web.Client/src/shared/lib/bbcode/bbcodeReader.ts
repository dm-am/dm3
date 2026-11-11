// The reader of the markup by the server's grammar; content tags are read by
// bbcodeContentTags.ts.

import {
  MAX_DEPTH,
  type BbNode,
  type ContainerTag,
  type TagName,
} from "./bbcodeTagSet";
import {
  asciiLower,
  closingBrackets,
  findCloser,
  isLetter,
  isSpace,
  readOpening,
  readPrivateOpening,
} from "./bbcodeSpelling";
import { readContentTag } from "./bbcodeContentTags";
import type { RawIssue } from "./bbcodeWarnings";

/** A tag the reader has entered and not left yet. */
export interface Frame {
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
  /** The first tag met inside: a [tip] holding a node is the tag as typed. */
  inner?: string;
}

class TooDeep extends Error {}

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
 * One pass, one stack of open tags. A name is a tag only before "]", "=", a
 * space or the end; names compare in A-Z case and keep their typed spelling. A
 * tag is markup only once its closing tag ends it: left open, it prints as
 * typed, and a closing tag with nothing to close is text. [code] only changes
 * the font. The exception is [private]: left open it hides to the end, since a
 * doubt about hiding is decided towards hiding more.
 */
class Reader {
  private readonly closes: Int32Array;
  private readonly stack: Frame[];
  private pos = 0;
  /** The last search for the closing tag of an example. */
  private closer: { from: number; at: number } | null = null;
  /**
   * Where the first unclosed [noparse] stands: every hidden-block token after
   * it is refused on save, to the end of the text, since the save path reads
   * no other tag (PrivateBlockMarkup).
   */
  private leftOpen: number | null = null;
  /** The first token of a hidden block has been named as off its surface. */
  private outsideNamed = false;

  /**
   * @param privateOutside The surface has no hidden block, and its first token
   *   is named, as the save refuses it (PrivateBlockMarkup.DescribeSurfaceRefusal).
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
    if (!(
      follow === undefined ||
      follow === "]" ||
      follow === "=" ||
      isSpace(follow)
    )) {
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

  /** An example: its content as typed, ended only by its own closing tag. */
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
   * Where an example's closing tag stands from a position on, or -1, searched
   * forward once however many openings are left unclosed.
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
   * Closes the nearest open tag of its name and leaves what opened inside it
   * unclosed; never past a [private]. Nothing to close: text as typed.
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

    if (tag === "tip" || tag === "img" || tag === "link" || tag === "mention") {
      const read = readContentTag(frame, close);
      if ("reason" in read) {
        // The server refuses the tag and prints it as typed, what it holds in
        // its place: the author and the reader see what did not work instead
        // of an empty place, and the check says why.
        this.report({ ...read, at: frame.at, tag });
        flatten(into, frame.open, frame.children, close);
      } else {
        into.push(read);
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
export function readText(
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

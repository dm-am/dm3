/**
 * Tags whose content is one value (an address, a login, a [tip] word), read at
 * their closing tag as the server's ContentTag does; one holding no value of
 * its kind is printed as typed.
 */

import type { BbNode } from "./bbcodeTagSet";
import { hasWhiteSpace, trimWhiteSpace } from "./bbcodeSpelling";
import { isSafeAddress, tipImageAddress } from "./bbcodeAddresses";
import type { Frame } from "./bbcodeReader";
import type { RawIssue } from "./bbcodeWarnings";

/** Why a tag with content is not markup, as the markup check names it. */
type Refusal = Pick<RawIssue, "reason" | "case">;

/**
 * Read a tag with content at its own closing tag: the node it becomes, or why
 * the server prints it as typed.
 */
export function readContentTag(frame: Frame, close: string): BbNode | Refusal {
  return frame.tag === "tip" ? readTip(frame, close) : readValue(frame, close);
}

/**
 * A [tip] holds a word on one line and shows a text, a picture or both (TipTag):
 * refused for a tag or a line break in the word, an address the page refuses,
 * or nothing to show, asked in the server's order.
 */
function readTip(frame: Frame, close: string): BbNode | Refusal {
  const word = contentOf(frame);
  if (
    frame.inner !== undefined ||
    !trimWhiteSpace(word) ||
    /[\r\n]/.test(word)
  ) {
    return {
      reason: "bad-content",
      case: frame.inner === "tip" ? "nested" : undefined,
    };
  }

  const written = frame.params.get("text");
  const text =
    written !== undefined && trimWhiteSpace(written) ? written : null;
  const address = frame.params.get("img");
  const img = address === undefined ? null : tipImageAddress(address);
  if (address !== undefined && img === null) {
    const lower = trimWhiteSpace(address).toLowerCase();
    return {
      reason: "bad-address",
      case: /^(https?:\/\/|\/)/.test(lower) ? undefined : "scheme",
    };
  }
  if (text === null && img === null) return { reason: "missing-param" };
  return {
    t: "tip",
    word,
    text,
    img,
    source: frame.open + word + close,
  };
}

/** An image, a link or a mention holds one address or login, one the server shows. */
function readValue(frame: Frame, close: string): BbNode | Refusal {
  const only = frame.children;
  if (only.length > 1 || (only.length === 1 && only[0].t !== "text")) {
    return { reason: "bad-content" };
  }
  const content = contentOf(frame);
  const source = frame.open + content + close;
  const value = trimWhiteSpace(content);

  if (frame.tag === "mention") {
    if (!value || /[[\r\n]/.test(value)) return { reason: "bad-content" };
    return { t: "mention", login: value, source };
  }

  if (!value || hasWhiteSpace(value)) return { reason: "bad-content" };
  if (!isSafeAddress(value)) return refusedAddress(value);
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

/**
 * An address the server refuses: it prints the tag as typed, the address in
 * its place, and the check says whether the scheme is what it refused.
 */
function refusedAddress(address: string): Refusal {
  const lower = address.toLowerCase();
  return {
    reason: "bad-address",
    case:
      lower.startsWith("http://") || lower.startsWith("https://")
        ? undefined
        : "scheme",
  };
}

/** The text a tag holds when it holds nothing but text, and "" otherwise. */
function contentOf(frame: Frame): string {
  const only = frame.children;
  return only.length === 1 && only[0].t === "text" ? only[0].s : "";
}

// The tag set of the editor's converter: the tags of each surface, their
// parameters, and the tree a text becomes once read.

/**
 * The surfaces of the server: common (comments and prose fields), post, info
 * (profiles, descriptions), message (the global chat), direct (private
 * messages, without [mod]) and hint (a game tag's description: [tip] only, no
 * visual mode, previewed by hintToHtml).
 */
export type BBCodeContext =
  "common" | "post" | "info" | "message" | "direct" | "hint";

/** The tags of the grammar. */
export type TagName =
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

/** The tags each context reads as markup; [noparse] everywhere, [tip] in the hint alone. */
export const CONTEXT_TAGS: Record<BBCodeContext, readonly string[]> = {
  common: [...BASE_TAGS, "mod"],
  post: [...BASE_TAGS, "private"],
  info: [...BASE_TAGS],
  message: [...BASE_TAGS, "mod"],
  direct: [...BASE_TAGS],
  hint: ["tip", "noparse"],
};

/** Every tag of the content surfaces: the markup check and a caller with no context. */
export const ALL_TAGS: ReadonlySet<string> = new Set([
  ...BASE_TAGS,
  "mod",
  "private",
]);

/** A tag missing here takes none. An image has no alt: text nobody sees hides insults. */
export const TAG_PARAMS: Partial<Record<TagName, readonly string[]>> = {
  spoiler: ["text"],
  img: ["width", "height"],
  link: ["text"],
  tip: ["text", "img"],
};

/** Parameters whose value is a size in pixels. */
export const NUMBER_PARAMS: ReadonlySet<string> = new Set(["width", "height"]);

/**
 * The largest size in pixels an image takes. A size is a bound, and no screen
 * is this wide, so a larger one would bound nothing. The server holds the same
 * number (ParameterLimits.MaxPixels), ClientFieldLimitsShould holds the two equal.
 */
export const MAX_PIXELS = 9999;

/** A size in pixels: a whole number from 1 to MAX_PIXELS, written without zeros ahead. */
export function isPixels(value: string): boolean {
  return /^[1-9]\d*$/.test(value) && Number(value) <= MAX_PIXELS;
}

/** The tags drawn as a block of their own in the visual mode. */
export const BLOCK_TAGS: ReadonlySet<string> = new Set([
  "spoiler",
  "nsfw",
  "quote",
  "mod",
  "ul",
  "ol",
  "li",
]);

/** Deeper is refused and shown as typed, as BbParser.TreeMaxDepth does. */
export const MAX_DEPTH = 512;

/** A tag that holds other nodes and has nothing of its own but its caption. */
export type ContainerTag =
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
 * A text once read; a spelling that is not markup is text. open and close keep
 * a tag as typed for the way back.
 */
export type BbNode =
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

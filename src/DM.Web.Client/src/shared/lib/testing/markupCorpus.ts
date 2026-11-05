/**
 * The server's corpus for the generated checks (markupCorpus.golden.json, by
 * BbMarkupCorpusShould), and its rules written out from BBCODE_RENDERING.md
 * rather than taken from the converter, so a rule it got wrong fails.
 * Spec-only; docs/guides/TESTING.md, "Генеративные проверки разметки".
 */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { type BBCodeContext } from "@/shared/lib/bbcode/bbcodeTagSet";
import { type BBCodeIssue } from "@/shared/lib/bbcode/bbcodeWarnings";

// ============================================================================
// SURFACES
// ============================================================================

export type MarkupSurface =
  | "GamePost"
  | "Comment"
  | "GlobalChatMessage"
  | "Profile"
  | "DirectMessage";

/** The editor context each surface of the server is written in. */
export const CONTEXT_OF: Record<MarkupSurface, BBCodeContext> = {
  GamePost: "post",
  Comment: "common",
  GlobalChatMessage: "message",
  Profile: "info",
  DirectMessage: "direct",
};

// ============================================================================
// THE CORPUS
// ============================================================================

/** One text of the corpus and the server's answers about it. */
export interface CorpusEntry {
  surface: MarkupSurface;
  /** Generated as markup the grammar accepts whole. */
  valid: boolean;
  input: string;
  /** What its author reads on the page; null for a text too deep to render. */
  display: string | null;
  /** Why the save path of the surface refuses the text, or null. */
  refusal: string | null;
  /** The character, counted from 1, the refusal stands at, or null with none. */
  refusalAt: number | null;
}

/** Which tags the page draws with one address between their halves. */
export interface AddressVerdict {
  address: string;
  link: boolean;
  img: boolean;
  mention: boolean;
}

/** The corpus as the server last wrote it. */
export function readCorpus(): CorpusEntry[] {
  return readCorpusFile().entries;
}

/** What the server answers about every address of the alphabet. */
export function readAddressVerdicts(): AddressVerdict[] {
  return readCorpusFile().addresses;
}

function readCorpusFile(): {
  entries: CorpusEntry[];
  addresses: AddressVerdict[];
} {
  // DM_MARKUP_CORPUS names another corpus, one the server wrote to search
  // beyond this one (docs/guides/TESTING.md); "write" is the server's own word
  // for writing this one, and means nothing here.
  const other = process.env.DM_MARKUP_CORPUS;
  const file =
    other && other !== "write"
      ? other
      : join(
          dirname(fileURLToPath(import.meta.url)),
          "markupCorpus.golden.json",
        );
  return JSON.parse(readFileSync(file, "utf8")) as {
    entries: CorpusEntry[];
    addresses: AddressVerdict[];
  };
}

// ============================================================================
// WHAT BOTH SIDES READ
// ============================================================================

/**
 * The text without what both sides take out before reading it, a surrogate
 * with no partner as U+FFFD: written out from BBCODE_RENDERING.md rather than
 * taken from the converter, so a range the converter got wrong is a failure.
 */
export function asBothSidesRead(text: string): string {
  let out = "";
  for (let i = 0; i < text.length; i++) {
    const code = text.charCodeAt(i);
    const removed =
      (code <= 0x1f && code !== 0x09 && code !== 0x0a && code !== 0x0d) ||
      (code >= 0x7f && code <= 0x9f) ||
      (code >= 0x202a && code <= 0x202e) ||
      (code >= 0x2066 && code <= 0x2069);
    if (removed) continue;
    const high = code >= 0xd800 && code <= 0xdbff;
    const next = text.charCodeAt(i + 1);
    if (high && next >= 0xdc00 && next <= 0xdfff) {
      out += text[i] + text[i + 1];
      i++;
      continue;
    }
    out += code >= 0xd800 && code <= 0xdfff ? "\uFFFD" : text[i];
  }
  return out;
}

/**
 * Whether the save refuses a text for this finding: every finding about
 * private text, less a closing tag with a value, and a text nested too deep
 * (IBbCodeNestingLimit).
 */
export function refusesSave(issue: BBCodeIssue): boolean {
  switch (issue.reason) {
    case "private-nested":
    case "private-outside-game":
    case "too-deep":
      return true;
    case "private-spelling":
      return !issue.text.includes("не закрывает скрытый блок");
    case "stray-closing":
      return issue.text.startsWith("[/private]");
    default:
      return false;
  }
}

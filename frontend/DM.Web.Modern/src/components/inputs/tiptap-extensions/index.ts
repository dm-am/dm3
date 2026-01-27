/**
 * BBCode Tiptap Extensions
 *
 * Custom Tiptap extensions for BBCode-specific elements.
 * All extensions use data-bb-* attributes for lossless round-trip conversion.
 */

export { Spoiler, type SpoilerOptions } from "./Spoiler";
export { Nsfw, type NsfwOptions } from "./Nsfw";
export { ModBlock, type ModBlockOptions } from "./ModBlock";
export { WarningBlock, type WarningBlockOptions } from "./WarningBlock";
export { BbTab, type BbTabOptions } from "./BbTab";
export { CutMarker, type CutOptions } from "./Cut";
export { BbQuote, type BbQuoteOptions } from "./BbQuote";
export { Private, type PrivateOptions } from "./Private";
export { Noparse, type NoparseOptions } from "./Noparse";
export { BbLink, type BbLinkOptions } from "./BbLink";
export { BbImage, type BbImageOptions } from "./BbImage";

// Re-export all for convenience
import { Spoiler } from "./Spoiler";
import { Nsfw } from "./Nsfw";
import { ModBlock } from "./ModBlock";
import { WarningBlock } from "./WarningBlock";
import { BbTab } from "./BbTab";
import { CutMarker } from "./Cut";
import { BbQuote } from "./BbQuote";
import { Private } from "./Private";
import { Noparse } from "./Noparse";
import { BbLink } from "./BbLink";
import { BbImage } from "./BbImage";

/**
 * All BBCode extensions bundled together
 */
export const BBCodeExtensions = [
  Spoiler,
  Nsfw,
  ModBlock,
  WarningBlock,
  BbTab,
  CutMarker,
  BbQuote,
  Private,
  Noparse,
  BbLink,
  BbImage,
];

export default BBCodeExtensions;

/**
 * BBCode ↔ HTML Lossless Conversion Utilities
 *
 * Uses data-bb-* attributes to preserve semantic information during round-trip conversion.
 * This enables WYSIWYG editing while storing content as BBCode.
 *
 * Supported tags (DM2 compatible + DM3 extensions):
 * - Formatting: [b], [i], [u], [strike], [code]
 * - Blocks: [spoiler], [nsfw], [quote], [noparse], [mod]
 * - Lists: [ul], [ol], [li]
 * - Media: [img]URL[/img]
 * - Links: [link]URL[/link] (displays "ссылка"), [link=text]URL[/link] (displays custom text)
 * - Special: [tab], [private]
 *
 * @module bbcode
 * @version 3.2.0 - Precompiled regex, pattern validation, performance metrics
 */

// ============================================================================
// TYPES
// ============================================================================

/** BBCode context determines which tags are available */
export type BBCodeContext = "common" | "post" | "info" | "message";

/** Options for BBCode ↔ HTML conversion */
interface ConversionOptions {
  /** Preserve whitespace in code blocks */
  preserveCodeWhitespace?: boolean;
  /** Sanitize URLs for security */
  sanitizeUrls?: boolean;
}

// ============================================================================
// PHASE STATE TYPES
// ============================================================================

/**
 * Protected block extracted from BBCode.
 * These blocks must NOT be processed by intermediate phases.
 */
interface ProtectedBlock {
  /** Original content inside the block */
  content: string;
  /** The BBCode tag type */
  tag: "noparse" | "code";
}

/**
 * State object passed between bbcodeToHtml phases.
 * Immutable pattern: each phase returns a NEW state object.
 */
interface BbcodeToHtmlState {
  /** Current HTML/intermediate representation being processed */
  html: string;
  /** Extracted noparse blocks, indexed by placeholder number */
  noparseBlocks: string[];
  /** Extracted code blocks with tag info */
  codeBlocks: ProtectedBlock[];
  /** Original conversion options */
  options: ConversionOptions;
}

/**
 * State object passed between htmlToBbcode phases.
 */
interface HtmlToBbcodeState {
  /** Current BBCode/intermediate representation being processed */
  bbcode: string;
  /** Original conversion options */
  options: ConversionOptions;
}

/**
 * Phase function signature for bbcodeToHtml pipeline.
 * Pure function: takes state, returns new state.
 */
type BbcodeToHtmlPhase = (state: BbcodeToHtmlState) => BbcodeToHtmlState;

/**
 * Phase function signature for htmlToBbcode pipeline.
 * Pure function: takes state, returns new state.
 */
type HtmlToBbcodePhase = (state: HtmlToBbcodeState) => HtmlToBbcodeState;

// ============================================================================
// CONSTANTS
// ============================================================================

import { DEFAULT_LINK_TEXT } from "./bbcodeConstants";

/** Base tags available in all contexts */
const BASE_TAGS = [
  "b",
  "i",
  "strike",
  "u",
  "code",
  "spoiler",
  "nsfw",
  "ul",
  "ol",
  "img",
  "link",
  "quote",
  "tab",
  "noparse",
  "mention",
];

/**
 * Tags available per context
 *
 * Contexts:
 * - common: General use, includes [mod] for moderated areas
 * - post: Game posts, includes [private] for character-specific messages
 * - info: Game/user info pages
 * - message: Chat and private conversations, includes [mod]
 */
export const CONTEXT_TAGS: Record<BBCodeContext, string[]> = {
  common: [...BASE_TAGS, "mod", "warning"],
  post: [...BASE_TAGS, "private"],
  info: [...BASE_TAGS],
  message: [...BASE_TAGS, "mod", "warning"],
};

// ============================================================================
// PRECOMPILED REGEX PATTERNS
// ============================================================================
// Patterns are compiled once at module load for better performance.
// ~10-20% faster than creating regex on each function call.

/** Patterns for BBCode → HTML conversion (Phase 1: extract) */
const BB_EXTRACT = {
  noparse: /\[noparse\]([\s\S]*?)\[\/noparse\]/gi,
  code: /\[code\]([\s\S]*?)\[\/code\]/gi,
} as const;

/**
 * Patterns for BBCode → HTML conversion (Phase 3: convert)
 *
 * IMPORTANT: Pattern order matters for overlapping patterns!
 * More specific patterns MUST come before less specific ones.
 * See PATTERN_ORDER_RULES for enforced ordering.
 */
const BB_TO_HTML = {
  // Basic formatting
  bold: /\[b\]([\s\S]*?)\[\/b\]/gi,
  italic: /\[i\]([\s\S]*?)\[\/i\]/gi,
  underline: /\[u\]([\s\S]*?)\[\/u\]/gi,
  strike: /\[strike\]([\s\S]*?)\[\/strike\]/gi,

  // Lists
  ul: /\[ul\]([\s\S]*?)\[\/ul\]/gi,
  ol: /\[ol\]([\s\S]*?)\[\/ol\]/gi,
  li: /\[li\]([\s\S]*?)\[\/li\]/gi,

  // Blocks (NOTE: [spoiler=X] is NOT supported - only simple spoiler)
  spoiler: /\[spoiler\]([\s\S]*?)\[\/spoiler\]/gi,
  nsfw: /\[nsfw\]([\s\S]*?)\[\/nsfw\]/gi,
  mod: /\[mod\]([\s\S]*?)\[\/mod\]/gi,
  warning: /\[warning\]([\s\S]*?)\[\/warning\]/gi,

  // Quote (author parameter is NOT supported - [quote=X] treated as [quote])
  quoteWithAuthor: /\[quote=[^\]]+\]([\s\S]*?)\[\/quote\]/gi,
  quote: /\[quote\]([\s\S]*?)\[\/quote\]/gi,

  // Link (with text MUST be before simple link)
  linkWithText: /\[link=([^\]]+)\]([\s\S]*?)\[\/link\]/gi,
  link: /\[link\]([\s\S]*?)\[\/link\]/gi,

  // Image (more specific patterns MUST be before simple img)
  imgWithSize: /\[img=(\d+)(?:x(\d+))?\]([\s\S]*?)\[\/img\]/gi,
  imgWithAttrs: /\[img\s+([^\]]*)\]([\s\S]*?)\[\/img\]/gi,
  img: /\[img\]([\s\S]*?)\[\/img\]/gi,

  // Private
  private: /\[private=([^\]]+)\]([\s\S]*?)\[\/private\]/gi,

  // Standalone
  tab: /\[tab\]/gi,
  // Legacy stripper: [cut] was an author-driven truncation marker, now
  // removed in favour of the unified <TruncatedContent> height-based
  // truncation. Existing content is rendered with the marker silently dropped.
  cutLegacy: /\[cut\]/gi,
  mention: /\[mention="([^"]+)"\]/gi,
} as const;

/** Patterns for HTML → BBCode conversion (Phase 2: marked elements) */
const HTML_TO_BB_MARKED = {
  noparse: /<span[^>]*data-bb-tag="noparse"[^>]*>([\s\S]*?)<\/span>/gi,

  // Code (multiple formats)
  codePreCode:
    /<pre[^>]*data-bb-tag="code"[^>]*>\s*<code[^>]*>([\s\S]*?)<\/code>\s*<\/pre>/gi,
  codePreClass:
    /<pre[^>]*class="code"[^>]*>\s*<code[^>]*>([\s\S]*?)<\/code>\s*<\/pre>/gi,
  codePre: /<pre[^>]*data-bb-tag="code"[^>]*>([\s\S]*?)<\/pre>/gi,
  codePreClassOnly: /<pre[^>]*class="code"[^>]*>([\s\S]*?)<\/pre>/gi,
  code: /<code[^>]*data-bb-tag="code"[^>]*>([\s\S]*?)<\/code>/gi,

  tab: /<span[^>]*data-bb-tag="tab"[^>]*>[^<]*<\/span>/gi,

  bold: /<strong[^>]*data-bb-tag="b"[^>]*>([\s\S]*?)<\/strong>/gi,
  italic: /<em[^>]*data-bb-tag="i"[^>]*>([\s\S]*?)<\/em>/gi,
  underline: /<u[^>]*data-bb-tag="u"[^>]*>([\s\S]*?)<\/u>/gi,
  strike: /<s[^>]*data-bb-tag="strike"[^>]*>([\s\S]*?)<\/s>/gi,

  ul: /<ul[^>]*data-bb-tag="ul"[^>]*>([\s\S]*?)<\/ul>/gi,
  ol: /<ol[^>]*data-bb-tag="ol"[^>]*>([\s\S]*?)<\/ol>/gi,
  li: /<li[^>]*data-bb-tag="li"[^>]*>([\s\S]*?)<\/li>/gi,

  spoiler: /<div[^>]*data-bb-tag="spoiler"[^>]*>([\s\S]*?)<\/div>/gi,
  nsfw: /<div[^>]*data-bb-tag="nsfw"[^>]*>([\s\S]*?)<\/div>/gi,
  mod: /<div[^>]*data-bb-tag="mod"[^>]*>([\s\S]*?)<\/div>/gi,
  warning: /<div[^>]*data-bb-tag="warning"[^>]*>([\s\S]*?)<\/div>/gi,

  // Quote with author - convert to simple quote
  quoteWithAuthor:
    /<blockquote[^>]*data-bb-tag="quote"[^>]*data-bb-author="[^"]*"[^>]*>(?:<cite>[^<]*<\/cite>)?([\s\S]*?)<\/blockquote>/gi,
  quote: /<blockquote[^>]*data-bb-tag="quote"[^>]*>([\s\S]*?)<\/blockquote>/gi,

  linkSelfref:
    /<a[^>]*href="([^"]*)"[^>]*data-bb-tag="link"[^>]*data-bb-selfref="true"[^>]*>[^<]*<\/a>/gi,
  linkWithText:
    /<a[^>]*href="([^"]*)"[^>]*data-bb-tag="link"[^>]*data-bb-text="([^"]*)"[^>]*>[^<]*<\/a>/gi,
  link: /<a[^>]*href="([^"]*)"[^>]*data-bb-tag="link"[^>]*>([\s\S]*?)<\/a>/gi,

  // Wrapped image (custom size) — full <span class="bb-image-frame">...<img></span>.
  // MUST be matched BEFORE `img` below, otherwise the inner img gets replaced
  // first and the span is left as orphan markup.
  imgWrapped:
    /<span[^>]*class="[^"]*bb-image-frame[^"]*"[^>]*>\s*<img[^>]*\/?>\s*<\/span>/gi,
  // Plain image (default size) — bare <img class="bb-image" data-bb-tag="img" ...>
  img: /<img[^>]*data-bb-tag="img"[^>]*\/?>/gi,

  // Span with data-bb-character is what the editor emits. The server renders
  // the same tag as a div carrying data-bb-addressees, and the author's own
  // view of a post arrives in that form: matching only the span dropped the
  // block on save and published the private text to the whole room.
  private:
    /<span[^>]*data-bb-tag="private"[^>]*data-bb-character="([^"]*)"[^>]*>([\s\S]*?)<\/span>/gi,
  privateBlock:
    /<div[^>]*data-bb-tag="private"[^>]*data-bb-addressees="([^"]*)"[^>]*>([\s\S]*?)<\/div>/gi,
  // Legacy stripper: same reason as BB_TO_HTML.cutLegacy above. Removes any
  // lingering <hr data-bb-tag="cut"> elements from historical content when
  // converting rendered HTML back to BBCode.
  cutLegacy: /<hr[^>]*data-bb-tag="cut"[^>]*\/?>/gi,
  mention:
    /<a[^>]*class="bb-mention"[^>]*data-bb-tag="mention"[^>]*data-bb-user="([^"]*)"[^>]*>[^<]*<\/a>/gi,
} as const;

/** Patterns for HTML → BBCode conversion (Phase 3: unmarked elements) */
const HTML_TO_BB_UNMARKED = {
  strong: /<strong>([\s\S]*?)<\/strong>/gi,
  b: /<b>([\s\S]*?)<\/b>/gi,
  em: /<em>([\s\S]*?)<\/em>/gi,
  i: /<i>([\s\S]*?)<\/i>/gi,
  u: /<u>([\s\S]*?)<\/u>/gi,
  s: /<s>([\s\S]*?)<\/s>/gi,
  strike: /<strike>([\s\S]*?)<\/strike>/gi,
  del: /<del>([\s\S]*?)<\/del>/gi,

  preCode: /<pre>\s*<code>([\s\S]*?)<\/code>\s*<\/pre>/gi,
  pre: /<pre>([\s\S]*?)<\/pre>/gi,
  code: /<code>([\s\S]*?)<\/code>/gi,

  ul: /<ul>([\s\S]*?)<\/ul>/gi,
  ol: /<ol>([\s\S]*?)<\/ol>/gi,
  li: /<li>([\s\S]*?)<\/li>/gi,

  a: /<a[^>]*href="([^"]*)"[^>]*>([\s\S]*?)<\/a>/gi,
  img: /<img[^>]*src="([^"]*)"[^>]*\/?>/gi,

  // Quote with author - convert to simple quote (ignore author)
  blockquoteWithAuthor:
    /<blockquote><div class="quote-author">[^<]*<\/div>([\s\S]*?)<\/blockquote>/gi,
  blockquote: /<blockquote>([\s\S]*?)<\/blockquote>/gi,

  spoiler: /<div class="spoiler">([\s\S]*?)<\/div>/gi,
  nsfw: /<div class="nsfw-spoiler">([\s\S]*?)<\/div>/gi,
  private:
    /<span[^>]*class="private-text"[^>]*data-(?:users|character)="([^"]*)"[^>]*>([\s\S]*?)<\/span>/gi,
  privateBlock:
    /<div[^>]*class="private-message"[^>]*data-bb-addressees="([^"]*)"[^>]*>([\s\S]*?)<\/div>/gi,
} as const;

/** Patterns for structural elements (Phase 4) */
const STRUCTURAL = {
  brMarked: /<br[^>]*data-bb-br="true"[^>]*\/?>/gi,
  br: /<br\s*\/?>/gi,
  trailingP: /<p>\s*\n?$/,
  emptyPBetween: /<\/p>\s*<p><br\s*\/?><\/p>\s*<p>/gi,
  emptyPBetweenSimple: /<\/p>\s*<p><\/p>\s*<p>/gi,
  emptyPWithBr: /<p><br\s*\/?><\/p>/gi,
  emptyP: /<p><\/p>/gi,
  consecutiveP: /<\/p>\s*<p>/gi,
  pBeforeBlock: /<\/p>\s*(\[(?:code|spoiler|nsfw|quote|mod|warning|ul|ol)\])/gi,
  blockBeforeP: /(\[\/(?:code|spoiler|nsfw|quote|mod|warning|ul|ol)\])\s*<p>/gi,
  pAndBlock: /<p>\s*(\[(?:code|spoiler|nsfw|quote|mod|warning|ul|ol)\])/gi,
  consecutiveBlocks:
    /(\[\/(?:code|spoiler|nsfw|quote|mod|warning|ul|ol)\])(\[(?:code|spoiler|nsfw|quote|mod|warning|ul|ol)(?:=[^\]]*)?\])/gi,
  pOpen: /<p>/gi,
  pClose: /<\/p>/gi,
} as const;

/** Patterns for cleanup (Phase 5) */
const CLEANUP = {
  excessiveNewlines: /\n{3,}/g,
  liWhitespace: /\[\/li\]\s*\[li\]/g,
  ulStart: /\[ul\]\s*/g,
  ulEnd: /\s*\[\/ul\]/g,
  olStart: /\[ol\]\s*/g,
  olEnd: /\s*\[\/ol\]/g,
  emptyLi: /\[li\]\s*\[\/li\]/g,
  liContent: /\[li\]\s*([\s\S]*?)\s*\[\/li\]/g,
} as const;

/** Block element detection patterns */
const BLOCK_PATTERNS = {
  open: /^<(?:pre|div|blockquote|ul|ol|hr)[^>]*>/i,
  close: /<\/(?:pre|div|blockquote|ul|ol)>$/i,
} as const;

// ============================================================================
// PATTERN ORDER VALIDATION
// ============================================================================

/**
 * Rules for pattern application order.
 * More specific patterns MUST be applied before less specific ones.
 *
 * Example: [img=WxH]...[/img] must match before [img]...[/img],
 * otherwise [img=100x200] would never match.
 */
const PATTERN_ORDER_RULES: Array<{
  first: keyof typeof BB_TO_HTML;
  then: keyof typeof BB_TO_HTML;
  reason: string;
}> = [
  {
    first: "quoteWithAuthor",
    then: "quote",
    reason: "[quote=X] before [quote] (author ignored)",
  },
  { first: "linkWithText", then: "link", reason: "[link=text] before [link]" },
  {
    first: "imgWithSize",
    then: "imgWithAttrs",
    reason: "[img=WxH] before [img attrs]",
  },
  { first: "imgWithAttrs", then: "img", reason: "[img attrs] before [img]" },
];

/**
 * Validate that pattern order rules are respected in the BB_TO_HTML object.
 * Called once at module load in development mode.
 * @throws Error if patterns are in wrong order
 */
function validatePatternOrder(): void {
  const keys = Object.keys(BB_TO_HTML) as Array<keyof typeof BB_TO_HTML>;

  for (const rule of PATTERN_ORDER_RULES) {
    const firstIndex = keys.indexOf(rule.first);
    const thenIndex = keys.indexOf(rule.then);

    if (firstIndex === -1) {
      throw new Error(
        `[bbcode] Pattern order validation failed: "${rule.first}" not found in BB_TO_HTML`,
      );
    }
    if (thenIndex === -1) {
      throw new Error(
        `[bbcode] Pattern order validation failed: "${rule.then}" not found in BB_TO_HTML`,
      );
    }
    if (firstIndex > thenIndex) {
      throw new Error(
        `[bbcode] Pattern order violation: "${rule.first}" must come before "${rule.then}" (${rule.reason})`,
      );
    }
  }
}

// Validate pattern order in development mode
if (process.env.NODE_ENV === "development" || process.env.NODE_ENV === "test") {
  validatePatternOrder();
}

// ============================================================================
// PERFORMANCE MONITORING
// ============================================================================

/** Performance metrics for development mode */
const PERF_ENABLED = process.env.NODE_ENV === "development";
const PERF_THRESHOLD_MS = 10; // Warn if conversion takes longer than this

/**
 * Measure execution time and warn if too slow (dev mode only)
 */
function measurePerformance<T>(
  name: string,
  inputLength: number,
  fn: () => T,
): T {
  if (!PERF_ENABLED) {
    return fn();
  }

  const start = performance.now();
  const result = fn();
  const duration = performance.now() - start;

  if (duration > PERF_THRESHOLD_MS) {
    console.warn(
      `[bbcode] Slow ${name}: ${duration.toFixed(2)}ms for ${inputLength} chars`,
    );
  }

  return result;
}

// ============================================================================
// UTILITY FUNCTIONS
// ============================================================================

/**
 * Escape HTML special characters to prevent XSS.
 *
 * @param text - Plain text to escape
 * @returns HTML-safe string with &lt;, &gt;, &amp;, &quot;, &#039; entities
 *
 * @example
 * escapeHtml('<script>alert("xss")</script>')
 * // => '&lt;script&gt;alert(&quot;xss&quot;)&lt;/script&gt;'
 */
function escapeHtml(text: string): string {
  return text
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;");
}

/**
 * Unescape HTML entities back to characters.
 *
 * @param text - HTML string with entities
 * @returns Plain text with entities decoded
 *
 * @example
 * unescapeHtml('&lt;b&gt;bold&lt;/b&gt;')
 * // => '<b>bold</b>'
 */
function unescapeHtml(text: string): string {
  return text
    .replace(/&amp;/g, "&")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&#039;/g, "'")
    .replace(/&nbsp;/g, " ");
}

/**
 * Escape attribute value for safe HTML insertion.
 *
 * @param value - Attribute value to escape
 * @returns Safe attribute value
 */
function escapeAttr(value: string): string {
  return value.replace(/"/g, "&quot;").replace(/'/g, "&#039;");
}

/**
 * Dangerous URL protocols that can execute JavaScript or embed data
 */
const DANGEROUS_PROTOCOLS = ["javascript:", "data:", "vbscript:"];

/**
 * Safe URL protocols allowed for links
 */
const SAFE_LINK_PROTOCOLS = ["http:", "https:", "mailto:", "tel:"];

/**
 * Safe URL protocols allowed for images
 */
const SAFE_IMAGE_PROTOCOLS = ["http:", "https:"];

/**
 * Sanitize URL to prevent XSS attacks.
 *
 * Blocks dangerous protocols (javascript:, data:, vbscript:) and
 * optionally restricts to a whitelist of allowed protocols.
 *
 * @param url - URL to sanitize
 * @param allowedProtocols - Whitelist of allowed protocols (default: http, https, mailto, tel)
 * @returns Sanitized URL or '#' if dangerous
 *
 * @example
 * sanitizeUrl('javascript:alert(1)')  // => '#'
 * sanitizeUrl('https://example.com')  // => 'https://example.com'
 * sanitizeUrl('/relative/path')       // => '/relative/path'
 */
export function sanitizeUrl(
  url: string,
  allowedProtocols: string[] = SAFE_LINK_PROTOCOLS,
): string {
  if (!url) return "#";

  const trimmed = url.trim().toLowerCase();

  // Block dangerous protocols
  if (DANGEROUS_PROTOCOLS.some((p) => trimmed.startsWith(p))) {
    return "#";
  }

  // Check for allowed protocols if URL has a protocol
  const colonIndex = trimmed.indexOf(":");
  if (colonIndex !== -1) {
    const slashIndex = trimmed.indexOf("/");

    // Protocol-relative URLs (//example.com) are allowed
    if (trimmed.startsWith("//")) {
      return url.trim();
    }

    // If there's a slash before the colon, it's a relative path (path/to:file)
    if (slashIndex !== -1 && slashIndex < colonIndex) {
      return url.trim();
    }

    // URL has a protocol - check if it's allowed
    const hasAllowedProtocol = allowedProtocols.some((p) =>
      trimmed.startsWith(p),
    );
    if (!hasAllowedProtocol) {
      return "#";
    }
  }

  return url.trim();
}

/**
 * Sanitize image URL (only http/https allowed).
 *
 * @param url - Image URL to sanitize
 * @returns Sanitized URL or '#' if dangerous/unsupported
 *
 * @example
 * sanitizeImageUrl('javascript:alert(1)')  // => '#'
 * sanitizeImageUrl('data:image/png;...')   // => '#'
 * sanitizeImageUrl('https://img.com/a.jpg') // => 'https://img.com/a.jpg'
 */
export function sanitizeImageUrl(url: string): string {
  return sanitizeUrl(url, SAFE_IMAGE_PROTOCOLS);
}

/**
 * Render a single BBCode image node to the unified HTML shape that the
 * frontend parser, backend BbParserWrapper, Tiptap BbImage extension,
 * and every TruncatedContent consumer all agree on.
 *
 * Default size (width === null && height === null):
 *     <img class="bb-image" data-bb-tag="img" src="..." alt="..." ...>
 *
 * Custom size:
 *     <span class="bb-image-frame" data-bb-width="W" data-bb-height="H"
 *           style="--bb-image-max-width:Wpx;--bb-image-max-height:Hpx">
 *       <img class="bb-image" data-bb-tag="img" src="..." alt="..." ...>
 *     </span>
 *
 * The wrapper exists ONLY when size is explicit — default images skip it
 * for a smaller DOM. Sizing flows through CSS custom properties so any
 * ancestor can override via a normal class rule (no !important needed).
 */
export function renderBbImage(
  url: string,
  alt: string,
  size: { width: number | null; height: number | null },
): string {
  const escapedSrc = escapeAttr(url);
  const escapedAlt = escapeAttr(alt);
  const imgTag =
    `<img src="${escapedSrc}" alt="${escapedAlt}" class="bb-image" ` +
    `data-bb-tag="img" loading="lazy" decoding="async" ` +
    `referrerpolicy="no-referrer" />`;

  const { width, height } = size;
  if (width == null && height == null) {
    return imgTag;
  }

  const cssVars: string[] = [];
  const dataAttrs: string[] = [];
  if (width != null) {
    cssVars.push(`--bb-image-max-width:${width}px`);
    dataAttrs.push(`data-bb-width="${width}"`);
  }
  if (height != null) {
    cssVars.push(`--bb-image-max-height:${height}px`);
    dataAttrs.push(`data-bb-height="${height}"`);
  }
  return (
    `<span class="bb-image-frame" ${dataAttrs.join(" ")} ` +
    `style="${cssVars.join(";")}">${imgTag}</span>`
  );
}

// ============================================================================
// PIPELINE EXECUTOR
// ============================================================================

/**
 * Execute a pipeline of phase functions in strict order.
 *
 * @param initialState - Starting state
 * @param phases - Array of phase functions in execution order
 * @returns Final state after all phases
 */
function executePipeline<TState>(
  initialState: TState,
  phases: Array<(state: TState) => TState>,
): TState {
  return phases.reduce((state, phase) => phase(state), initialState);
}

// ============================================================================
// BBCODE → HTML PHASE FUNCTIONS
// ============================================================================

/**
 * Phase 1: Extract protected blocks (noparse, code) and replace with placeholders.
 *
 * Protected content is stored in state arrays and will be restored in Phase 5.
 * Placeholders use format: __NOPARSE_N__ and __CODE_N__
 */
function phase1_extractProtectedBlocks(
  state: BbcodeToHtmlState,
): BbcodeToHtmlState {
  let html = state.html;
  const noparseBlocks: string[] = [];
  const codeBlocks: ProtectedBlock[] = [];

  // Extract [noparse] blocks - content should not be processed
  html = html.replace(BB_EXTRACT.noparse, (_, content) => {
    noparseBlocks.push(content);
    return `__NOPARSE_${noparseBlocks.length - 1}__`;
  });

  // Extract [code] blocks - preserve whitespace
  html = html.replace(BB_EXTRACT.code, (_, content) => {
    codeBlocks.push({ tag: "code", content });
    return `__CODE_${codeBlocks.length - 1}__`;
  });

  return { ...state, html, noparseBlocks, codeBlocks };
}

/**
 * Phase 2: Escape HTML special characters in content.
 *
 * Escapes: & < > " '
 * Placeholders (__NOPARSE_N__, __CODE_N__) are safe - no special chars.
 */
function phase2_escapeHtmlContent(state: BbcodeToHtmlState): BbcodeToHtmlState {
  return { ...state, html: escapeHtml(state.html) };
}

/**
 * Phase 3: Convert BBCode tags to HTML with data-bb markers.
 *
 * Converts all supported BBCode tags with proper marker attributes.
 * Order matters: more specific patterns (e.g., [img=WxH]) before generic ones.
 */
function phase3_convertBbcodeTags(state: BbcodeToHtmlState): BbcodeToHtmlState {
  let html = state.html;

  // Basic formatting with tag preservation
  html = html.replace(BB_TO_HTML.bold, '<strong data-bb-tag="b">$1</strong>');
  html = html.replace(BB_TO_HTML.italic, '<em data-bb-tag="i">$1</em>');
  html = html.replace(BB_TO_HTML.underline, '<u data-bb-tag="u">$1</u>');
  html = html.replace(BB_TO_HTML.strike, '<s data-bb-tag="strike">$1</s>');

  // Lists
  html = html.replace(BB_TO_HTML.ul, '<ul data-bb-tag="ul">$1</ul>');
  html = html.replace(BB_TO_HTML.ol, '<ol data-bb-tag="ol">$1</ol>');
  html = html.replace(BB_TO_HTML.li, '<li data-bb-tag="li">$1</li>');

  // Blocks (NOTE: [spoiler=X] is NOT supported)
  html = html.replace(
    BB_TO_HTML.spoiler,
    '<div class="bb-spoiler" data-bb-tag="spoiler">$1</div>',
  );
  html = html.replace(
    BB_TO_HTML.nsfw,
    '<div class="bb-nsfw" data-bb-tag="nsfw">$1</div>',
  );
  html = html.replace(
    BB_TO_HTML.mod,
    '<div class="bb-mod" data-bb-tag="mod">$1</div>',
  );
  html = html.replace(
    BB_TO_HTML.warning,
    '<div class="bb-warning" data-bb-tag="warning">$1</div>',
  );

  // Quote - author parameter is NOT supported, [quote=X] converts same as [quote]
  html = html.replace(
    BB_TO_HTML.quoteWithAuthor,
    '<blockquote class="bb-quote" data-bb-tag="quote">$1</blockquote>',
  );
  html = html.replace(
    BB_TO_HTML.quote,
    '<blockquote class="bb-quote" data-bb-tag="quote">$1</blockquote>',
  );

  // Links - DM3 format (with text FIRST - more specific pattern)
  html = html.replace(BB_TO_HTML.linkWithText, (_, displayText, url) => {
    const safeUrl = sanitizeUrl(url.trim());
    const escapedUrl = escapeAttr(safeUrl);
    const escapedText = escapeAttr(displayText);
    return `<a href="${escapedUrl}" data-bb-tag="link" data-bb-text="${escapedText}" target="_blank" rel="noopener">${displayText}</a>`;
  });
  html = html.replace(BB_TO_HTML.link, (_, url) => {
    const safeUrl = sanitizeUrl(url.trim());
    const escapedUrl = escapeAttr(safeUrl);
    return `<a href="${escapedUrl}" data-bb-tag="link" data-bb-selfref="true" target="_blank" rel="noopener">${DEFAULT_LINK_TEXT}</a>`;
  });

  // Images — unified contract (shared with backend BbParserWrapper.cs):
  //   - Default size: <img class="bb-image" data-bb-tag="img" src="..." alt="">
  //   - Custom size:  <span class="bb-image-frame" data-bb-width=".." data-bb-height=".."
  //                         style="--bb-image-max-width:Wpx;--bb-image-max-height:Hpx">
  //                     <img class="bb-image" data-bb-tag="img" src="..." alt="">
  //                   </span>
  // No inline max-width/max-height on <img> — sizing flows through CSS
  // custom properties so ancestor rules (TruncatedContent etc.) can
  // override without !important. See _BbcodeContent.sass image contract.

  // With explicit size — e.g. [img=800x600]URL[/img]
  html = html.replace(BB_TO_HTML.imgWithSize, (_, width, height, src) => {
    const safeSrc = sanitizeImageUrl(src.trim());
    return renderBbImage(safeSrc, "", {
      width: parseInt(width, 10),
      height: height ? parseInt(height, 10) : null,
    });
  });

  // With attributes — e.g. [img alt="text"]URL[/img]
  html = html.replace(BB_TO_HTML.imgWithAttrs, (_, attrs, src) => {
    const safeSrc = sanitizeImageUrl(src.trim());
    const altMatch =
      attrs.match(/alt\s*=\s*"([^"]*)"/i) ||
      attrs.match(/alt\s*=\s*'([^']*)'/i);
    const altText = altMatch ? altMatch[1] : "";
    return renderBbImage(safeSrc, altText, { width: null, height: null });
  });

  // Plain — [img]URL[/img]
  html = html.replace(BB_TO_HTML.img, (_, src) => {
    const safeSrc = sanitizeImageUrl(src.trim());
    return renderBbImage(safeSrc, "", { width: null, height: null });
  });

  // Private message
  html = html.replace(BB_TO_HTML.private, (_, character, content) => {
    const escapedCharacter = escapeAttr(character);
    return `<span class="bb-private" data-bb-tag="private" data-bb-character="${escapedCharacter}">${content}</span>`;
  });

  // Standalone tags
  html = html.replace(
    BB_TO_HTML.tab,
    '<span class="bb-tab" data-bb-tag="tab">\u00A0\u00A0\u00A0\u00A0</span>',
  );
  // Silently strip legacy [cut] markers — see note on BB_TO_HTML.cutLegacy.
  html = html.replace(BB_TO_HTML.cutLegacy, "");
  html = html.replace(BB_TO_HTML.mention, (_, username) => {
    const escapedUser = escapeAttr(username);
    const encodedUser = encodeURIComponent(username);
    return `<a class="bb-mention" href="/users/${encodedUser}" data-bb-tag="mention" data-bb-user="${escapedUser}">@${escapeHtml(username)}</a>`;
  });

  return { ...state, html };
}

/**
 * Phase 4: Wrap lines in paragraph tags for proper Tiptap structure.
 *
 * - Splits by newlines
 * - Wraps inline content in <p></p>
 * - Skips lines starting with block elements
 * - Empty lines become empty paragraphs
 *
 * CRITICAL: Must run BEFORE restoreProtectedBlocks because:
 * - Placeholders are single tokens without newlines
 * - Restored content may have newlines that shouldn't be split
 */
function phase4_wrapLinesInParagraphs(
  state: BbcodeToHtmlState,
): BbcodeToHtmlState {
  const lines = state.html.split("\n");
  const processedLines: string[] = [];

  for (const line of lines) {
    const trimmed = line.trim();

    if (!trimmed) {
      processedLines.push("<p></p>");
      continue;
    }

    if (BLOCK_PATTERNS.open.test(trimmed)) {
      processedLines.push(trimmed);
      continue;
    }

    if (BLOCK_PATTERNS.close.test(trimmed)) {
      processedLines.push(trimmed);
      continue;
    }

    processedLines.push(`<p>${trimmed}</p>`);
  }

  let html = processedLines.join("");
  html = html.replace(/(<p><\/p>)+$/, "");

  return { ...state, html };
}

/**
 * Phase 5: Restore protected blocks from placeholders.
 *
 * - Replaces __CODE_N__ with <code data-bb-tag="code">escaped content</code>
 * - Replaces __NOPARSE_N__ with <span class="bb-noparse" data-bb-tag="noparse">escaped content</span>
 *
 * Content is HTML-escaped during restoration.
 */
function phase5_restoreProtectedBlocks(
  state: BbcodeToHtmlState,
): BbcodeToHtmlState {
  let html = state.html;

  state.codeBlocks.forEach((block, i) => {
    const escapedContent = escapeHtml(block.content);
    html = html.replace(
      `__CODE_${i}__`,
      `<code data-bb-tag="code">${escapedContent}</code>`,
    );
  });

  state.noparseBlocks.forEach((content, i) => {
    const escapedContent = escapeHtml(content);
    html = html.replace(
      `__NOPARSE_${i}__`,
      `<span class="bb-noparse" data-bb-tag="noparse">${escapedContent}</span>`,
    );
  });

  return { ...state, html };
}

/**
 * bbcodeToHtml phase pipeline configuration.
 * Phases MUST execute in this exact order.
 */
const BBCODE_TO_HTML_PHASES: BbcodeToHtmlPhase[] = [
  phase1_extractProtectedBlocks,
  phase2_escapeHtmlContent,
  phase3_convertBbcodeTags,
  phase4_wrapLinesInParagraphs,
  phase5_restoreProtectedBlocks,
];

// ============================================================================
// HTML → BBCODE PHASE FUNCTIONS
// ============================================================================

/**
 * Phase 0: Remove trailing empty paragraphs from Tiptap output.
 *
 * Tiptap adds empty <p></p> after block elements for cursor placement.
 * These should not appear in BBCode output.
 */
function phase0_removeTrailingEmptyParagraphs(
  state: HtmlToBbcodeState,
): HtmlToBbcodeState {
  const bbcode = state.bbcode.replace(/(<p>(\s|<br\s*\/?>)*<\/p>)+$/gi, "");
  return { ...state, bbcode };
}

/**
 * Phase 1: Remove Tiptap wrapper tags.
 *
 * Strips leading <p> and trailing </p> from the entire content.
 */
function phase1_removeTiptapWrappers(
  state: HtmlToBbcodeState,
): HtmlToBbcodeState {
  const bbcode = state.bbcode.replace(/^<p>/, "").replace(/<\/p>$/, "");
  return { ...state, bbcode };
}

/**
 * Phase 2: Convert HTML elements with data-bb markers to BBCode.
 *
 * Priority phase - processes elements that were created by bbcodeToHtml.
 * Uses data-bb-tag, data-bb-text, data-bb-selfref, etc. attributes.
 */
function phase2_convertMarkedHtml(state: HtmlToBbcodeState): HtmlToBbcodeState {
  let bbcode = state.bbcode;

  // Noparse - must be first
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.noparse, "[noparse]$1[/noparse]");

  // Code blocks
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.codePreCode, "[code]$1[/code]");
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.codePreClass, "[code]$1[/code]");
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.codePre, "[code]$1[/code]");
  bbcode = bbcode.replace(
    HTML_TO_BB_MARKED.codePreClassOnly,
    "[code]$1[/code]",
  );
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.code, "[code]$1[/code]");

  // Tab
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.tab, "[tab]");

  // Basic formatting
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.bold, "[b]$1[/b]");
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.italic, "[i]$1[/i]");
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.underline, "[u]$1[/u]");
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.strike, "[strike]$1[/strike]");

  // Lists
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.ul, "[ul]$1[/ul]");
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.ol, "[ol]$1[/ol]");
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.li, "[li]$1[/li]");

  // Blocks (NOTE: [spoiler=X] is NOT supported - only simple spoiler)
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.spoiler, "[spoiler]$1[/spoiler]");
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.nsfw, "[nsfw]$1[/nsfw]");
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.mod, "[mod]$1[/mod]");
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.warning, "[warning]$1[/warning]");

  // Quote
  // Quote - author is NOT supported, convert any quote to simple [quote]
  bbcode = bbcode.replace(
    HTML_TO_BB_MARKED.quoteWithAuthor,
    "[quote]$1[/quote]",
  );
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.quote, "[quote]$1[/quote]");

  // Links
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.linkSelfref, (_, href) => {
    const safeHref = sanitizeUrl(href);
    return safeHref === "#" ? "" : `[link]${safeHref}[/link]`;
  });
  // Link with text - unescape text attribute to prevent entity accumulation on round-trip
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.linkWithText, (_, href, text) => {
    const safeHref = sanitizeUrl(href);
    const unescapedText = unescapeHtml(text);
    return safeHref === "#"
      ? unescapedText
      : `[link=${unescapedText}]${safeHref}[/link]`;
  });
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.link, (_, href, text) => {
    const safeHref = sanitizeUrl(href);
    if (text === DEFAULT_LINK_TEXT) {
      return safeHref === "#" ? "" : `[link]${safeHref}[/link]`;
    }
    return safeHref === "#" ? text : `[link=${text}]${safeHref}[/link]`;
  });

  // Images — wrapped form FIRST (otherwise the plain img pattern below would
  // replace the inner <img> and leave the <span class="bb-image-frame">
  // empty). Custom size comes from the wrapper span's data-bb-width /
  // data-bb-height attributes (kept alongside the CSS vars as a parser-
  // friendly mirror of the sizing info).
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.imgWrapped, (match) => {
    const srcMatch = match.match(/src="([^"]*)"/i);
    if (!srcMatch) return "";
    const safeSrc = sanitizeImageUrl(srcMatch[1]);
    if (safeSrc === "#") return "";

    const widthMatch = match.match(/data-bb-width="(\d+)"/i);
    const heightMatch = match.match(/data-bb-height="(\d+)"/i);
    // alt lives on the inner <img>, so we look for it after the opening span
    const altMatch = match.match(/<img[^>]*\balt="([^"]*)"/i);
    const alt = altMatch ? altMatch[1] : "";
    const altPart = alt ? ` alt="${alt}"` : "";

    if (widthMatch && heightMatch) {
      return `[img=${widthMatch[1]}x${heightMatch[1]}${altPart}]${safeSrc}[/img]`;
    }
    if (widthMatch) {
      return `[img=${widthMatch[1]}${altPart}]${safeSrc}[/img]`;
    }
    // Wrapper with only height — rare, fall back to alt-only form
    if (alt) {
      return `[img alt="${alt}"]${safeSrc}[/img]`;
    }
    return `[img]${safeSrc}[/img]`;
  });

  // Plain (default-size) image — bare <img class="bb-image" data-bb-tag="img">.
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.img, (match) => {
    const srcMatch = match.match(/src="([^"]*)"/i);
    if (!srcMatch) return match;

    const safeSrc = sanitizeImageUrl(srcMatch[1]);
    if (safeSrc === "#") return "";

    // Preserve alt attribute if present
    const altMatch = match.match(/alt="([^"]*)"/i);
    if (altMatch && altMatch[1]) {
      return `[img alt="${altMatch[1]}"]${safeSrc}[/img]`;
    }
    return `[img]${safeSrc}[/img]`;
  });

  // Private - unescape character attribute to prevent entity accumulation on
  // round-trip. Two shapes, one rule: the span the editor emits and the div the
  // server renders for the author's own view of a post.
  const toPrivateTag = (_: string, character: string, content: string) =>
    `[private=${unescapeHtml(character)}]${content}[/private]`;
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.private, toPrivateTag);
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.privateBlock, toPrivateTag);
  // Silently strip any stray legacy cut markers — see HTML_TO_BB_MARKED.cutLegacy note.
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.cutLegacy, "");

  // Mentions - unescape username to prevent entity accumulation
  bbcode = bbcode.replace(HTML_TO_BB_MARKED.mention, (_, username) => {
    return `[mention="${unescapeHtml(username)}"]`;
  });

  return { ...state, bbcode };
}

/**
 * Phase 3: Convert unmarked HTML elements to BBCode (fallback).
 *
 * Handles external HTML that wasn't created by bbcodeToHtml:
 * - <strong>, <b> → [b]
 * - <em>, <i> → [i]
 * - etc.
 */
function phase3_convertUnmarkedHtml(
  state: HtmlToBbcodeState,
): HtmlToBbcodeState {
  let bbcode = state.bbcode;

  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.strong, "[b]$1[/b]");
  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.b, "[b]$1[/b]");
  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.em, "[i]$1[/i]");
  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.i, "[i]$1[/i]");
  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.u, "[u]$1[/u]");
  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.s, "[strike]$1[/strike]");
  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.strike, "[strike]$1[/strike]");
  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.del, "[strike]$1[/strike]");

  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.preCode, "[code]$1[/code]");
  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.pre, "[code]$1[/code]");
  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.code, "[code]$1[/code]");

  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.ul, "[ul]$1[/ul]");
  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.ol, "[ol]$1[/ol]");
  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.li, "[li]$1[/li]");

  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.a, (_, href, text) => {
    const safeHref = sanitizeUrl(href.trim());
    const cleanText = text.trim();
    if (safeHref === "#") {
      return cleanText || href.trim();
    }
    if (safeHref === cleanText || cleanText === DEFAULT_LINK_TEXT) {
      return `[link]${safeHref}[/link]`;
    }
    return `[link=${cleanText}]${safeHref}[/link]`;
  });

  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.img, (_, src) => {
    const safeSrc = sanitizeImageUrl(src.trim());
    if (safeSrc === "#") return "";
    return `[img]${safeSrc}[/img]`;
  });

  // Unmarked quote - author is NOT supported, convert to simple [quote]
  bbcode = bbcode.replace(
    HTML_TO_BB_UNMARKED.blockquoteWithAuthor,
    "[quote]$1[/quote]",
  );
  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.blockquote, "[quote]$1[/quote]");

  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.spoiler, "[spoiler]$1[/spoiler]");
  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.nsfw, "[nsfw]$1[/nsfw]");
  // Unmarked private - unescape character name. Two shapes for one tag: the
  // span older content carries and the div the server renders it as today.
  const toPrivateTag = (_: string, character: string, content: string) =>
    `[private=${unescapeHtml(character)}]${content}[/private]`;
  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.private, toPrivateTag);
  bbcode = bbcode.replace(HTML_TO_BB_UNMARKED.privateBlock, toPrivateTag);

  return { ...state, bbcode };
}

/**
 * Phase 4: Convert structural elements (paragraphs, line breaks).
 *
 * Converts:
 * - <br> → \n
 * - </p><p> → \n
 * - <p></p> → \n (empty paragraph = blank line)
 * - Block transitions (BBCode tags before/after paragraphs)
 */
function phase4_convertStructuralElements(
  state: HtmlToBbcodeState,
): HtmlToBbcodeState {
  let bbcode = state.bbcode;

  bbcode = bbcode.replace(STRUCTURAL.brMarked, "\n");
  bbcode = bbcode.replace(STRUCTURAL.br, "\n");
  bbcode = bbcode.replace(STRUCTURAL.trailingP, "");
  bbcode = bbcode.replace(STRUCTURAL.emptyPBetween, "\n\n");
  bbcode = bbcode.replace(STRUCTURAL.emptyPBetweenSimple, "\n\n");
  bbcode = bbcode.replace(STRUCTURAL.emptyPWithBr, "\n");
  bbcode = bbcode.replace(STRUCTURAL.emptyP, "\n");
  bbcode = bbcode.replace(STRUCTURAL.consecutiveP, "\n");
  bbcode = bbcode.replace(STRUCTURAL.pBeforeBlock, "\n$1");
  bbcode = bbcode.replace(STRUCTURAL.blockBeforeP, "$1\n");
  bbcode = bbcode.replace(STRUCTURAL.pAndBlock, "$1");
  bbcode = bbcode.replace(STRUCTURAL.consecutiveBlocks, "$1\n$2");
  bbcode = bbcode.replace(STRUCTURAL.pOpen, "");
  bbcode = bbcode.replace(STRUCTURAL.pClose, "");

  return { ...state, bbcode };
}

/**
 * Phase 5: Final cleanup and normalization.
 *
 * - Unescape HTML entities (&amp; → &, etc.)
 * - Normalize excessive newlines (max 2 consecutive)
 * - Clean up whitespace in lists
 * - Trim leading/trailing whitespace inside block tags
 * - Final trim
 */
function phase5_cleanup(state: HtmlToBbcodeState): HtmlToBbcodeState {
  let bbcode = state.bbcode;

  bbcode = unescapeHtml(bbcode);
  bbcode = bbcode.replace(CLEANUP.excessiveNewlines, "\n\n");
  bbcode = bbcode.replace(CLEANUP.liWhitespace, "[/li][li]");
  bbcode = bbcode.replace(CLEANUP.ulStart, "[ul]");
  bbcode = bbcode.replace(CLEANUP.ulEnd, "[/ul]");
  bbcode = bbcode.replace(CLEANUP.olStart, "[ol]");
  bbcode = bbcode.replace(CLEANUP.olEnd, "[/ol]");
  bbcode = bbcode.replace(CLEANUP.emptyLi, "");
  bbcode = bbcode.replace(CLEANUP.liContent, "[li]$1[/li]");

  // Clean up whitespace inside block tags
  const blockTags = ["spoiler", "nsfw", "quote", "noparse", "code"];
  for (const tag of blockTags) {
    const blockRegex = new RegExp(
      `\\[${tag}(=[^\\]]*)?\\]\\s*([\\s\\S]*?)\\s*\\[\\/${tag}\\]`,
      "gi",
    );
    bbcode = bbcode.replace(blockRegex, (_, attr, content) => {
      const trimmedContent = content.trim();
      return `[${tag}${attr || ""}]${trimmedContent}[/${tag}]`;
    });
  }

  return { ...state, bbcode: bbcode.trim() };
}

/**
 * htmlToBbcode phase pipeline configuration.
 * Phases MUST execute in this exact order.
 */
const HTML_TO_BBCODE_PHASES: HtmlToBbcodePhase[] = [
  phase0_removeTrailingEmptyParagraphs,
  phase1_removeTiptapWrappers,
  phase2_convertMarkedHtml,
  phase3_convertUnmarkedHtml,
  phase4_convertStructuralElements,
  phase5_cleanup,
];

// ============================================================================
// BBCODE → HTML CONVERSION
// ============================================================================

/**
 * Convert BBCode markup to HTML with data-bb markers for lossless round-trip.
 *
 * The output HTML includes `data-bb-*` attributes on each element that preserve
 * the original BBCode tag information. This enables perfect conversion back to
 * BBCode via {@link htmlToBbcode}.
 *
 * @param bbcode - BBCode string to convert
 * @param options - Conversion options
 * @returns HTML string with data-bb-* markers
 *
 * @example Basic formatting
 * ```typescript
 * bbcodeToHtml('[b]Hello[/b]')
 * // => '<p><strong data-bb-tag="b">Hello</strong></p>'
 *
 * bbcodeToHtml('[i]italic[/i] and [u]underline[/u]')
 * // => '<p><em data-bb-tag="i">italic</em> and <u data-bb-tag="u">underline</u></p>'
 * ```
 *
 * @example Links (DM3 format)
 * ```typescript
 * bbcodeToHtml('[link]https://example.com[/link]')
 * // => '<p><a href="https://example.com" data-bb-tag="link" data-bb-selfref="true">ссылка</a></p>'
 *
 * bbcodeToHtml('[link=Click me]https://example.com[/link]')
 * // => '<p><a href="https://example.com" data-bb-tag="link" data-bb-text="Click me">Click me</a></p>'
 * ```
 *
 * @example Nested content
 * ```typescript
 * bbcodeToHtml('[quote][b]Bold quote[/b][/quote]')
 * // => '<blockquote class="bb-quote" data-bb-tag="quote">...'
 * ```
 *
 * @see {@link htmlToBbcode} for reverse conversion
 * @see {@link validateBBCode} for syntax validation
 */
export function bbcodeToHtml(
  bbcode: string,
  options: ConversionOptions = {},
): string {
  if (!bbcode) return "";

  return measurePerformance("bbcodeToHtml", bbcode.length, () => {
    const initialState: BbcodeToHtmlState = {
      html: bbcode,
      noparseBlocks: [],
      codeBlocks: [],
      options,
    };

    const finalState = executePipeline(initialState, BBCODE_TO_HTML_PHASES);
    return finalState.html;
  });
}

// ============================================================================
// HTML → BBCODE CONVERSION
// ============================================================================

/**
 * Convert HTML back to BBCode, using data-bb markers for lossless conversion.
 *
 * Elements with `data-bb-*` attributes are converted back to their original
 * BBCode tags. Elements without markers (from external sources like paste)
 * are converted using sensible defaults.
 *
 * @param html - HTML string to convert (typically from Tiptap editor)
 * @param options - Conversion options
 * @returns BBCode string
 *
 * @example Marked elements (from bbcodeToHtml)
 * ```typescript
 * htmlToBbcode('<strong data-bb-tag="b">Hello</strong>')
 * // => '[b]Hello[/b]'
 *
 * htmlToBbcode('<s data-bb-tag="strike">deleted</s>')
 * // => '[strike]deleted[/strike]'
 * ```
 *
 * @example Unmarked elements (from paste)
 * ```typescript
 * htmlToBbcode('<strong>Hello</strong>')
 * // => '[b]Hello[/b]'
 *
 * htmlToBbcode('<a href="https://example.com">Link</a>')
 * // => '[link=Link]https://example.com[/link]'
 * ```
 *
 * @see {@link bbcodeToHtml} for reverse conversion
 * @see {@link cleanPastedHtml} for cleaning pasted HTML before conversion
 */
export function htmlToBbcode(
  html: string,
  options: ConversionOptions = {},
): string {
  if (!html) return "";

  return measurePerformance("htmlToBbcode", html.length, () => {
    const initialState: HtmlToBbcodeState = {
      bbcode: html,
      options,
    };

    const finalState = executePipeline(initialState, HTML_TO_BBCODE_PHASES);
    return finalState.bbcode;
  });
}

// ============================================================================
// UTILITY FUNCTIONS
// ============================================================================

/**
 * Check if a BBCode tag is available in a given context.
 *
 * @param tag - Tag name to check (case-insensitive)
 * @param context - Context to check against
 * @returns true if tag is available in context
 *
 * @example
 * ```typescript
 * isTagAvailable('private', 'post')    // => true
 * isTagAvailable('private', 'message') // => false
 * isTagAvailable('mod', 'message')     // => true
 * isTagAvailable('mod', 'post')        // => false
 * ```
 */
export function isTagAvailable(tag: string, context: BBCodeContext): boolean {
  const normalizedTag = tag.toLowerCase();
  return CONTEXT_TAGS[context].includes(normalizedTag);
}

/**
 * Strip BBCode tags that aren't available in the given context.
 *
 * Removes tag pairs and standalone tags that don't exist in the context's
 * allowed tags list. Content inside removed tags is preserved.
 *
 * @param bbcode - BBCode string to filter
 * @param context - Context to filter against
 * @returns BBCode with unavailable tags removed (content preserved)
 *
 * @example
 * ```typescript
 * stripUnavailableTags('[private=char]secret[/private] public', 'message')
 * // => ' public' (private not available in message context)
 *
 * stripUnavailableTags('[mod]note[/mod] text', 'post')
 * // => ' text' (mod not available in post context)
 * ```
 */
export function stripUnavailableTags(
  bbcode: string,
  context: BBCodeContext,
): string {
  const availableTags = CONTEXT_TAGS[context];
  const tagPattern = /\[(\/?)([\w]+)(?:=[^\]]+)?\]/g;

  return bbcode.replace(tagPattern, (match, slash, tag) => {
    if (availableTags.includes(tag.toLowerCase())) {
      return match;
    }
    return "";
  });
}

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
 *
 * @see {@link htmlToBbcode} for conversion after cleaning
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

  // Normalize whitespace
  cleaned = cleaned.replace(/\r\n/g, "\n");
  cleaned = cleaned.replace(/\r/g, "\n");
  cleaned = cleaned.replace(/\t/g, " ");
  cleaned = cleaned.replace(/ {2,}/g, " ");
  cleaned = cleaned.replace(/\n{3,}/g, "\n\n");

  // Remove zero-width characters
  cleaned = cleaned.replace(/[\u200B-\u200D\uFEFF]/g, "");

  return cleaned.trim();
}

/**
 * Validate BBCode for balanced tags.
 *
 * Checks that all opening tags have matching closing tags and that
 * tags are properly nested. Does not validate tag content.
 *
 * Content inside [noparse] and [code] blocks is excluded from validation,
 * as these blocks may contain literal BBCode-like text that shouldn't be parsed.
 *
 * @param bbcode - BBCode string to validate
 * @returns Array of error messages (empty if valid)
 *
 * @example
 * ```typescript
 * validateBBCode('[b]bold[/b]')
 * // => []
 *
 * validateBBCode('[b]unclosed')
 * // => ['Unclosed tag: [b]']
 *
 * validateBBCode('[b][i]wrong order[/b][/i]')
 * // => ['Mismatched tags: expected [/i], found [/b]']
 *
 * // noparse content is excluded from validation
 * validateBBCode('[noparse][b]not validated[/noparse]')
 * // => [] (the unclosed [b] inside noparse is ignored)
 * ```
 */
export function validateBBCode(bbcode: string): string[] {
  const errors: string[] = [];
  const openTags: string[] = [];

  // First, replace content inside [noparse] and [code] blocks with placeholders
  // This prevents validation of literal BBCode examples inside these blocks
  let processedBbcode = bbcode;
  processedBbcode = processedBbcode.replace(
    /\[noparse\]([\s\S]*?)\[\/noparse\]/gi,
    "[noparse]__CONTENT__[/noparse]",
  );
  processedBbcode = processedBbcode.replace(
    /\[code\]([\s\S]*?)\[\/code\]/gi,
    "[code]__CONTENT__[/code]",
  );

  const pairedTags = [
    "b",
    "i",
    "u",
    "strike",
    "code",
    "spoiler",
    "nsfw",
    "quote",
    "ul",
    "ol",
    "li",
    "link",
    "private",
    "noparse",
    "img",
    "mod",
    "warning",
  ];
  const standaloneTags = ["tab"];

  const tagPattern = /\[(\/?)(\w+)(=[^\]]+)?\]/g;
  let match;

  while ((match = tagPattern.exec(processedBbcode)) !== null) {
    const [, isClosing, tagName] = match;
    const lowerTag = tagName.toLowerCase();

    if (isClosing) {
      const lastOpen = openTags.pop();
      if (lastOpen !== lowerTag) {
        if (lastOpen) {
          errors.push(
            `Mismatched tags: expected [/${lastOpen}], found [/${lowerTag}]`,
          );
        } else {
          errors.push(`Unexpected closing tag: [/${lowerTag}]`);
        }
      }
    } else if (
      pairedTags.includes(lowerTag) &&
      !standaloneTags.includes(lowerTag)
    ) {
      openTags.push(lowerTag);
    }
  }

  openTags.forEach((tag) => {
    errors.push(`Unclosed tag: [${tag}]`);
  });

  return errors;
}

/**
 * Get plain text content from BBCode (strips all tags).
 *
 * Useful for generating previews or search indexing.
 *
 * @param bbcode - BBCode string
 * @returns Plain text with all tags removed
 *
 * @example
 * ```typescript
 * bbcodeToPlainText('[b]Hello[/b] [i]World[/i]')
 * // => 'Hello World'
 *
 * bbcodeToPlainText('[quote]Some quote[/quote]')
 * // => 'Some quote'
 * ```
 */
export function bbcodeToPlainText(bbcode: string): string {
  if (!bbcode) return "";

  let text = bbcode;

  // Replace [tab] with space before removing tags
  text = text.replace(/\[tab\]/gi, " ");

  // Remove all BBCode tags
  text = text.replace(/\[[\w]+(?:=[^\]]+)?\]/g, "");
  text = text.replace(/\[\/[\w]+\]/g, "");

  // Clean up whitespace
  text = text.replace(/\s+/g, " ").trim();

  return text;
}

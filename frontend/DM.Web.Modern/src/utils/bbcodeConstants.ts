/**
 * BBCode Constants
 *
 * Centralized constants for BBCode-related strings.
 * These are used across bbcode.ts, bbcodeInteractive.ts, and Tiptap extensions.
 *
 * Note: This project is Russian-only. These strings are not meant for i18n,
 * but are centralized here to avoid duplication and ensure consistency.
 */

// ============================================================================
// LINK CONSTANTS
// ============================================================================

/**
 * Default display text for links without custom text.
 * Used when [link]URL[/link] format is used (URL as content, no custom text).
 */
export const DEFAULT_LINK_TEXT = "ссылка";

// ============================================================================
// SPOILER CONSTANTS
// ============================================================================

/** Text shown when spoiler content is hidden */
export const SPOILER_SHOW_TEXT = "Показать содержимое";

/** Text shown when spoiler content is visible */
export const SPOILER_HIDE_TEXT = "Скрыть содержимое";

// ============================================================================
// NSFW CONSTANTS
// ============================================================================

/** Text shown when NSFW content is hidden */
export const NSFW_SHOW_TEXT = "Показать шокирующий контент";

/** Text shown when NSFW content is visible */
export const NSFW_HIDE_TEXT = "Скрыть шокирующий контент";

/** Warning text shown in 18+ overlay */
export const NSFW_WARNING_TEXT =
  "Если вам исполнилось 18 лет и вы готовы к просмотру контента, который может оказаться для вас неприемлемым, нажмите сюда.";

// ============================================================================
// IMAGE CONSTANTS
// ============================================================================

/** Default max width for images (in pixels) - must match backend BbParserWrapper.DefaultMaxWidth */
export const DEFAULT_IMG_MAX_WIDTH = 600;

/** Default max height for images (in pixels) - must match backend BbParserWrapper.DefaultMaxHeight */
export const DEFAULT_IMG_MAX_HEIGHT = 400;

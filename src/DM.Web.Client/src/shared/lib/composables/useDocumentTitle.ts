/**
 * Document title composable
 * @module shared/lib/composables/useDocumentTitle
 *
 * Sets `document.title` for dynamic pages whose title depends on loaded data
 * (profile username, board name, topic title, game name). Static routes get
 * their title from the router `meta.title` + `afterEach` handler instead.
 *
 * The router `afterEach` runs first on navigation and sets a default title from
 * `meta.title`; a page calling this composable on the same tick overrides it,
 * so the composable always wins for dynamic pages.
 *
 * Composition is the same everywhere: the segment that tells two tabs apart
 * first, the wider context after it, the brand last. A browser truncates a tab
 * title from the right, so the leading segment is the one that survives.
 */
import { toValue, watchEffect, type MaybeRefOrGetter } from "vue";

const BRAND = "Dungeon Master";

/**
 * The one separator between title segments: a vertical bar, the sign the design
 * language already uses for navigation strips. It replaced an em dash, which is
 * out of interface copy.
 */
export const TITLE_SEPARATOR = " | ";

/**
 * Joins title segments and drops the empty ones (an entity still loading, a
 * route with no section), so a title never carries a dangling separator. The
 * single place a multi-part title is composed: pages that glued their own put
 * two different separators into one string.
 */
export function joinTitleSegments(
  ...segments: Array<string | null | undefined>
): string {
  return segments
    .map((segment) => segment?.trim())
    .filter((segment): segment is string => !!segment)
    .join(TITLE_SEPARATOR);
}

/** Formats a page title as `"{title} | Dungeon Master"`, or just the brand when empty. */
export function formatDocumentTitle(title: string | null | undefined): string {
  return joinTitleSegments(title, BRAND);
}

/**
 * Reactively sets `document.title` from a title source. Pass a ref, a getter or
 * a plain string. While the source is empty/nullish the title falls back to the
 * brand alone. Cleanup on unmount is not needed — the router `afterEach` sets a
 * default title on the next navigation.
 */
export function useDocumentTitle(
  title: MaybeRefOrGetter<string | null | undefined>,
): void {
  watchEffect(() => {
    document.title = formatDocumentTitle(toValue(title));
  });
}

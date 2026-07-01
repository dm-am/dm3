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
 */
import { toValue, watchEffect, type MaybeRefOrGetter } from "vue";

const BRAND = "DM.AM";

/** Formats a page title as `"{title} — DM.AM"`, or just the brand when empty. */
export function formatDocumentTitle(title: string | null | undefined): string {
  const trimmed = title?.trim();
  return trimmed ? `${trimmed} — ${BRAND}` : BRAND;
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

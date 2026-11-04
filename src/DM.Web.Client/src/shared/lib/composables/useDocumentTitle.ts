/**
 * Sets document.title for a page whose title depends on loaded data, after the
 * router's afterEach has set the default. The segment that tells two tabs apart
 * comes first: a browser truncates from the right.
 */
import { toValue, watchEffect, type MaybeRefOrGetter } from "vue";

const BRAND = "Dungeon Master";

/** The vertical bar of the navigation strips; an em dash is out of interface copy. */
export const TITLE_SEPARATOR = " | ";

/** Joins title segments without the empty ones, so no separator dangles. */
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
 * Reactively sets document.title from a ref, a getter or a string; empty falls
 * back to the brand. The next navigation resets it, so no cleanup.
 */
export function useDocumentTitle(
  title: MaybeRefOrGetter<string | null | undefined>,
): void {
  watchEffect(() => {
    document.title = formatDocumentTitle(toValue(title));
  });
}

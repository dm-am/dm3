import type { LocationQueryValue } from "vue-router";
import { permalinkOrigin } from "@/shared/config/site";

/**
 * The canonical link to one comment: this page, its page number, the anchor.
 *
 * Only the page number is carried over from the query. The sort and the active
 * search, author and date filters are the sharer's view of the list, not the
 * recipient's — a link that keeps them lands on a page where the comment may
 * not be. The page number has to stay: without it the link opens the first page
 * and the anchor finds nothing.
 *
 * Two buttons build this link — the reader's "copy link" on the comment and the
 * prefill of the moderator's warning — and they have to agree, because the
 * warning quotes the link the reader would have copied.
 */
export function commentPermalink(
  id: string,
  pageNumber: LocationQueryValue | LocationQueryValue[] | undefined,
): string {
  const search = pageNumber ? `?number=${String(pageNumber)}` : "";
  return (
    permalinkOrigin() + window.location.pathname + search + `#comment-${id}`
  );
}

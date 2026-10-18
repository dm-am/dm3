import { stringifyQuery, type LocationQuery } from "vue-router";
import { permalinkOrigin } from "@/shared/config/site";

/**
 * Where the number of a comment in the moderator's warning window leads: this
 * page, the whole query, the anchor.
 *
 * The whole query, as the number of the comment keeps it (CommentItem,
 * numberTo): the page number of a filtered or re-sorted list is a page of that
 * view, and the same page of the whole list holds other comments. It is
 * written by the router's own serializer, so it is the address the number
 * links to.
 */
export function commentPermalink(id: string, query: LocationQuery): string {
  const search = stringifyQuery(query);
  return (
    permalinkOrigin() +
    window.location.pathname +
    (search ? `?${search}` : "") +
    `#comment-${id}`
  );
}

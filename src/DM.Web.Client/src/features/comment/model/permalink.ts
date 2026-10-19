import { stringifyQuery, type LocationQuery } from "vue-router";
import { permalinkOrigin } from "@/shared/config/site";

// The whole query, written by the router's own serializer: the address the number links to.
export function commentPermalink(id: string, query: LocationQuery): string {
  const search = stringifyQuery(query);
  return (
    permalinkOrigin() +
    window.location.pathname +
    (search ? `?${search}` : "") +
    `#comment-${id}`
  );
}

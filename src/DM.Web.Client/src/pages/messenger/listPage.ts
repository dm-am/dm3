import type { LocationQuery } from "vue-router";

/**
 * The page of the list of conversations in the address (?number): every move
 * inside the messenger carries it, or the list would fall back to its first
 * page under the reader.
 */
const LIST_PAGE_KEY = "number";

export function listPageQuery(query: LocationQuery): Record<string, string> {
  const page = query[LIST_PAGE_KEY];
  return typeof page === "string" && page ? { [LIST_PAGE_KEY]: page } : {};
}

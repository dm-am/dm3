import type { LocationQuery } from "vue-router";

/**
 * The page of the list of conversations in the address: ?number, the key the
 * site's paging writes. The list stands beside every correspondence and reads
 * its page from the address, where none means the first one, so a move inside
 * the messenger that left the page out would turn the list back to its first
 * page under the reader. Every such move carries it: the row of a
 * conversation, the link of a message, a day of the calendar and the way back
 * from it, the search of a person to write to.
 */
export const LIST_PAGE_KEY = "number";

/** The page of the list the address names, alone: what a move carries on. */
export function listPageQuery(query: LocationQuery): Record<string, string> {
  const page = query[LIST_PAGE_KEY];
  return typeof page === "string" && page ? { [LIST_PAGE_KEY]: page } : {};
}

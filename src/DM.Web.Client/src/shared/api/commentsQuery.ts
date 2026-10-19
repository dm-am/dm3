// One shape and one wire conversion for the comments of a topic, game, blog
// and publication, so a filter the server learns reaches every discussion.

import { pageSkip } from "./pageSkip";

/** Filter, sort and paging of a comments list, as the client states them. */
export type CommentsQuery = {
  /** 1-based page number; the wire takes skip/take. */
  number?: number;
  /** Page size, from the reader's paging preference. */
  size?: number;
  /** Case-insensitive substring of the comment text. */
  search?: string;
  /** Author usernames, OR-ed. */
  authors?: string[];
  /** ISO 8601 with time. */
  createdFromUtc?: string;
  /** ISO 8601 with time. */
  createdToUtc?: string;
  /** "created" (default) or "likes". */
  sortBy?: string;
  /** "asc" (the default for created) or "desc". */
  sortOrder?: string;
};

/** Page size when the caller states none. */
const DEFAULT_SIZE = 20;

/**
 * The query as wire params. authorUsernames is the API's name for a repeatable
 * username filter; authors stays in the route query, where links already use it.
 */
export function toCommentsQueryParams(
  query: CommentsQuery = {},
): Record<string, string | number | string[] | undefined> {
  const size = query.size ?? DEFAULT_SIZE;
  const params: Record<string, string | number | string[] | undefined> = {
    take: size,
  };

  const skip = pageSkip(query.number ?? 1, size);
  if (skip !== undefined) params.skip = skip;
  if (query.search) params.search = query.search;
  if (query.authors?.length) params.authorUsernames = query.authors;
  if (query.createdFromUtc) params.createdFromUtc = query.createdFromUtc;
  if (query.createdToUtc) params.createdToUtc = query.createdToUtc;
  if (query.sortBy) params.sortBy = query.sortBy;
  if (query.sortOrder) params.sortOrder = query.sortOrder;

  return params;
}

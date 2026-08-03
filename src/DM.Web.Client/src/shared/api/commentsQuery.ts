/**
 * The discussion query, in one place.
 *
 * Comments hang off four records — topic, game, blog, publication — and the
 * four endpoints behind them read the same set: text search, authors, period,
 * sort. The forum client spelled the conversion to skip/take itself while the
 * game and blog clients sent a page number and nothing else, which is the whole
 * reason those two discussions had no search on screen. One shape and one
 * builder, so a filter the server learns reaches every discussion at once.
 */

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

/** The query as wire params: the page number becomes skip/take. */
export function toCommentsQueryParams(
  query: CommentsQuery = {},
): Record<string, string | number | string[] | undefined> {
  const size = query.size ?? DEFAULT_SIZE;
  const params: Record<string, string | number | string[] | undefined> = {
    take: size,
  };

  if (query.number && query.number > 1) params.skip = (query.number - 1) * size;
  if (query.search) params.search = query.search;
  if (query.authors?.length) params.authors = query.authors;
  if (query.createdFromUtc) params.createdFromUtc = query.createdFromUtc;
  if (query.createdToUtc) params.createdToUtc = query.createdToUtc;
  if (query.sortBy) params.sortBy = query.sortBy;
  if (query.sortOrder) params.sortOrder = query.sortOrder;

  return params;
}

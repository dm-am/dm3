/**
 * The rated-posts request, built in one place.
 *
 * Five surfaces ask the posts endpoint the same question — which posts got
 * reviews: /pulse, the two profile subpages, a game's "Оцененные посты" and the
 * moderation worklist. Each used to translate the filter state into query
 * params on its own, and two of the copies had neither the date filters nor
 * paging: they sent one fixed page and drew whatever came back, so the same
 * filter bar answered differently depending on the page it stood on.
 *
 * What a page adds to the reader's filter is its SCOPE — whose posts, of which
 * game. A scope is what the route says the page is about, so it wins over the
 * same filter arriving in the URL.
 */
import gameApi from "../api/gameApi";

/** The filter state the shared pulse filter keeps in the URL. */
export interface PulseSearchParams {
  sortBy?: "rating" | "lastreview" | "reviewcount" | "created";
  sortOrder?: "asc" | "desc";
  search?: string;
  minRating?: number;
  maxRating?: number;
  authorUsernames?: string[];
  createdFrom?: string;
  createdTo?: string;
  gameId?: string;
  number?: number;
  size?: number;
}

/** The query the posts endpoint actually takes. */
export type RatedPostsApiParams = NonNullable<
  Parameters<typeof gameApi.getRatedPosts>[0]
>;

/**
 * Whose rated posts a page is about.
 *
 * "site" is the whole website — /pulse and the moderation worklist narrow the
 * list by time and by nothing else.
 */
export type RatedPostsScope =
  | { kind: "author"; username: string }
  | { kind: "reviewer"; username: string }
  | { kind: "game"; gameId: string }
  | { kind: "site" };

/**
 * Build API params for the rated-posts request.
 * Invalid date strings (e.g. hand-edited URL params) are ignored
 * instead of producing an Invalid Date that throws on toISOString().
 */
export function buildRatedPostsParams(
  params: PulseSearchParams,
  scope: RatedPostsScope = { kind: "site" },
): RatedPostsApiParams {
  const apiParams: RatedPostsApiParams = {
    sortBy: params.sortBy || "lastreview",
    sortOrder: params.sortOrder || "desc",
    hasReviews: true,
  };

  if (params.search) apiParams.search = params.search;
  if (params.minRating !== undefined && params.minRating !== null)
    apiParams.minRating = params.minRating;
  if (params.maxRating !== undefined && params.maxRating !== null)
    apiParams.maxRating = params.maxRating;
  if (params.authorUsernames)
    apiParams.authorUsernames = params.authorUsernames;
  if (params.createdFrom) {
    const createdFromUtc = new Date(params.createdFrom + "T00:00:00Z");
    if (!isNaN(createdFromUtc.getTime()))
      apiParams.createdFromUtc = createdFromUtc.toISOString();
  }
  if (params.createdTo) {
    const createdToUtc = new Date(params.createdTo + "T23:59:59.999Z");
    if (!isNaN(createdToUtc.getTime()))
      apiParams.createdToUtc = createdToUtc.toISOString();
  }
  if (params.gameId) apiParams.gameId = params.gameId;

  // The scope goes last: where the route and the URL name the same thing, the
  // route is the one the reader cannot have typed by accident.
  switch (scope.kind) {
    case "author":
      apiParams.authorUsernames = [scope.username];
      break;
    case "reviewer":
      apiParams.reviewerUsername = scope.username;
      break;
    case "game":
      apiParams.gameId = scope.gameId;
      break;
    case "site":
      break;
  }

  // Pagination
  const pageSize = params.size || 20;
  apiParams.take = pageSize;
  if (params.number && params.number > 1) {
    apiParams.skip = (params.number - 1) * pageSize;
  }

  return apiParams;
}

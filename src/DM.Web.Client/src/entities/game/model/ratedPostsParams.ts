// The rated-posts request of every surface that asks it, so one filter bar answers alike.
import type { RatedPostsQuery } from "./postTypes";

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

/** Whose rated posts a page is about; "site" is the whole website. */
export type RatedPostsScope =
  | { kind: "author"; username: string }
  | { kind: "reviewer"; username: string }
  | { kind: "game"; gameId: string }
  | { kind: "site" };

/** A date edited by hand into the address is ignored rather than thrown on. */
export function buildRatedPostsParams(
  params: PulseSearchParams,
  scope: RatedPostsScope = { kind: "site" },
): RatedPostsQuery {
  const apiParams: RatedPostsQuery = {
    sortBy: params.sortBy || "lastreview",
    sortOrder: params.sortOrder || "desc",
    hasReviews: true,
  };

  if (params.search) apiParams.search = params.search;
  if (params.minRating != null) apiParams.minRating = params.minRating;
  if (params.maxRating != null) apiParams.maxRating = params.maxRating;
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
  }

  const pageSize = params.size || 20;
  apiParams.take = pageSize;
  if (params.number && params.number > 1) {
    apiParams.skip = (params.number - 1) * pageSize;
  }

  return apiParams;
}

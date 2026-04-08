/**
 * Statistics API types
 * Matches backend LiveStats DTO from CommunityStats.cs
 */

/** Statistical value with today's delta */
export interface StatValue {
  /** Current total value */
  value: number;
  /** Change from start of day (UTC) */
  todayDelta: number;
}

/** Total counts with daily deltas */
export interface TotalsWithDelta {
  users: StatValue;
  characters: StatValue;
  games: StatValue;
  gamePosts: StatValue;
  blogs: StatValue;
  publications: StatValue;
}

/** Highlighted post information */
export interface PostHighlight {
  postId: string;
  gameId: string;
  gameTitle: string;
  authorUsername: string;
  ratingSum?: number;
}

/** Live community statistics */
export interface LiveStats {
  /** Number of users currently online */
  online: number;
  /** Total counts with today's changes */
  totals: TotalsWithDelta;
  /** Most recently rated post (PostReview) */
  lastReviewedPost?: PostHighlight;
  /** Best post of the week by rating (sum of PostReview ratings) */
  weeklyBestPost?: PostHighlight;
}

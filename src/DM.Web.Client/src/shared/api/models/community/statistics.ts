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

/** Time period specification (matches backend Period DTO) */
export interface StatsPeriod {
  year: number;
  /** Month 1-12; null for yearly periods */
  month?: number | null;
}

/** Single leaderboard entry (player or game) */
export interface LeaderboardEntry {
  /**
   * Ordinal position (1-based, no shared ranks): entries are ordered by
   * score descending, ties broken by name, and numbered 1..N.
   */
  rank: number;
  /** Entity identifier (user or game GUID) */
  entityId: string;
  /** Short public id for game entries (canonical 5-letter URL id); null for players */
  publicId?: string | null;
  /** Display name (username or game title) */
  name: string;
  /** Score value (rating sum, posts count, or text volume). Always positive. */
  score: number;
}

/**
 * Leaderboards for a period.
 * Matches backend Leaderboards DTO (GET /v1/leaderboards/{year}[/{month}]).
 */
export interface Leaderboards {
  period: StatsPeriod;
  topPlayersByRating: LeaderboardEntry[];
  topGamesByRating: LeaderboardEntry[];
  topPlayersByPosts: LeaderboardEntry[];
  topGamesByPosts: LeaderboardEntry[];
  /** Top players by written text volume ("Самый многопишущий игрок") */
  topPlayersByVolume: LeaderboardEntry[];
  /** Top blogs by rating (sum of likes on the blog's publications) */
  topBlogsByRating?: LeaderboardEntry[];
  /** Top blogs by publications count */
  topBlogsByPosts?: LeaderboardEntry[];
  /** Top blog authors by written text volume (sum of publication content characters) */
  topBlogAuthorsByVolume?: LeaderboardEntry[];
}

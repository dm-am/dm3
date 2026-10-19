/** Matches the API DTOs in StatisticsDtos.cs. */

interface StatValue {
  value: number;
  /** Change since the start of the day (UTC). */
  todayDelta: number;
}

interface TotalsWithDelta {
  users: StatValue;
  characters: StatValue;
  games: StatValue;
  gamePosts: StatValue;
  blogs: StatValue;
  publications: StatValue;
}

interface PostHighlight {
  postId: string;
  gameId: string;
  gameTitle: string;
  authorUsername: string;
  ratingSum?: number;
}

export interface LiveStats {
  online: number;
  totals: TotalsWithDelta;
  /** Best post of the week by the sum of PostReview ratings. */
  weeklyBestPost?: PostHighlight;
}

/** Matches the backend Period DTO. */
interface StatsPeriod {
  year: number;
  /** Month 1-12; null for yearly periods */
  month?: number | null;
}

/** Single leaderboard entry (player or game) */
export interface LeaderboardEntry {
  /** 1-based, no shared ranks: by score descending, ties by name. */
  rank: number;
  entityId: string;
  /** The 5-letter URL id of a game; null for players. */
  publicId?: string | null;
  /** Username or game title. */
  name: string;
  /** Rating sum, posts count or text volume. Always positive. */
  score: number;
}

/** Matches the backend Leaderboards DTO (GET /v1/leaderboards/{year}[/{month}]). */
export interface Leaderboards {
  period: StatsPeriod;
  topPlayersByRating: LeaderboardEntry[];
  topGamesByRating: LeaderboardEntry[];
  topPlayersByPosts: LeaderboardEntry[];
  topGamesByPosts: LeaderboardEntry[];
  /** By written text volume ("Самый многопишущий игрок"). */
  topPlayersByVolume: LeaderboardEntry[];
  /** By the sum of likes on the blog's publications. */
  topBlogsByRating?: LeaderboardEntry[];
  topBlogsByPosts?: LeaderboardEntry[];
  /** By the characters of their publications. */
  topBlogAuthorsByVolume?: LeaderboardEntry[];
}

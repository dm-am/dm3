import type {
  Leaderboards,
  LeaderboardEntry,
} from "@/shared/api/models/community";

/**
 * Leaderboard board definitions — SSOT for both consumers (the full
 * statistics page and the period-digest topics), so titles, kinds and
 * response-field wiring can never drift between them.
 */
export interface LeaderboardBoardDef {
  key: string;
  title: string;
  kind: "player" | "game" | "blog";
  /** Reads this board's entries from the leaderboards response. */
  select: (boards: Leaderboards) => LeaderboardEntry[] | undefined;
  /** Included in the three-card digest-topic teaser (rating boards only). */
  teaser: boolean;
}

export const LEADERBOARD_BOARDS: readonly LeaderboardBoardDef[] = [
  {
    key: "players-by-rating",
    title: "Лучший игрок по сумме оценок",
    kind: "player",
    select: (b) => b.topPlayersByRating,
    teaser: true,
  },
  {
    key: "games-by-rating",
    title: "Лучшая игра по сумме оценок",
    kind: "game",
    select: (b) => b.topGamesByRating,
    teaser: true,
  },
  {
    key: "blogs-by-rating",
    title: "Лучший блог по лайкам",
    kind: "blog",
    select: (b) => b.topBlogsByRating,
    teaser: true,
  },
  {
    key: "players-by-posts",
    title: "Самый активный игрок",
    kind: "player",
    select: (b) => b.topPlayersByPosts,
    teaser: false,
  },
  {
    key: "games-by-posts",
    title: "Самая активная игра",
    kind: "game",
    select: (b) => b.topGamesByPosts,
    teaser: false,
  },
  {
    key: "blogs-by-posts",
    title: "Самый активный блог",
    kind: "blog",
    select: (b) => b.topBlogsByPosts,
    teaser: false,
  },
  {
    key: "players-by-volume",
    title: "Самый многопишущий игрок",
    kind: "player",
    select: (b) => b.topPlayersByVolume,
    teaser: false,
  },
  {
    key: "blog-authors-by-volume",
    title: "Самый многопишущий автор блога",
    kind: "player",
    select: (b) => b.topBlogAuthorsByVolume,
    teaser: false,
  },
];

/** The three rating boards shown by the digest-topic teaser. */
export const TEASER_BOARDS: readonly LeaderboardBoardDef[] =
  LEADERBOARD_BOARDS.filter((b) => b.teaser);

import type {
  Leaderboards,
  LeaderboardEntry,
} from "@/shared/api/models/community";

// One definition for the statistics page and the digest topics, so the two cannot drift.
export interface LeaderboardBoardDef {
  key: string;
  title: string;
  kind: "player" | "game" | "blog";
  select: (boards: Leaderboards) => LeaderboardEntry[] | undefined;
  /** In the three-card digest teaser (rating boards only). */
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
    title: "Лучший блог по отметкам Нравится",
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

export const TEASER_BOARDS: readonly LeaderboardBoardDef[] =
  LEADERBOARD_BOARDS.filter((b) => b.teaser);

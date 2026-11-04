// The game store specs hand these to vi.mock and read the mocks back.
import { vi } from "vitest";

export const mockGetParticipatingGames = vi.fn();
export const mockGetModerationGames = vi.fn();
export const mockGetPopularGames = vi.fn();
export const mockGetActiveGames = vi.fn();
export const mockGetRecruitingGames = vi.fn();
export const mockGetFinishedGames = vi.fn();
export const mockGetTags = vi.fn();
export const mockSearchGames = vi.fn();
export const mockGetGame = vi.fn();
export const mockGetRooms = vi.fn();
export const mockGetPosts = vi.fn();
export const mockGetCharacters = vi.fn();
export const mockGetGameComments = vi.fn();
export const mockUpdateGameComment = vi.fn();
export const mockDeleteGameComment = vi.fn();
export const mockSubscribe = vi.fn();
export const mockUnsubscribe = vi.fn();
export const mockGetFirstUnreadRoomPost = vi.fn();
export const mockGetPost = vi.fn();

/** The module ../api/gameApi stands for. */
export const gameApi = () => ({
  default: {
    getTags: mockGetTags,
    getGame: mockGetGame,
    subscribe: mockSubscribe,
    unsubscribe: mockUnsubscribe,
  },
});

/** The module ../api/gameCatalogApi stands for. */
export const gameCatalogApi = () => ({
  default: {
    getParticipatingGames: mockGetParticipatingGames,
    getModerationGames: mockGetModerationGames,
    getPopularGames: mockGetPopularGames,
    getActiveGames: mockGetActiveGames,
    getRecruitingGames: mockGetRecruitingGames,
    getFinishedGames: mockGetFinishedGames,
    searchGames: mockSearchGames,
  },
});

/** The module ../api/roomApi stands for. */
export const roomApi = () => ({
  default: { getRooms: mockGetRooms },
});

/** The module ../api/postApi stands for. */
export const postApi = () => ({
  default: {
    getPosts: mockGetPosts,
    getFirstUnreadRoomPost: mockGetFirstUnreadRoomPost,
    getPost: mockGetPost,
  },
});

/** The module ../api/characterApi stands for. */
export const characterApi = () => ({
  default: { getCharacters: mockGetCharacters },
});

/** The module ../api/discussionApi stands for. */
export const discussionApi = () => ({
  default: {
    getGameComments: mockGetGameComments,
    updateGameComment: mockUpdateGameComment,
    deleteGameComment: mockDeleteGameComment,
  },
});

/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type {
  Game,
  GameRef,
  GameId,
  Room,
  Post,
  Character,
  GameStatus,
  GameRecruitment,
  GamePrivacySettings,
  CommentariesAccessMode,
} from "./types";
import type { Served } from "@/shared/api/models";
import type { UserRef } from "@/shared/api/models/common";

// Helper to cast raw values to Served type for test mocks
function asServed<T>(value: T): Served<T> {
  return value as Served<T>;
}

// Use vi.hoisted to ensure mocks are created before vi.mock hoisting
const {
  mockGetParticipatingGames,
  mockGetModerationGames,
  mockGetPopularGames,
  mockGetActiveGames,
  mockGetRecruitingGames,
  mockGetFinishedGames,
  mockGetTags,
  mockSearchGames,
  mockGetGame,
  mockGetRooms,
  mockGetPosts,
  mockGetCharacters,
  mockGetGameComments,
  mockSubscribe,
  mockUnsubscribe,
} = vi.hoisted(() => ({
  mockGetParticipatingGames: vi.fn(),
  mockGetModerationGames: vi.fn(),
  mockGetPopularGames: vi.fn(),
  mockGetActiveGames: vi.fn(),
  mockGetRecruitingGames: vi.fn(),
  mockGetFinishedGames: vi.fn(),
  mockGetTags: vi.fn(),
  mockSearchGames: vi.fn(),
  mockGetGame: vi.fn(),
  mockGetRooms: vi.fn(),
  mockGetPosts: vi.fn(),
  mockGetCharacters: vi.fn(),
  mockGetGameComments: vi.fn(),
  mockSubscribe: vi.fn(),
  mockUnsubscribe: vi.fn(),
}));

vi.mock("../api/gameApi", () => ({
  default: {
    getParticipatingGames: mockGetParticipatingGames,
    getModerationGames: mockGetModerationGames,
    getPopularGames: mockGetPopularGames,
    getActiveGames: mockGetActiveGames,
    getRecruitingGames: mockGetRecruitingGames,
    getFinishedGames: mockGetFinishedGames,
    getTags: mockGetTags,
    searchGames: mockSearchGames,
    getGame: mockGetGame,
    getRooms: mockGetRooms,
    getPosts: mockGetPosts,
    getCharacters: mockGetCharacters,
    getGameComments: mockGetGameComments,
    subscribe: mockSubscribe,
    unsubscribe: mockUnsubscribe,
  },
}));

import { useGamesStore, useGameDetailsStore } from "./store";

const createMockUserRef = (id: string, username: string): UserRef =>
  ({
    id: id,
    username,
    lastActivityUtc: "2024-01-01T00:00:00Z",
  }) as UserRef;

const createMockRecruitment = (): GameRecruitment => ({
  isOpen: true,
  pcCount: 0,
  isSubsequent: false,
});

const createMockPrivacySettings = (): GamePrivacySettings => ({
  viewPrivates: true,
  viewDice: true,
  commentariesAccess: "Public" as CommentariesAccessMode,
});

const createMockGameRef = (id: string, title: string): GameRef => ({
  id: asServed(id as GameId),
  publicId: asServed(
    id
      .slice(0, 5)
      .toLowerCase()
      .replace(/[^a-z]/g, "a"),
  ),
  title,
  status: "Active" as GameStatus,
  master: asServed(createMockUserRef("user-1", "master")),
  assistants: asServed([]),
  participation: asServed([]),
  subscribersCount: 0,
  recruitment: asServed(createMockRecruitment()),
  unreadPostsCount: asServed(0),
  unreadCommentsCount: asServed(0),
  gameReviewsCount: asServed(0),
  postReviewsCount: asServed(0),
});

const createMockGame = (id: string, title: string): Game => ({
  ...createMockGameRef(id, title),
  system: "Test System",
  setting: "Test Setting",
  createdUtc: "2024-01-01T00:00:00Z",
  pendingAssistant: asServed(null),
  mentor: asServed(null),
  info: "",
  tagIds: [],
  privacySettings: createMockPrivacySettings(),
  schema: null,
  unreadCharactersCount: asServed(0),
});

describe("useGamesStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // ============================================================================
  // INITIALIZATION
  // ============================================================================

  describe("Initialization", () => {
    it("creates store with initial state", () => {
      const store = useGamesStore();

      expect(store.participatingGames).toBeNull();
      expect(store.popularGames).toBeNull();
      expect(store.activeGames).toBeNull();
      expect(store.recruitingGames).toBeNull();
      expect(store.finishedGames).toBeNull();
      expect(store.tags).toBeNull();
    });

    it("has fetch functions", () => {
      const store = useGamesStore();

      expect(typeof store.fetchParticipatingGames).toBe("function");
      expect(typeof store.fetchPopularGames).toBe("function");
      expect(typeof store.fetchActiveGames).toBe("function");
      expect(typeof store.fetchRecruitingGames).toBe("function");
      expect(typeof store.fetchFinishedGames).toBe("function");
      expect(typeof store.fetchTags).toBe("function");
    });

    it("has search functions", () => {
      const store = useGamesStore();

      expect(typeof store.searchGames).toBe("function");
      expect(typeof store.loadNextSearchPage).toBe("function");
      expect(typeof store.prefetchPage).toBe("function");
      expect(typeof store.resetSearch).toBe("function");
    });
  });

  // ============================================================================
  // FETCH POPULAR GAMES
  // ============================================================================

  describe("fetchPopularGames", () => {
    it("fetches popular games", async () => {
      const mockGames = [
        createMockGameRef("1", "Game 1"),
        createMockGameRef("2", "Game 2"),
      ];
      mockGetPopularGames.mockResolvedValue({
        data: { resources: mockGames },
        error: null,
      });

      const store = useGamesStore();
      await store.fetchPopularGames();

      expect(mockGetPopularGames).toHaveBeenCalled();
      expect(store.popularGames).toEqual(mockGames);
    });
  });

  // ============================================================================
  // FETCH ACTIVE GAMES
  // ============================================================================

  describe("fetchActiveGames", () => {
    it("fetches active games", async () => {
      const mockGames = [createMockGameRef("1", "Active Game")];
      mockGetActiveGames.mockResolvedValue({
        data: { resources: mockGames, paging: null },
        error: null,
      });

      const store = useGamesStore();
      await store.fetchActiveGames();

      expect(store.activeGames).toEqual(mockGames);
    });
  });

  // ============================================================================
  // FETCH RECRUITING GAMES
  // ============================================================================

  describe("fetchRecruitingGames", () => {
    it("fetches recruiting games", async () => {
      const mockGames = [createMockGameRef("1", "Recruiting Game")];
      mockGetRecruitingGames.mockResolvedValue({
        data: { resources: mockGames, paging: null },
        error: null,
      });

      const store = useGamesStore();
      await store.fetchRecruitingGames();

      expect(store.recruitingGames).toEqual(mockGames);
    });
  });

  // ============================================================================
  // FETCH FINISHED GAMES
  // ============================================================================

  describe("fetchFinishedGames", () => {
    it("fetches finished games", async () => {
      const mockGames = [createMockGameRef("1", "Finished Game")];
      mockGetFinishedGames.mockResolvedValue({
        data: { resources: mockGames, paging: null },
        error: null,
      });

      const store = useGamesStore();
      await store.fetchFinishedGames();

      expect(store.finishedGames).toEqual(mockGames);
    });
  });

  // ============================================================================
  // FETCH TAGS
  // ============================================================================

  describe("fetchTags", () => {
    it("fetches game tags", async () => {
      const mockTags = [
        { id: "tag-1", title: "Fantasy", gamesCount: 10 },
        { id: "tag-2", title: "Sci-Fi", gamesCount: 5 },
      ];
      mockGetTags.mockResolvedValue({
        data: { resources: mockTags },
        error: null,
      });

      const store = useGamesStore();
      await store.fetchTags();

      expect(store.tags).toEqual(mockTags);
    });
  });

  // ============================================================================
  // SEARCH GAMES
  // ============================================================================

  describe("searchGames", () => {
    it("searches games with params", async () => {
      const mockGames = [createMockGame("1", "Found Game")];
      mockSearchGames.mockResolvedValue({
        data: {
          resources: mockGames,
          paging: { current: 1, pages: 1, total: 1 },
        },
        error: null,
      });

      const store = useGamesStore();
      await store.searchGames({ search: "Found" });

      expect(mockSearchGames).toHaveBeenCalledWith({ search: "Found" });
      expect(store.searchResult?.resources).toEqual(mockGames);
    });

    it("caches search results", async () => {
      const mockGames = [createMockGame("1", "Game")];
      mockSearchGames.mockResolvedValue({
        data: {
          resources: mockGames,
          paging: { current: 1, pages: 1, total: 1 },
        },
        error: null,
      });

      const store = useGamesStore();

      // First search
      await store.searchGames({ search: "test" });
      expect(mockSearchGames).toHaveBeenCalledTimes(1);

      // Same search again - should use cache
      await store.searchGames({ search: "test" });
      // API should not be called again within cache TTL
      expect(mockSearchGames).toHaveBeenCalledTimes(1);
    });

    it("sets loading state during search", async () => {
      let resolvePromise: (value: any) => void;
      const promise = new Promise((resolve) => {
        resolvePromise = resolve;
      });
      mockSearchGames.mockReturnValue(promise);

      const store = useGamesStore();
      const searchPromise = store.searchGames({ search: "test" });

      expect(store.searchLoading).toBe(true);

      resolvePromise!({ data: { resources: [] }, error: null });
      await searchPromise;

      expect(store.searchLoading).toBe(false);
    });

    it("handles search error", async () => {
      mockSearchGames.mockResolvedValue({
        data: null,
        error: { status: 500, title: "Server error" },
      });

      const store = useGamesStore();
      await store.searchGames({ search: "test" });

      expect(store.searchError).toBeTruthy();
    });
  });

  // ============================================================================
  // PREFETCH PAGE
  // ============================================================================

  describe("prefetchPage", () => {
    it("prefetches page in background", async () => {
      mockSearchGames.mockResolvedValue({
        data: { resources: [], paging: { current: 1, pages: 3, total: 30 } },
        error: null,
      });

      const store = useGamesStore();
      await store.searchGames({ search: "test" });

      // Clear mock to track prefetch call
      mockSearchGames.mockClear();
      mockSearchGames.mockResolvedValue({
        data: { resources: [], paging: { current: 2, pages: 3, total: 30 } },
        error: null,
      });

      await store.prefetchPage(2);

      expect(mockSearchGames).toHaveBeenCalledWith(
        expect.objectContaining({ number: 2 }),
      );
    });

    it("skips prefetch if page already cached", async () => {
      mockSearchGames.mockResolvedValue({
        data: { resources: [], paging: { current: 1, pages: 1, total: 10 } },
        error: null,
      });

      const store = useGamesStore();
      await store.searchGames({ search: "test", number: 1 });

      mockSearchGames.mockClear();

      // Prefetch same page - should skip
      await store.prefetchPage(1);

      expect(mockSearchGames).not.toHaveBeenCalled();
    });
  });
});

// ============================================================================
// GAME DETAILS STORE
// ============================================================================

describe("useGameDetailsStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  // ============================================================================
  // INITIALIZATION
  // ============================================================================

  describe("Initialization", () => {
    it("creates store with initial state", () => {
      const store = useGameDetailsStore();

      expect(store.game).toBeNull();
      expect(store.rooms).toEqual([]);
      expect(store.posts).toEqual([]);
      expect(store.characters).toEqual([]);
      expect(store.comments).toEqual([]);
    });
  });

  // ============================================================================
  // LOAD GAME
  // ============================================================================

  describe("loadGame", () => {
    it("loads game by id", async () => {
      const mockGame = createMockGame("game-1", "Test Game");
      mockGetGame.mockResolvedValue({ data: mockGame, error: null });

      const store = useGameDetailsStore();
      await store.loadGame("game-1");

      expect(mockGetGame).toHaveBeenCalledWith("game-1");
      expect(store.game).toEqual(mockGame);
    });

    it("handles load error", async () => {
      mockGetGame.mockResolvedValue({
        data: null,
        error: { status: 404, title: "Not found" },
      });

      const store = useGameDetailsStore();
      await store.loadGame("invalid");

      expect(store.gameError).toBeTruthy();
      expect(store.game).toBeNull();
    });

    it("tracks loading state", async () => {
      let resolvePromise: (value: any) => void;
      const promise = new Promise((resolve) => {
        resolvePromise = resolve;
      });
      mockGetGame.mockReturnValue(promise);

      const store = useGameDetailsStore();
      const loadPromise = store.loadGame("game-1");

      expect(store.gameLoading).toBe(true);

      resolvePromise!({ data: createMockGame("1", "Game"), error: null });
      await loadPromise;

      expect(store.gameLoading).toBe(false);
    });
  });

  // ============================================================================
  // LOAD ROOMS
  // ============================================================================

  describe("participation flags", () => {
    // The wire carries GameParticipation names (Owner/Authority/Moderator), not
    // the site-level GameRole names the client used to mirror. Only Player and
    // Reader overlapped between the two, so a master matched nothing at all.
    it.each([
      [
        ["Owner", "Authority"],
        { isMaster: true, isAssistant: false, isPlayer: true },
      ],
      [["Authority"], { isMaster: false, isAssistant: true, isPlayer: false }],
      [["Moderator"], { isMaster: false, isAssistant: false, isPlayer: true }],
      [["Player"], { isMaster: false, isAssistant: false, isPlayer: true }],
      [["Reader"], { isMaster: false, isAssistant: false, isPlayer: false }],
    ])("reads %j", async (participation, expected) => {
      const mockGame = {
        ...createMockGame("game-1", "Test Game"),
        participation: asServed(participation),
      };
      mockGetGame.mockResolvedValue({ data: mockGame, error: null });

      const store = useGameDetailsStore();
      await store.loadGame("game-1");

      expect(store.isMaster).toBe(expected.isMaster);
      expect(store.isAssistant).toBe(expected.isAssistant);
      expect(store.isPlayer).toBe(expected.isPlayer);
    });

    it("treats the master as subscribed only when Reader is present", async () => {
      const mockGame = {
        ...createMockGame("game-1", "Test Game"),
        participation: asServed(["Owner", "Authority"]),
      };
      mockGetGame.mockResolvedValue({ data: mockGame, error: null });

      const store = useGameDetailsStore();
      await store.loadGame("game-1");

      expect(store.isSubscribed).toBe(false);
    });
  });
  describe("loadRooms", () => {
    it("loads rooms for game", async () => {
      const mockRooms: Room[] = [
        { id: "room-1", title: "Room 1" } as Room,
        { id: "room-2", title: "Room 2" } as Room,
      ];
      mockGetRooms.mockResolvedValue({
        data: { resources: mockRooms },
        error: null,
      });

      const store = useGameDetailsStore();
      await store.loadRooms("game-1");

      expect(store.rooms).toEqual(mockRooms);
    });
  });

  // ============================================================================
  // LOAD POSTS
  // ============================================================================

  describe("loadPosts", () => {
    it("loads posts for room", async () => {
      const mockPosts: Post[] = [
        {
          id: "post-1",
          gameText: "Post 1",
          createdUtc: "2024-01-01T00:00:00Z",
        } as Post,
      ];
      mockGetPosts.mockResolvedValue({
        data: {
          resources: mockPosts,
          paging: { current: 1, pages: 1, total: 1 },
        },
        error: null,
      });

      const store = useGameDetailsStore();
      store.rooms = [{ id: "room-1", title: "Room" } as Room];
      await store.loadPosts("room-1");

      expect(store.posts).toEqual(mockPosts);
      expect(store.currentRoom?.id).toBe("room-1");
    });

    it("loads specific page", async () => {
      mockGetPosts.mockResolvedValue({
        data: { resources: [], paging: { current: 2, pages: 3, total: 30 } },
        error: null,
      });

      const store = useGameDetailsStore();
      await store.loadPosts("room-1", 2);

      expect(mockGetPosts).toHaveBeenCalledWith("room-1", { number: 2 });
    });
  });

  // ============================================================================
  // LOAD CHARACTERS
  // ============================================================================

  describe("loadCharacters", () => {
    it("loads characters for game", async () => {
      const mockCharacters: Character[] = [
        { id: "char-1", name: "Character 1" } as Character,
      ];
      mockGetCharacters.mockResolvedValue({
        data: { resources: mockCharacters },
        error: null,
      });

      const store = useGameDetailsStore();
      await store.loadCharacters("game-1");

      expect(store.characters).toEqual(mockCharacters);
    });
  });

  // ============================================================================
  // SUBSCRIBE/UNSUBSCRIBE
  // ============================================================================

  describe("subscribe", () => {
    it("subscribes to game", async () => {
      const mockGame = createMockGame("game-1", "Game");
      mockSubscribe.mockResolvedValue({ error: null });
      mockGetGame.mockResolvedValue({ data: mockGame, error: null });

      const store = useGameDetailsStore();
      store.game = mockGame;

      const result = await store.subscribe();

      expect(mockSubscribe).toHaveBeenCalledWith("game-1");
      // Null is success: the mutators return the problem document on failure.
      expect(result).toBeNull();
    });

    it("reports a failure when there is no game to subscribe to", async () => {
      const store = useGameDetailsStore();
      const result = await store.subscribe();

      // Not null, so the caller shows its message instead of silently
      // reporting success.
      expect(result).not.toBeNull();
      expect(mockSubscribe).not.toHaveBeenCalled();
    });

    it("hands back what the server said", async () => {
      const refused = {
        type: "",
        title: "Вы в черном списке",
        status: 403,
        traceId: "t",
      };
      const mockGame = createMockGame("game-1", "Game");
      mockSubscribe.mockResolvedValue({ error: refused });

      const store = useGameDetailsStore();
      store.game = mockGame;

      expect(await store.subscribe()).toBe(refused);
    });
  });

  describe("unsubscribe", () => {
    it("unsubscribes from game", async () => {
      const mockGame = createMockGame("game-1", "Game");
      mockUnsubscribe.mockResolvedValue({ error: null });
      mockGetGame.mockResolvedValue({ data: mockGame, error: null });

      const store = useGameDetailsStore();
      store.game = mockGame;

      const result = await store.unsubscribe();

      expect(mockUnsubscribe).toHaveBeenCalledWith("game-1");
      expect(result).toBeNull();
    });
  });

  // ============================================================================
  // RESET
  // ============================================================================

  describe("reset", () => {
    it("resets all state", async () => {
      const mockGame = createMockGame("game-1", "Game");
      mockGetGame.mockResolvedValue({ data: mockGame, error: null });

      const store = useGameDetailsStore();
      await store.loadGame("game-1");

      store.reset();

      expect(store.game).toBeNull();
      expect(store.rooms).toEqual([]);
      expect(store.posts).toEqual([]);
      expect(store.characters).toEqual([]);
      expect(store.comments).toEqual([]);
    });
  });

  // ============================================================================
  // COMPUTED
  // ============================================================================

  describe("Computed", () => {
    it("isLoading reflects any loading state", () => {
      const store = useGameDetailsStore();

      expect(store.isLoading).toBe(false);

      store.gameLoading = true;
      expect(store.isLoading).toBe(true);
    });

    it("hasError reflects any error state", () => {
      const store = useGameDetailsStore();

      expect(store.hasError).toBeFalsy();

      store.gameError = "Error";
      expect(store.hasError).toBeTruthy();
    });
  });
});

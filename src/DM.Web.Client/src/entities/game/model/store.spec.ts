/**
 * @vitest-environment jsdom
 */

/**
 * useGamesStore: the short lists of games, the tag catalogue, and the search
 * of the games page with its cache.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mockGetPopularGames, mockSearchGames } from "./store.doubles";
import {
  createMockGameRef,
  createMockGame,
  freshPinia,
} from "./store.fixtures";
import { useGamesStore } from "./store";

vi.mock("../api/gameApi", async () =>
  (await import("./store.doubles")).gameApi(),
);

vi.mock("../api/gameCatalogApi", async () =>
  (await import("./store.doubles")).gameCatalogApi(),
);

describe("useGamesStore", () => {
  beforeEach(freshPinia);

  afterEach(() => {
    vi.restoreAllMocks();
  });

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

      expect(mockSearchGames).toHaveBeenCalledWith(
        { search: "Found" },
        { ownsRefusal: true, ownsRateLimit: true },
      );
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
      let resolvePromise: (value: unknown) => void;
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

    // The title of a 429 names no number, and the number is what the reader acts on.
    it("names the wait in the strip when the search is rate limited", async () => {
      mockSearchGames.mockResolvedValue({
        data: null,
        error: {
          status: 429,
          title: "Слишком много запросов",
          retryAfterSeconds: 30,
        },
      });

      const store = useGamesStore();
      await store.searchGames({ search: "test" });

      expect(store.searchError).toBe(
        "Слишком много попыток, попробуйте через 30 секунд",
      );
    });
  });

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

/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import { createMockBlog, createMockBlogRef } from "./store.fixtures";

// Use vi.hoisted to ensure mocks are created before vi.mock hoisting
const {
  mockGetActiveBlogs,
  mockGetPopularBlogs,
  mockGetParticipatingBlogs,
  mockApiGet,
} = vi.hoisted(() => ({
  mockGetActiveBlogs: vi.fn(),
  mockGetPopularBlogs: vi.fn(),
  mockGetParticipatingBlogs: vi.fn(),
  mockApiGet: vi.fn(),
}));

vi.mock("../api/blogApi", () => ({
  default: {
    getActiveBlogs: mockGetActiveBlogs,
    getPopularBlogs: mockGetPopularBlogs,
    getParticipatingBlogs: mockGetParticipatingBlogs,
  },
}));

vi.mock("@/shared/api", () => ({
  Api: {
    get: (...args: unknown[]) => mockApiGet(...args),
  },
}));

import { useBlogsStore } from "./store";

// Read slot by slot, so a check does not break when a later argument gains a value.
const lastRequest = () => {
  const [url, params, audience, options] = mockApiGet.mock.calls.at(-1) ?? [];
  return {
    url: url as string | undefined,
    params: params as Record<string, unknown> | undefined,
    audience: audience as string | undefined,
    options: options as
      { ownsRefusal?: boolean; ownsRateLimit?: boolean } | undefined,
  };
};

describe("useBlogsStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("fetchActiveBlogs", () => {
    it("fetches active blogs", async () => {
      const mockBlogs = [
        createMockBlogRef("1", "Blog 1"),
        createMockBlogRef("2", "Blog 2"),
      ];
      mockGetActiveBlogs.mockResolvedValue({
        data: { resources: mockBlogs },
        error: null,
      });

      const store = useBlogsStore();
      await store.fetchActiveBlogs();

      expect(mockGetActiveBlogs).toHaveBeenCalled();
      expect(store.activeBlogs).toEqual(mockBlogs);
    });

    it("handles fetch error", async () => {
      mockGetActiveBlogs.mockResolvedValue({
        data: null,
        error: { status: 500, title: "Server error" },
      });

      const store = useBlogsStore();
      await store.fetchActiveBlogs();

      expect(store.activeBlogsError).toBeTruthy();
    });
  });

  describe("searchBlogs", () => {
    it("searches blogs with params", async () => {
      const mockBlogs = [createMockBlog("1", "Found Blog")];
      mockApiGet.mockResolvedValue({
        data: {
          resources: mockBlogs,
          paging: { current: 1, pages: 1, total: 1 },
        },
        error: null,
      });

      const store = useBlogsStore();
      await store.searchBlogs({ search: "Found" });

      expect(lastRequest().url).toBe("blogs");
      expect(lastRequest().params).toEqual(
        expect.objectContaining({ search: "Found" }),
      );
      expect(store.searchResult?.resources).toEqual(mockBlogs);
    });

    it("maps frontend params to API params", async () => {
      mockApiGet.mockResolvedValue({
        data: { resources: [], paging: null },
        error: null,
      });

      const store = useBlogsStore();
      await store.searchBlogs({
        search: "test",
        status: "Active",
        sortBy: "popularity",
        sortOrder: "desc",
        number: 2,
        size: 10,
      });

      expect(lastRequest().url).toBe("blogs");
      expect(lastRequest().params).toEqual(
        expect.objectContaining({
          search: "test",
          // Plural and repeatable on the wire, the same name /v1/games takes.
          // The store's own parameter stays singular: one status is what the
          // filter offers.
          statuses: ["Active"],
          sortBy: "popularity",
          sortOrder: "desc",
          skip: 10, // (number - 1) * size = (2 - 1) * 10 = 10
          take: 10,
        }),
      );
    });

    it("caches search results", async () => {
      const mockBlogs = [createMockBlog("1", "Blog")];
      mockApiGet.mockResolvedValue({
        data: {
          resources: mockBlogs,
          paging: { current: 1, pages: 1, total: 1 },
        },
        error: null,
      });

      const store = useBlogsStore();

      // First search
      await store.searchBlogs({ search: "test" });
      expect(mockApiGet).toHaveBeenCalledTimes(1);

      // Same search again - should use cache
      await store.searchBlogs({ search: "test" });
      expect(mockApiGet).toHaveBeenCalledTimes(1);
    });

    it("sets loading state during search", async () => {
      let resolvePromise: (value: unknown) => void;
      const promise = new Promise((resolve) => {
        resolvePromise = resolve;
      });
      mockApiGet.mockReturnValue(promise);

      const store = useBlogsStore();
      const searchPromise = store.searchBlogs({ search: "test" });

      expect(store.searchLoading).toBe(true);

      resolvePromise!({ data: { resources: [] }, error: null });
      await searchPromise;

      expect(store.searchLoading).toBe(false);
    });

    it("handles search error", async () => {
      mockApiGet.mockResolvedValue({
        data: null,
        error: { status: 500, title: "Server error" },
      });

      const store = useBlogsStore();
      await store.searchBlogs({ search: "test" });

      expect(store.searchError).toBeTruthy();
    });

    it("says the wait itself, with the seconds the server asked for", async () => {
      mockApiGet.mockResolvedValue({
        data: null,
        error: {
          status: 429,
          title: "Слишком много запросов",
          retryAfterSeconds: 30,
        },
      });

      const store = useBlogsStore();
      await store.searchBlogs({ search: "test" });

      expect(store.searchError).toBe(
        "Слишком много попыток, попробуйте через 30 секунд",
      );
      expect(lastRequest().options?.ownsRateLimit).toBe(true);
    });

    it("falls back to its own sentence when nothing was named", async () => {
      // The shape the client produces for a request that got no response.
      mockApiGet.mockResolvedValue({
        data: null,
        error: { type: "Unknown", title: "", status: 0, traceId: "" },
      });

      const store = useBlogsStore();
      await store.searchBlogs({ search: "test" });

      expect(store.searchError).toBe("Не удалось загрузить блоги");
    });

    // The flag is asserted: the interceptor lives in the client this test replaces.
    it("takes the refusal over, so the strip is the only place it is said", async () => {
      mockApiGet.mockResolvedValue({
        data: null,
        error: { type: "", title: "Ошибка сервера", status: 500, traceId: "t" },
      });

      const store = useBlogsStore();
      await store.searchBlogs({ search: "test" });

      expect(store.searchError).toBe("Ошибка сервера");
      expect(lastRequest().options?.ownsRefusal).toBe(true);
    });

    it("includes host usernames in search", async () => {
      mockApiGet.mockResolvedValue({
        data: { resources: [], paging: null },
        error: null,
      });

      const store = useBlogsStore();
      await store.searchBlogs({ hostUsernames: ["user1", "user2"] });

      expect(lastRequest().params).toEqual(
        expect.objectContaining({
          hostUsernames: ["user1", "user2"],
        }),
      );
    });

    it("includes date range filters", async () => {
      mockApiGet.mockResolvedValue({
        data: { resources: [], paging: null },
        error: null,
      });

      const store = useBlogsStore();
      await store.searchBlogs({
        createdFromUtc: "2024-01-01",
        createdToUtc: "2024-12-31",
      });

      expect(lastRequest().params).toEqual(
        expect.objectContaining({
          createdFromUtc: "2024-01-01",
          createdToUtc: "2024-12-31",
        }),
      );
    });
  });

  describe("prefetchPage", () => {
    it("prefetches page in background", async () => {
      mockApiGet.mockResolvedValue({
        data: { resources: [], paging: { current: 1, pages: 3, total: 30 } },
        error: null,
      });

      const store = useBlogsStore();
      await store.searchBlogs({ search: "test" });

      // Clear mock to track prefetch call
      mockApiGet.mockClear();
      mockApiGet.mockResolvedValue({
        data: { resources: [], paging: { current: 2, pages: 3, total: 30 } },
        error: null,
      });

      await store.prefetchPage(2);

      // page 2 with default size 20 -> skip = (2-1) * 20 = 20
      expect(mockApiGet).toHaveBeenCalledWith(
        "blogs",
        expect.objectContaining({ skip: 20, take: 20 }),
      );
    });

    it("skips prefetch if no last search params", async () => {
      const store = useBlogsStore();
      await store.prefetchPage(2);

      expect(mockApiGet).not.toHaveBeenCalled();
    });

    it("skips prefetch if page already cached", async () => {
      mockApiGet.mockResolvedValue({
        data: { resources: [], paging: { current: 1, pages: 1, total: 10 } },
        error: null,
      });

      const store = useBlogsStore();
      await store.searchBlogs({ search: "test", number: 1 });

      mockApiGet.mockClear();

      // Prefetch same page - should skip
      await store.prefetchPage(1);

      expect(mockApiGet).not.toHaveBeenCalled();
    });
  });

  describe("clearSearchCache", () => {
    it("clears the search cache", async () => {
      mockApiGet.mockResolvedValue({
        data: { resources: [], paging: null },
        error: null,
      });

      const store = useBlogsStore();
      await store.searchBlogs({ search: "test" });

      store.clearSearchCache();

      // Search again - should make new API call
      await store.searchBlogs({ search: "test" });

      expect(mockApiGet).toHaveBeenCalledTimes(2);
    });
  });

  describe("Loading States", () => {
    it("tracks loading state for active blogs", async () => {
      let resolvePromise: (value: unknown) => void;
      const promise = new Promise((resolve) => {
        resolvePromise = resolve;
      });
      mockGetActiveBlogs.mockReturnValue(promise);

      const store = useBlogsStore();
      const fetchPromise = store.fetchActiveBlogs();

      expect(store.activeBlogsLoading).toBe(true);

      resolvePromise!({ data: { resources: [] }, error: null });
      await fetchPromise;

      expect(store.activeBlogsLoading).toBe(false);
    });
  });

  describe("resetParticipatingBlogs", () => {
    it("resets only participating blogs", async () => {
      const mockBlogs = [createMockBlogRef("1", "My Blog")];
      mockGetParticipatingBlogs.mockResolvedValue({
        data: { resources: mockBlogs },
        error: null,
      });
      mockGetActiveBlogs.mockResolvedValue({
        data: { resources: [createMockBlogRef("2", "Active")] },
        error: null,
      });

      const store = useBlogsStore();
      await store.fetchParticipatingBlogs();
      await store.fetchActiveBlogs();

      store.resetParticipatingBlogs();

      expect(store.participatingBlogs).toBeNull();
      expect(store.activeBlogs).not.toBeNull(); // Should remain
    });
  });
});

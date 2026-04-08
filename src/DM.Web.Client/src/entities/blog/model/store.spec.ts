/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type { Blog, BlogRef, BlogId } from "./types";

// Use vi.hoisted to ensure mocks are created before vi.mock hoisting
const { mockGetPublicBlogs, mockGetActiveBlogs, mockGetPopularBlogs, mockGetParticipatingBlogs, mockApiGet } = vi.hoisted(() => ({
  mockGetPublicBlogs: vi.fn(),
  mockGetActiveBlogs: vi.fn(),
  mockGetPopularBlogs: vi.fn(),
  mockGetParticipatingBlogs: vi.fn(),
  mockApiGet: vi.fn(),
}));

vi.mock("../api/blogApi", () => ({
  default: {
    getPublicBlogs: mockGetPublicBlogs,
    getActiveBlogs: mockGetActiveBlogs,
    getPopularBlogs: mockGetPopularBlogs,
    getParticipatingBlogs: mockGetParticipatingBlogs,
  },
}));

vi.mock("@/shared/api", () => ({
  Api: {
    get: (...args: any[]) => mockApiGet(...args),
  },
}));

import { useBlogsStore } from "./store";

const createMockBlogRef = (id: string, title: string): BlogRef => ({
  id: id as BlogId,
  title,
  author: { id: "user-1", username: "author" } as any,
  status: "Active",
  createdUtc: "2024-01-01",
  subscribersCount: 0,
} as BlogRef);

const createMockBlog = (id: string, title: string): Blog => ({
  id: id as BlogId,
  title,
  author: { id: "user-1", username: "author" } as any,
  status: "Active",
  createdUtc: "2024-01-01",
  subscribersCount: 0,
  draftVisibility: "Private",
  commentsEnabled: true,
  publicationCount: 0,
  commentsCount: 0,
} as Blog);

describe("useBlogsStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    // Clear module-level cache
    useBlogsStore().clearSearchCache();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // ============================================================================
  // INITIALIZATION
  // ============================================================================

  describe("Initialization", () => {
    it("creates store with initial state", () => {
      const store = useBlogsStore();

      expect(store.activeBlogs).toBeNull();
      expect(store.popularBlogs).toBeNull();
      expect(store.participatingBlogs).toBeNull();
      expect(store.searchResult).toBeNull();
    });

    it("has fetch functions", () => {
      const store = useBlogsStore();

      expect(typeof store.fetchActiveBlogs).toBe("function");
      expect(typeof store.fetchPopularBlogs).toBe("function");
      expect(typeof store.fetchParticipatingBlogs).toBe("function");
    });

    it("has search functions", () => {
      const store = useBlogsStore();

      expect(typeof store.searchBlogs).toBe("function");
      expect(typeof store.prefetchPage).toBe("function");
      expect(typeof store.clearSearchCache).toBe("function");
    });
  });

  // ============================================================================
  // FETCH ACTIVE BLOGS
  // ============================================================================

  describe("fetchActiveBlogs", () => {
    it("fetches active blogs", async () => {
      const mockBlogs = [createMockBlogRef("1", "Blog 1"), createMockBlogRef("2", "Blog 2")];
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

  // ============================================================================
  // FETCH POPULAR BLOGS
  // ============================================================================

  describe("fetchPopularBlogs", () => {
    it("fetches popular blogs", async () => {
      const mockBlogs = [createMockBlogRef("1", "Popular Blog")];
      mockGetPopularBlogs.mockResolvedValue({
        data: { resources: mockBlogs },
        error: null,
      });

      const store = useBlogsStore();
      await store.fetchPopularBlogs();

      expect(mockGetPopularBlogs).toHaveBeenCalled();
      expect(store.popularBlogs).toEqual(mockBlogs);
    });
  });

  // ============================================================================
  // FETCH PARTICIPATING BLOGS
  // ============================================================================

  describe("fetchParticipatingBlogs", () => {
    it("fetches user's participating blogs", async () => {
      const mockBlogs = [createMockBlogRef("1", "My Blog")];
      mockGetParticipatingBlogs.mockResolvedValue({
        data: { resources: mockBlogs },
        error: null,
      });

      const store = useBlogsStore();
      await store.fetchParticipatingBlogs();

      expect(store.participatingBlogs).toEqual(mockBlogs);
    });
  });

  // ============================================================================
  // SEARCH BLOGS
  // ============================================================================

  describe("searchBlogs", () => {
    it("searches blogs with params", async () => {
      const mockBlogs = [createMockBlog("1", "Found Blog")];
      mockApiGet.mockResolvedValue({
        data: { resources: mockBlogs, paging: { current: 1, pages: 1, total: 1 } },
        error: null,
      });

      const store = useBlogsStore();
      await store.searchBlogs({ search: "Found" });

      expect(mockApiGet).toHaveBeenCalledWith(
        "blogs",
        expect.objectContaining({ search: "Found" })
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

      expect(mockApiGet).toHaveBeenCalledWith(
        "blogs",
        expect.objectContaining({
          search: "test",
          status: "Active",
          sortBy: "popularity",
          sortOrder: "desc",
          skip: 10, // (number - 1) * size = (2 - 1) * 10 = 10
          take: 10,
        })
      );
    });

    it("caches search results", async () => {
      const mockBlogs = [createMockBlog("1", "Blog")];
      mockApiGet.mockResolvedValue({
        data: { resources: mockBlogs, paging: { current: 1, pages: 1, total: 1 } },
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
      let resolvePromise: (value: any) => void;
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

    it("includes host usernames in search", async () => {
      mockApiGet.mockResolvedValue({
        data: { resources: [], paging: null },
        error: null,
      });

      const store = useBlogsStore();
      await store.searchBlogs({ hostUsernames: ["user1", "user2"] });

      expect(mockApiGet).toHaveBeenCalledWith(
        "blogs",
        expect.objectContaining({
          hostUsernames: ["user1", "user2"],
        })
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

      expect(mockApiGet).toHaveBeenCalledWith(
        "blogs",
        expect.objectContaining({
          createdFromUtc: "2024-01-01",
          createdToUtc: "2024-12-31",
        })
      );
    });
  });

  // ============================================================================
  // PREFETCH PAGE
  // ============================================================================

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
        expect.objectContaining({ skip: 20, take: 20 })
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

  // ============================================================================
  // CLEAR SEARCH CACHE
  // ============================================================================

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

  // ============================================================================
  // LOADING STATES
  // ============================================================================

  describe("Loading States", () => {
    it("tracks loading state for active blogs", async () => {
      let resolvePromise: (value: any) => void;
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

    it("tracks loading state for popular blogs", async () => {
      let resolvePromise: (value: any) => void;
      const promise = new Promise((resolve) => {
        resolvePromise = resolve;
      });
      mockGetPopularBlogs.mockReturnValue(promise);

      const store = useBlogsStore();
      const fetchPromise = store.fetchPopularBlogs();

      expect(store.popularBlogsLoading).toBe(true);

      resolvePromise!({ data: { resources: [] }, error: null });
      await fetchPromise;

      expect(store.popularBlogsLoading).toBe(false);
    });
  });

  // ============================================================================
  // RESET
  // ============================================================================

  describe("resetAllBlogs", () => {
    it("resets all blog data", async () => {
      const mockBlogs = [createMockBlogRef("1", "Blog")];
      mockGetActiveBlogs.mockResolvedValue({
        data: { resources: mockBlogs },
        error: null,
      });
      mockGetPopularBlogs.mockResolvedValue({
        data: { resources: mockBlogs },
        error: null,
      });

      const store = useBlogsStore();
      await store.fetchActiveBlogs();
      await store.fetchPopularBlogs();

      store.resetAllBlogs();

      expect(store.activeBlogs).toBeNull();
      expect(store.popularBlogs).toBeNull();
      expect(store.participatingBlogs).toBeNull();
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

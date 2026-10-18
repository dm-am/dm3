/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type { Blog, BlogRef, BlogId, Publication } from "./types";

// Use vi.hoisted to ensure mocks are created before vi.mock hoisting
const {
  mockGetPublicBlogs,
  mockGetActiveBlogs,
  mockGetPopularBlogs,
  mockGetParticipatingBlogs,
  mockGetBlog,
  mockGetPublications,
  mockUpdateBlogComment,
  mockDeleteBlogComment,
  mockApiGet,
} = vi.hoisted(() => ({
  mockGetPublicBlogs: vi.fn(),
  mockGetActiveBlogs: vi.fn(),
  mockGetPopularBlogs: vi.fn(),
  mockGetParticipatingBlogs: vi.fn(),
  mockGetBlog: vi.fn(),
  mockGetPublications: vi.fn(),
  mockUpdateBlogComment: vi.fn(),
  mockDeleteBlogComment: vi.fn(),
  mockApiGet: vi.fn(),
}));

vi.mock("../api/blogApi", () => ({
  default: {
    getPublicBlogs: mockGetPublicBlogs,
    getActiveBlogs: mockGetActiveBlogs,
    getPopularBlogs: mockGetPopularBlogs,
    getParticipatingBlogs: mockGetParticipatingBlogs,
    getBlog: mockGetBlog,
    getPublications: mockGetPublications,
    updateBlogComment: mockUpdateBlogComment,
    deleteBlogComment: mockDeleteBlogComment,
  },
}));

vi.mock("@/shared/api", () => ({
  Api: {
    get: (...args: any[]) => mockApiGet(...args),
  },
}));

import { useBlogsStore } from "./store";
import { useBlogDetailsStore } from "./detailsStore";

/**
 * The last request the store sent, read slot by slot.
 *
 * The client takes the url, the query params, the render audience and its own
 * options as four positional arguments, and a check written as one call shape
 * breaks whenever a slot behind the one it cares about gains a value — which is
 * how the filter checks below would have answered for the refusal flag.
 */
const lastRequest = () => {
  const [url, params, audience, options] = mockApiGet.mock.calls.at(-1) ?? [];
  return {
    url: url as string | undefined,
    params: params as Record<string, unknown> | undefined,
    audience: audience as string | undefined,
    options: options as
      | { ownsRefusal?: boolean; ownsRateLimit?: boolean }
      | undefined,
  };
};

const createMockBlogRef = (id: string, title: string): BlogRef =>
  ({
    id: id as BlogId,
    title,
    author: { id: "user-1", username: "author" } as any,
    status: "Active",
    createdUtc: "2024-01-01",
    subscribersCount: 0,
  }) as BlogRef;

const createMockBlog = (id: string, title: string): Blog =>
  ({
    id: id as BlogId,
    title,
    author: { id: "user-1", username: "author" } as any,
    status: "Active",
    createdUtc: "2024-01-01",
    subscribersCount: 0,
    draftVisibility: "Private",
    publicationCount: 0,
    commentsCount: 0,
  }) as Blog;

describe("useBlogsStore", () => {
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

    // Every failure of this list used to arrive as one sentence, "Ошибка
    // загрузки данных" — a rate limit, a broken server and a dead connection
    // call for three different things to do. The API names what it can in the
    // problem document's title (the error middleware answers an unhandled
    // exception with "Ошибка сервера") and names nothing when the request never
    // got a response. Both halves are asserted because both are the point: the
    // server's own sentence when it sent one, the page's own when it did not.
    // The first half is asserted a few lines down, where the strip answers a
    // 500 alone.
    it("says the wait itself, with the seconds the server asked for", async () => {
      // The limiter answers 429 with "Слишком много запросов" and puts the wait
      // in a header, which the transport copies into the document. The strip is
      // now the only carrier of this refusal, so it says the wording that
      // carries the number — the one the interceptor's plate used to hold.
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

    // The strip where the rows would be is where this failure is read, and the
    // response interceptor puts a plate of its own over a 403 and a 5xx unless
    // the request says the page owns the refusal. Until it did, a broken server
    // was announced twice on one screen — the API's sentence in the strip and
    // the same sentence in the corner — and only the strip said which list was
    // empty. The flag is asserted rather than the absent toast because the
    // interceptor lives in the client this store's test replaces.
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

// ============================================================================
// BLOG DETAILS STORE — OUT-OF-ORDER RESPONSES
// ============================================================================

/**
 * Mirrors the game details store: one bag for "the current blog", two replies
 * in flight after two clicks inside a round trip, and whichever landed last
 * used to win — silently, with the spinner already hidden.
 */
describe("useBlogDetailsStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

  function deferred<T>() {
    let resolve!: (value: T) => void;
    const promise = new Promise<T>((settle) => {
      resolve = settle;
    });
    return { promise, resolve };
  }

  it("keeps the newer blog when the older reply lands last", async () => {
    const older = deferred<unknown>();
    const newer = deferred<unknown>();
    mockGetBlog
      .mockReturnValueOnce(older.promise)
      .mockReturnValueOnce(newer.promise);

    const store = useBlogDetailsStore();
    const loadA = store.loadBlog("blog-a");
    const loadB = store.loadBlog("blog-b");

    newer.resolve({ data: createMockBlog("blog-b", "Blog B"), error: null });
    await loadB;
    older.resolve({ data: createMockBlog("blog-a", "Blog A"), error: null });
    await loadA;

    expect(store.blog?.title).toBe("Blog B");
  });

  it("lets the newer request own the spinner", async () => {
    const older = deferred<unknown>();
    const newer = deferred<unknown>();
    mockGetBlog
      .mockReturnValueOnce(older.promise)
      .mockReturnValueOnce(newer.promise);

    const store = useBlogDetailsStore();
    const loadA = store.loadBlog("blog-a");
    const loadB = store.loadBlog("blog-b");

    older.resolve({ data: createMockBlog("blog-a", "Blog A"), error: null });
    await loadA;

    expect(store.blogLoading).toBe(true);
    expect(store.blog).toBeNull();

    newer.resolve({ data: createMockBlog("blog-b", "Blog B"), error: null });
    await loadB;

    expect(store.blogLoading).toBe(false);
    expect(store.blog?.title).toBe("Blog B");
  });

  it("shows the publications of the rubric chosen last", async () => {
    const ofFirst = [{ id: "pub-1", title: "One" }] as unknown as Publication[];
    const ofSecond = [
      { id: "pub-2", title: "Two" },
    ] as unknown as Publication[];

    const older = deferred<unknown>();
    const newer = deferred<unknown>();
    mockGetPublications
      .mockReturnValueOnce(older.promise)
      .mockReturnValueOnce(newer.promise);

    const store = useBlogDetailsStore();
    const loadFirst = store.loadPublications("blog-a", { rubricId: "r-1" });
    const loadSecond = store.loadPublications("blog-a", { rubricId: "r-2" });

    newer.resolve({
      data: { resources: ofSecond, paging: null },
      error: null,
    });
    await loadSecond;
    older.resolve({ data: { resources: ofFirst, paging: null }, error: null });
    await loadFirst;

    expect(store.publications).toEqual(ofSecond);
  });

  // Both halves are asserted for the same reason the game store asserts both:
  // the shell reads the number out of the store (BlogPage.vue) because five
  // call sites outside it reload the blog, while the returned object only
  // reaches the caller that asked. The shell's own spec drives the store
  // through the mocked API, so dropping the line that fills the ref has to be
  // caught here as well.
  it.each([403, 404, 410, 500])(
    "keeps the %i the server answered",
    async (status) => {
      mockGetBlog.mockResolvedValue({
        data: null,
        error: { status, title: "Refused" },
      });

      const store = useBlogDetailsStore();
      const result = await store.loadBlog("blog-a");

      expect(store.blogErrorStatus).toBe(status);
      expect(result).toEqual({ ok: false, status });
    },
  );

  it("reports a refusal that carries no status as one", async () => {
    mockGetBlog.mockResolvedValue({
      data: null,
      error: { title: "Network is down" },
    });

    const store = useBlogDetailsStore();
    const result = await store.loadBlog("blog-a");

    expect(store.blogErrorStatus).toBeNull();
    expect(result.ok).toBe(false);
  });

  it("drops a reply that arrives after the store was reset", async () => {
    const pending = deferred<unknown>();
    mockGetBlog.mockReturnValueOnce(pending.promise);

    const store = useBlogDetailsStore();
    const load = store.loadBlog("blog-a");

    store.reset();

    pending.resolve({ data: createMockBlog("blog-a", "Blog A"), error: null });
    await load;

    expect(store.blog).toBeNull();
  });

  describe("comment mutations", () => {
    const refusal = {
      type: "",
      title: "Недостаточно прав",
      status: 403,
      traceId: "t",
    };

    it("leaves the comment untouched when the delete is refused", async () => {
      mockDeleteBlogComment.mockResolvedValue({ data: null, error: refusal });

      const store = useBlogDetailsStore();
      store.comments = [{ id: "c-1", text: "Текст" }] as never;

      const { error } = await store.deleteComment("c-1");

      expect(error).toBe(refusal);
      expect(store.comments[0].isRemoved).toBeFalsy();
    });

    it("keeps the old text when the edit is refused", async () => {
      mockUpdateBlogComment.mockResolvedValue({ data: null, error: refusal });

      const store = useBlogDetailsStore();
      store.comments = [{ id: "c-1", text: "Текст" }] as never;

      const { error } = await store.updateComment("c-1", "Новый текст");

      expect(error).toBe(refusal);
      expect(store.comments[0].text).toBe("Текст");
    });
  });
});

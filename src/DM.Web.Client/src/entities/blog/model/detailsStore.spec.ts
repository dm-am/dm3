/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type { Publication } from "./types";
import { createMockBlog } from "./store.fixtures";
import { useAuthStore } from "@/shared/stores";

// Use vi.hoisted to ensure mocks are created before vi.mock hoisting
const {
  mockGetBlog,
  mockGetPublications,
  mockUpdateBlogComment,
  mockDeleteBlogComment,
  mockGetReaders,
  mockSubscribe,
  mockUnsubscribe,
  mockDeleteBlog,
  mockCreateRubric,
  mockDeleteRubric,
} = vi.hoisted(() => ({
  mockGetBlog: vi.fn(),
  mockGetPublications: vi.fn(),
  mockUpdateBlogComment: vi.fn(),
  mockDeleteBlogComment: vi.fn(),
  mockGetReaders: vi.fn(),
  mockSubscribe: vi.fn(),
  mockUnsubscribe: vi.fn(),
  mockDeleteBlog: vi.fn(),
  mockCreateRubric: vi.fn(),
  mockDeleteRubric: vi.fn(),
}));

vi.mock("../api/blogApi", () => ({
  default: {
    getBlog: mockGetBlog,
    getPublications: mockGetPublications,
    updateBlogComment: mockUpdateBlogComment,
    deleteBlogComment: mockDeleteBlogComment,
    getReaders: mockGetReaders,
    subscribe: mockSubscribe,
    unsubscribe: mockUnsubscribe,
    deleteBlog: mockDeleteBlog,
    createRubric: mockCreateRubric,
    deleteRubric: mockDeleteRubric,
  },
}));

import { useBlogDetailsStore } from "./detailsStore";
import { useBlogsStore } from "./store";
import { deferred } from "@/shared/lib/testing/tasks";

// Two replies in flight after two clicks: the one asked for last wins.
describe("useBlogDetailsStore", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
  });

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

  // The parts the pages only read through: the roles, the rubrics' order, the
  // blog's removal and the subscription. The page specs set the blog by hand
  // or replace the action, so what the store derives and reloads is held here.

  it("tells the author, the assistant and the mentor apart", () => {
    const store = useBlogDetailsStore();
    store.blog = {
      ...createMockBlog("blog-a", "Blog A"),
      // The author listed among the assistants stays the author.
      assistants: [
        { id: "user-1", username: "author" },
        { id: "user-2", username: "assistant" },
      ],
      mentor: { id: "user-3", username: "mentor" },
    } as never;
    const auth = useAuthStore();
    const rolesOf = (username: string) => {
      auth.user = { username } as never;
      return {
        isOwner: store.isOwner,
        isAssistant: store.isAssistant,
        isMentor: store.isMentor,
        canManage: store.canManage,
        canUseNotepad: store.canUseNotepad,
      };
    };
    const none = {
      isOwner: false,
      isAssistant: false,
      isMentor: false,
      canManage: false,
      canUseNotepad: false,
    };

    expect(rolesOf("author")).toEqual({
      ...none,
      isOwner: true,
      canManage: true,
      canUseNotepad: true,
    });
    expect(rolesOf("assistant")).toEqual({
      ...none,
      isAssistant: true,
      canManage: true,
      canUseNotepad: true,
    });
    expect(rolesOf("mentor")).toEqual({
      ...none,
      isMentor: true,
      canUseNotepad: true,
    });
    expect(rolesOf("reader")).toEqual(none);
  });

  it("counts the reader as subscribed once the readers name them", async () => {
    mockGetReaders.mockResolvedValue({
      data: { resources: [{ user: { username: "reader" }, role: "reader" }] },
      error: null,
    });
    useAuthStore().user = { username: "reader" } as never;
    const store = useBlogDetailsStore();

    expect(store.isSubscribed).toBe(false);
    await store.loadReaders("blog-a");
    expect(store.isSubscribed).toBe(true);
  });

  it("keeps the rubrics in their order", async () => {
    const rubric = (id: string, sortOrder: number) => ({ id, sortOrder });
    const served = (...rubrics: ReturnType<typeof rubric>[]) => ({
      data: { ...createMockBlog("blog-a", "Blog A"), rubrics },
      error: null,
    });
    mockCreateRubric.mockResolvedValue({ data: null, error: null });
    mockDeleteRubric.mockResolvedValue({ data: null, error: null });
    mockGetBlog
      .mockResolvedValueOnce(served(rubric("r-2", 2), rubric("r-1", 1)))
      .mockResolvedValueOnce(
        served(rubric("r-2", 2), rubric("r-3", 3), rubric("r-1", 1)),
      )
      .mockResolvedValueOnce(served(rubric("r-3", 3), rubric("r-1", 1)));
    const store = useBlogDetailsStore();
    const order = () => store.rubrics.map((r) => r.id);

    await store.loadBlog("blog-a");
    expect(order()).toEqual(["r-1", "r-2"]);

    expect(await store.createRubric({ title: "Третья" })).toBeNull();
    expect(order()).toEqual(["r-1", "r-2", "r-3"]);

    expect(await store.deleteRubric("r-2")).toBeNull();
    expect(order()).toEqual(["r-1", "r-3"]);
  });

  it("drops the blog lists after the blog is deleted", async () => {
    mockDeleteBlog.mockResolvedValue({ data: null, error: null });
    const invalidate = vi
      .spyOn(useBlogsStore(), "invalidateBlogLists")
      .mockResolvedValue();
    const store = useBlogDetailsStore();
    store.blog = createMockBlog("blog-a", "Blog A");

    expect(await store.deleteBlog()).toBeNull();
    expect(invalidate).toHaveBeenCalledOnce();
  });

  it("reloads the readers after a subscription", async () => {
    const reader = { user: { username: "reader" }, role: "reader" };
    mockSubscribe.mockResolvedValue({ data: null, error: null });
    mockUnsubscribe.mockResolvedValue({ data: null, error: null });
    mockGetReaders
      .mockResolvedValueOnce({ data: { resources: [reader] }, error: null })
      .mockResolvedValueOnce({ data: { resources: [] }, error: null });
    const store = useBlogDetailsStore();
    store.blog = createMockBlog("blog-a", "Blog A");

    expect(await store.subscribe()).toBeNull();
    expect(store.readers).toEqual([reader]);

    expect(await store.unsubscribe()).toBeNull();
    expect(store.readers).toEqual([]);
    expect(mockGetReaders).toHaveBeenCalledWith("blog-a");
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

/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { setActivePinia, createPinia } from "pinia";
import type {
  Board,
  Topic,
  Comment,
  BoardId,
  TopicId,
  CommentId,
} from "./types";
import type { Served } from "@/shared/api/models";
import type { User } from "@/shared/api/models/common";

// Helper to cast raw values to Served type for test mocks
function asServed<T>(value: T): Served<T> {
  return value as Served<T>;
}

// Use vi.hoisted to ensure mocks are created before vi.mock hoisting
const {
  mockGetBoards,
  mockGetBoard,
  mockGetModerators,
  mockGetTopics,
  mockGetTopic,
  mockGetComments,
  mockCreateComment,
  mockUpdateComment,
  mockDeleteComment,
  mockPostCommentLike,
  mockDeleteCommentLike,
  mockPostTopicLike,
  mockDeleteTopicLike,
  mockGetNews,
  mockUpdateTopic,
  mockReorderPinnedTopics,
} = vi.hoisted(() => ({
  mockGetBoards: vi.fn(),
  mockGetBoard: vi.fn(),
  mockGetModerators: vi.fn(),
  mockGetTopics: vi.fn(),
  mockGetTopic: vi.fn(),
  mockGetComments: vi.fn(),
  mockCreateComment: vi.fn(),
  mockUpdateComment: vi.fn(),
  mockDeleteComment: vi.fn(),
  mockPostCommentLike: vi.fn(),
  mockDeleteCommentLike: vi.fn(),
  mockPostTopicLike: vi.fn(),
  mockDeleteTopicLike: vi.fn(),
  mockGetNews: vi.fn(),
  mockUpdateTopic: vi.fn(),
  mockReorderPinnedTopics: vi.fn(),
}));

vi.mock("../api/forumApi", () => ({
  default: {
    getBoards: mockGetBoards,
    getBoard: mockGetBoard,
    getModerators: mockGetModerators,
    getTopics: mockGetTopics,
    getTopic: mockGetTopic,
    getComments: mockGetComments,
    createComment: mockCreateComment,
    updateComment: mockUpdateComment,
    deleteComment: mockDeleteComment,
    postCommentLike: mockPostCommentLike,
    deleteCommentLike: mockDeleteCommentLike,
    postTopicLike: mockPostTopicLike,
    deleteTopicLike: mockDeleteTopicLike,
    getNews: mockGetNews,
    updateTopic: mockUpdateTopic,
    reorderPinnedTopics: mockReorderPinnedTopics,
  },
}));

// The viewer, as a real store and not a bare object.
//
// The store under test reads the session through `storeToRefs(useAuthStore())`,
// and that call is exactly the kind of thing a pinia major changes under us — so
// the spec has to run it. Stubbing `storeToRefs` itself, as this file used to,
// left the one line that can break unexecuted and the gate green either way.
//
// The leaf module rather than the "@/shared/stores" barrel: usePaging imports
// "@/shared/stores/auth" directly, so mocking only the barrel put two different
// stores in the graph answering for one session.
//
// Full shape of the real store, not just the member read today: a double that
// offers less answers `undefined` for the rest, and `undefined` for
// `isAuthenticated` reads as a guest — a gap in the double would then pass as an
// assertion about the guest branch.
vi.mock("@/shared/stores/auth", async () => {
  const { defineStore } = await import("pinia");
  const { ref, computed } = await import("vue");
  return {
    useAuthStore: defineStore("auth", () => {
      const user = ref<User | null>({
        id: "user-1",
        username: "testuser",
        settings: { paging: { topicsPerPage: 20, commentsPerPage: 20 } },
      } as unknown as User);
      return {
        user,
        isAuthenticated: computed(() => user.value !== null),
        updateUser: (newUser: User | null) => {
          user.value = newUser;
        },
      };
    }),
  };
});

import { BOARD_PAGE_OWNS_REFUSAL, useBoardsStore } from "./store";
import { requestNotSent } from "@/shared/lib/errors";

const createMockUser = (id: string, username: string): User =>
  ({
    id: id,
    username,
    role: "RegularUser",
    isOnline: false,
    lastActivityUtc: "2024-01-01T00:00:00Z",
  }) as unknown as User;

const createMockBoard = (id: string, title: string): Board =>
  ({
    id: asServed(id as BoardId),
    title: asServed(title),
    alias: asServed(title.toLowerCase().replace(/\s+/g, "-")),
    description: asServed(`Description of ${title}`),
    moderators: asServed([]),
    topicsCount: asServed(10),
    commentsCount: asServed(50),
    unreadTopicsCount: asServed(0),
    unreadCommentsCount: asServed(0),
    lastComment: asServed(null),
    lastTopic: asServed(null),
  }) as Board;

const createMockTopic = (
  id: string,
  title: string,
  boardId: string,
  topicNumber: number = 1,
): Topic =>
  ({
    id: asServed(id as TopicId),
    topicNumber: asServed(topicNumber),
    author: asServed(createMockUser("user-1", "author")),
    createdUtc: asServed("2024-01-01T00:00:00Z"),
    modifiedUtc: asServed(null),
    title,
    description: "Topic content",
    isAttached: false,
    attachOrder: asServed(null),
    isClosed: false,
    closedByAuthor: asServed(false),
    lastActivityUtc: asServed("2024-01-01T00:00:00Z"),
    lastComment: asServed(null),
    commentsCount: asServed(5),
    unreadCommentsCount: asServed(0),
    board: createMockBoard(boardId, "Board"),
    likes: asServed([]),
    likesCount: asServed(0),
  }) as Topic;

const createMockComment = (id: string, text: string): Comment =>
  ({
    id: asServed(id as CommentId),
    text,
    author: asServed(createMockUser("user-1", "author")),
    createdUtc: asServed("2024-01-01T00:00:00Z"),
    modifiedUtc: asServed(null),
    likes: asServed([]),
    isRemoved: asServed(false),
  }) as Comment;

describe("useBoardsStore", () => {
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
      const store = useBoardsStore();

      expect(store.boards).toBeNull();
      expect(store.selectedBoard).toBeNull();
      expect(store.topics).toBeNull();
      expect(store.selectedTopic).toBeNull();
      expect(store.comments).toBeNull();
      expect(store.news).toBeNull();
    });

    it("has fetch functions", () => {
      const store = useBoardsStore();

      expect(typeof store.fetchBoards).toBe("function");
      expect(typeof store.trySelectBoard).toBe("function");
      expect(typeof store.searchTopics).toBe("function");
      expect(typeof store.trySelectTopic).toBe("function");
      expect(typeof store.searchComments).toBe("function");
      expect(typeof store.fetchNews).toBe("function");
    });

    it("has comment mutation functions", () => {
      const store = useBoardsStore();

      expect(typeof store.createComment).toBe("function");
      expect(typeof store.updateComment).toBe("function");
      expect(typeof store.deleteComment).toBe("function");
      expect(typeof store.likeComment).toBe("function");
      expect(typeof store.unlikeComment).toBe("function");
    });

    it("has topic like functions", () => {
      const store = useBoardsStore();

      expect(typeof store.likeTopic).toBe("function");
      expect(typeof store.unlikeTopic).toBe("function");
    });
  });

  // ============================================================================
  // FETCH BOARDS
  // ============================================================================

  describe("fetchBoards", () => {
    it("fetches all boards", async () => {
      const mockBoards = [
        createMockBoard("1", "Board 1"),
        createMockBoard("2", "Board 2"),
      ];
      mockGetBoards.mockResolvedValue({
        data: { resources: mockBoards },
        error: null,
      });

      const store = useBoardsStore();
      await store.fetchBoards();

      expect(mockGetBoards).toHaveBeenCalled();
      expect(store.boards).toEqual(mockBoards);
    });
  });

  // ============================================================================
  // SELECT BOARD
  // ============================================================================

  describe("trySelectBoard", () => {
    it("selects board from local cache", async () => {
      const mockBoards = [createMockBoard("board-1", "Board 1")];
      mockGetBoards.mockResolvedValue({
        data: { resources: mockBoards },
        error: null,
      });
      mockGetBoard.mockResolvedValue({
        data: { resource: mockBoards[0] },
        error: null,
      });

      const store = useBoardsStore();
      await store.fetchBoards();

      const result = await store.trySelectBoard("board-1" as BoardId);

      expect(result).toBe(true);
      expect(store.selectedBoard?.id).toBe("board-1");
    });

    it("fetches board from API if not in cache", async () => {
      const mockBoard = createMockBoard("board-new", "New Board");
      mockGetBoard.mockResolvedValue({
        data: { resource: mockBoard },
        error: null,
      });

      const store = useBoardsStore();
      const result = await store.trySelectBoard("board-new" as BoardId);

      expect(mockGetBoard).toHaveBeenCalledWith("board-new");
      expect(result).toBe(true);
      expect(store.selectedBoard).toEqual(mockBoard);
    });

    it("returns false on error", async () => {
      mockGetBoard.mockResolvedValue({
        data: null,
        error: { status: 404, title: "Not found" },
      });

      const store = useBoardsStore();
      const result = await store.trySelectBoard("invalid" as BoardId);

      expect(result).toBe(false);
    });
  });

  /**
   * Who answers for a board that would not open.
   *
   * The page of a board stands the error page of the status in place of the
   * board, a 429 and a 5xx included, so the read it opens the board with takes
   * the refusal over: with the interceptor's plate on top, a guest refused
   * with a 429 read the same refusal twice, in two wordings. The other reads
   * of a board show nothing about a failure, so the interceptor stays their
   * voice: the board read again after a sign-in (TopicsList), and the board of
   * a topic (trySelectTopic).
   */
  describe("board refusals", () => {
    const answered = () =>
      mockGetBoard.mockResolvedValue({
        data: { resource: createMockBoard("board-1", "General") },
        error: null,
      });

    it("takes the refusal over on the read that opens a board for its page", async () => {
      answered();
      const store = useBoardsStore();

      await store.trySelectBoardByAlias("general", BOARD_PAGE_OWNS_REFUSAL);

      expect(BOARD_PAGE_OWNS_REFUSAL).toEqual({
        ownsRefusal: true,
        ownsRateLimit: true,
      });
      expect(mockGetBoard.mock.calls).toEqual([
        ["general", { ownsRefusal: true, ownsRateLimit: true }],
      ]);
    });

    it("hands the page the status of a refused read", async () => {
      mockGetBoard.mockResolvedValue({
        data: null,
        error: { status: 429, title: "Too Many Requests" },
      });
      const store = useBoardsStore();

      expect(
        await store.trySelectBoardByAlias("general", BOARD_PAGE_OWNS_REFUSAL),
      ).toEqual({ ok: false, status: 429 });
    });

    it("leaves the refusal to the interceptor when the board is read again", async () => {
      answered();
      const store = useBoardsStore();

      await store.trySelectBoardByAlias("general");

      expect(mockGetBoard).toHaveBeenCalledTimes(1);
      expect(mockGetBoard.mock.calls[0]?.[0]).toBe("general");
      expect(mockGetBoard.mock.calls[0]?.[1]).toBeUndefined();
    });

    it("leaves the refusal to the interceptor for the board of a topic", async () => {
      answered();
      const store = useBoardsStore();

      await store.trySelectBoard("board-1" as BoardId);

      expect(mockGetBoard.mock.calls).toEqual([["board-1"]]);
    });
  });

  // ============================================================================
  // SEARCH TOPICS
  // ============================================================================

  describe("searchTopics", () => {
    it("fetches topics for selected board without filters", async () => {
      const mockBoard = createMockBoard("board-1", "Board");
      const mockTopics = [createMockTopic("topic-1", "Topic 1", "board-1")];
      const mockAttachedTopics = [
        createMockTopic("attached-1", "Attached", "board-1"),
      ];

      mockGetBoard.mockResolvedValue({
        data: { resource: mockBoard },
        error: null,
      });
      mockGetTopics.mockImplementation((_boardId, query) => {
        if (query?.isAttached === true) {
          return Promise.resolve({
            data: { resources: mockAttachedTopics },
            error: null,
          });
        }
        return Promise.resolve({
          data: {
            resources: mockTopics,
            paging: { current: 1, pages: 1, total: 1 },
          },
          error: null,
        });
      });

      const store = useBoardsStore();
      await store.trySelectBoard("board-1" as BoardId);
      await store.searchTopics({ number: 1 });

      expect(store.topics?.resources).toEqual(mockTopics);
      expect(store.attachedTopics).toEqual(mockAttachedTopics);
    });

    it("fetches both attached and regular topics in parallel when no filters", async () => {
      const mockBoard = createMockBoard("board-1", "Board");
      mockGetBoard.mockResolvedValue({
        data: { resource: mockBoard },
        error: null,
      });
      mockGetTopics.mockResolvedValue({
        data: { resources: [] },
        error: null,
      });

      const store = useBoardsStore();
      await store.trySelectBoard("board-1" as BoardId);
      await store.searchTopics({ number: 1 });

      // Should be called twice - once for attached, once for regular
      expect(mockGetTopics).toHaveBeenCalledTimes(2);
    });

    it("fetches single query when filters active", async () => {
      const mockBoard = createMockBoard("board-1", "Board");
      mockGetBoard.mockResolvedValue({
        data: { resource: mockBoard },
        error: null,
      });
      mockGetTopics.mockResolvedValue({
        data: { resources: [], paging: { current: 1, pages: 1, total: 0 } },
        error: null,
      });

      const store = useBoardsStore();
      await store.trySelectBoard("board-1" as BoardId);
      await store.searchTopics({ search: "test", number: 1 });

      // Should be called once with unified query
      expect(mockGetTopics).toHaveBeenCalledTimes(1);
      expect(store.attachedTopics).toBeNull();
    });
  });

  // ============================================================================
  // WHO ANSWERS FOR A FAILED LISTING
  // ============================================================================

  /**
   * A board that would not load said so twice.
   *
   * TopicsList stands a strip where the rows would be, and the response
   * interceptor put its plate over it: on a 403, a 429 and a 5xx the reader got
   * one failure in two boxes. The strip is the box in the place he is looking,
   * so the reads that fill it take the refusal over — and then they owe him the
   * whole sentence, the seconds of a wait included, which a bare flag had no
   * way to carry.
   *
   * The reads that refresh the pinned block after a pin or a reorder stay out
   * of this: they show nothing at all about a failure, so the interceptor is
   * their only voice.
   */
  describe("searchTopics refusals", () => {
    /** What a read that carries its own refusal hands the client. */
    const OWNED = { ownsRefusal: true, ownsRateLimit: true };

    /** The options the client got for each read, in call order. */
    const flagsPerCall = () => mockGetTopics.mock.calls.map((call) => call[2]);

    /** A store with a board already chosen, ready to read its listing. */
    const storeOnBoard = () => {
      const store = useBoardsStore();
      store.selectedBoard = createMockBoard("board-1", "Board");
      return store;
    };

    it("takes the refusal over on both reads of an unfiltered listing", async () => {
      mockGetTopics.mockResolvedValue({
        data: { resources: [] },
        error: null,
      });
      const store = storeOnBoard();

      await store.searchTopics({ number: 1 });

      expect(flagsPerCall()).toEqual([OWNED, OWNED]);
    });

    it("takes the refusal over on the single read of a filtered listing", async () => {
      mockGetTopics.mockResolvedValue({
        data: { resources: [], paging: { current: 1, pages: 1, total: 0 } },
        error: null,
      });
      const store = storeOnBoard();

      await store.searchTopics({ search: "правила", number: 1 });

      expect(flagsPerCall()).toEqual([OWNED]);
    });

    it("leaves the refusal to the interceptor when the pinned block refreshes", async () => {
      const store = storeOnBoard();
      store.attachedTopics = [] as any;
      store.topics = {
        resources: [createMockTopic("topic-1", "Topic", "board-1")],
        paging: null,
      } as any;

      mockUpdateTopic.mockResolvedValue({ data: null, error: null });
      mockReorderPinnedTopics.mockResolvedValue({ data: null, error: null });
      mockGetTopics.mockResolvedValue({
        data: { resources: [] },
        error: null,
      });

      await store.togglePinTopic("topic-1");
      await store.reorderPinnedTopics(["topic-1"]);

      // Two refreshes after the pin, one after the reorder, and none of them
      // prints a word about a failure — the flag would leave the reader with
      // nothing at all.
      expect(flagsPerCall()).toEqual([undefined, undefined, undefined]);
    });

    it("prints what the server said about the refused listing", async () => {
      mockGetTopics.mockResolvedValue({
        data: null,
        error: {
          type: "",
          title: "Раздел закрыт для чтения",
          status: 403,
          traceId: "trace",
        },
      });
      const store = storeOnBoard();

      await store.searchTopics({ number: 1 });

      expect(store.topicsError).toBe("Раздел закрыт для чтения");
    });

    it("counts out the wait a rate limit asked for, in place of its title", async () => {
      mockGetTopics.mockResolvedValue({
        data: null,
        error: {
          type: "",
          title: "Слишком много запросов",
          status: 429,
          traceId: "trace",
          retryAfterSeconds: 30,
        },
      });
      const store = storeOnBoard();

      await store.searchTopics({ number: 1 });

      // The title names the refusal without the number, and the number is the
      // part a reader acts on.
      expect(store.topicsError).toBe(
        "Слишком много попыток, попробуйте через 30 секунд",
      );
    });

    it("falls back to the listing sentence when the answer named nothing", async () => {
      mockGetTopics.mockResolvedValue({
        data: null,
        error: { type: "", title: "", status: 500, traceId: "trace" },
      });
      const store = storeOnBoard();

      await store.searchTopics({ number: 1 });

      expect(store.topicsError).toBe("Не удалось загрузить топики");
    });

    it("names the refusal of the read that fills the rows", async () => {
      mockGetTopics.mockImplementation((_alias, query) =>
        Promise.resolve({
          data: null,
          error: {
            type: "",
            title: query?.isAttached
              ? "Закрепленные не читаются"
              : "Топики не читаются",
            status: 500,
            traceId: "trace",
          },
        }),
      );
      const store = storeOnBoard();

      await store.searchTopics({ number: 1 });

      // Both reads failed; the strip stands where the rows would be, so it is
      // the rows' refusal it is about.
      expect(store.topicsError).toBe("Топики не читаются");
    });

    it("clears the sentence once the listing loads", async () => {
      mockGetTopics.mockResolvedValue({
        data: null,
        error: { type: "", title: "", status: 500, traceId: "trace" },
      });
      const store = storeOnBoard();

      await store.searchTopics({ number: 1 });
      expect(store.topicsError).toBe("Не удалось загрузить топики");

      mockGetTopics.mockResolvedValue({
        data: { resources: [] },
        error: null,
      });
      await store.searchTopics({ number: 1 });

      // A failed read is not cached, so this asks again — and a sentence left
      // standing over the rows it got would be the older lie of the two.
      expect(store.topicsError).toBeNull();
    });
  });

  // ============================================================================
  // SELECT TOPIC
  // ============================================================================

  describe("trySelectTopic", () => {
    it("selects topic and its board", async () => {
      const mockTopic = createMockTopic("topic-1", "Topic", "board-1");
      const mockBoard = createMockBoard("board-1", "Board");

      mockGetTopic.mockResolvedValue({
        data: { resource: mockTopic },
        error: null,
      });
      mockGetBoard.mockResolvedValue({
        data: { resource: mockBoard },
        error: null,
      });

      const store = useBoardsStore();
      await store.trySelectTopic("topic-1" as TopicId);

      expect(store.selectedTopic).toEqual(mockTopic);
      expect(store.selectedBoard).toEqual(mockBoard);
    });

    it("clears previous topic while loading new one", async () => {
      const mockTopic1 = createMockTopic("topic-1", "Topic 1", "board-1");
      const mockTopic2 = createMockTopic("topic-2", "Topic 2", "board-1");

      mockGetTopic.mockResolvedValue({
        data: { resource: mockTopic2 },
        error: null,
      });
      mockGetBoard.mockResolvedValue({
        data: { resource: createMockBoard("board-1", "Board") },
        error: null,
      });

      const store = useBoardsStore();
      store.selectedTopic = mockTopic1;

      await store.trySelectTopic("topic-2" as TopicId);

      expect(store.selectedTopic).toEqual(mockTopic2);
    });
  });

  // ============================================================================
  // FETCH COMMENTS
  // ============================================================================

  describe("searchComments", () => {
    it("fetches comments for selected topic", async () => {
      const mockTopic = createMockTopic("topic-1", "Topic", "board-1");
      const mockComments = [createMockComment("comment-1", "Comment text")];

      mockGetTopic.mockResolvedValue({
        data: { resource: mockTopic },
        error: null,
      });
      mockGetBoard.mockResolvedValue({
        data: { resource: createMockBoard("board-1", "Board") },
        error: null,
      });
      mockGetComments.mockResolvedValue({
        data: {
          resources: mockComments,
          paging: { current: 1, pages: 1, total: 1 },
        },
        error: null,
      });

      const store = useBoardsStore();
      await store.trySelectTopic("topic-1" as TopicId);
      await store.searchComments({ number: 1 });

      expect(store.comments?.resources).toEqual(mockComments);
    });

    it("keeps the comments on screen while it reloads", async () => {
      const store = useBoardsStore();
      const shown = {
        resources: [createMockComment("old", "old")],
        paging: null,
      } as any;
      store.comments = shown;
      store.selectedTopic = createMockTopic("topic-1", "Topic", "board-1");

      mockGetComments.mockResolvedValue({
        data: { resources: [], paging: null },
        error: null,
      });

      // A change of page, filter or sort has no cached entry under its own key,
      // and clearing the list there wiped what the reader was looking at — the
      // opposite of the stale-while-revalidate this method exists for.
      const fetchPromise = store.searchComments({ number: 1 });
      // Reactive wrapper, so identity is not the question: what matters is that
      // the rows are still there.
      expect(store.comments).toStrictEqual(shown);

      await fetchPromise;
      expect(store.comments?.resources).toEqual([]);
    });
  });

  // ============================================================================
  // CREATE COMMENT
  // ============================================================================

  describe("createComment", () => {
    it("creates comment and adds to list", async () => {
      const mockTopic = createMockTopic("topic-1", "Topic", "board-1");
      const newComment = createMockComment("new-comment", "New comment");

      mockCreateComment.mockResolvedValue({
        data: newComment,
        error: null,
      });

      const store = useBoardsStore();
      store.selectedTopic = mockTopic;
      store.comments = {
        resources: [],
        paging: { current: 1, pages: 1, total: 0 },
      } as any;

      const result = await store.createComment("New comment");

      expect(mockCreateComment).toHaveBeenCalledWith("topic-1", {
        text: "New comment",
      });
      expect(result.data).toEqual(newComment);
      expect(store.comments?.resources).toContainEqual(newComment);
    });

    it("updates topic comment count", async () => {
      const mockTopic = createMockTopic("topic-1", "Topic", "board-1");
      mockTopic.commentsCount = asServed(5);

      mockCreateComment.mockResolvedValue({
        data: createMockComment("new", "New"),
        error: null,
      });

      const store = useBoardsStore();
      store.selectedTopic = mockTopic;
      store.comments = {
        resources: [],
        paging: { current: 1, pages: 1, total: 5 },
      } as any;

      await store.createComment("New comment");

      expect(store.selectedTopic?.commentsCount).toBe(asServed(6));
    });

    it("returns error if no topic selected", async () => {
      const store = useBoardsStore();
      const result = await store.createComment("Text");

      expect(result.error).toBeTruthy();
    });
  });

  // ============================================================================
  // UPDATE COMMENT
  // ============================================================================

  describe("updateComment", () => {
    it("updates comment in list", async () => {
      const originalComment = createMockComment("comment-1", "Original");
      const updatedComment = { ...originalComment, text: "Updated" };

      mockUpdateComment.mockResolvedValue({
        data: updatedComment,
        error: null,
      });

      const store = useBoardsStore();
      store.comments = { resources: [originalComment], paging: null } as any;

      await store.updateComment("comment-1", "Updated");

      expect(store.comments?.resources[0].text).toBe("Updated");
    });

    it("keeps the old text and returns the refusal when the server rejects the edit", async () => {
      const original = createMockComment("comment-1", "Original");
      const refusal = {
        type: "",
        title: "Комментарий не найден",
        status: 404,
        traceId: "t",
      };

      mockUpdateComment.mockResolvedValue({ data: null, error: refusal });

      const store = useBoardsStore();
      store.comments = { resources: [original], paging: null } as any;

      const { error } = await store.updateComment("comment-1", "Updated");

      expect(error).toBe(refusal);
      expect(store.comments?.resources[0].text).toBe("Original");
    });
  });

  // ============================================================================
  // DELETE COMMENT
  // ============================================================================

  describe("deleteComment", () => {
    it("marks comment as removed", async () => {
      const comment = createMockComment("comment-1", "To delete");

      mockDeleteComment.mockResolvedValue({ error: null });

      const store = useBoardsStore();
      store.comments = { resources: [comment], paging: null } as any;

      await store.deleteComment("comment-1");

      expect(store.comments?.resources[0].isRemoved).toBe(true);
    });

    it("leaves the comment in place and returns the refusal when the delete fails", async () => {
      const comment = createMockComment("comment-1", "To delete");
      const refusal = {
        type: "",
        title: "Недостаточно прав",
        status: 403,
        traceId: "t",
      };

      mockDeleteComment.mockResolvedValue({ data: null, error: refusal });

      const store = useBoardsStore();
      store.comments = { resources: [comment], paging: null } as any;

      const { error } = await store.deleteComment("comment-1");

      expect(error).toBe(refusal);
      expect(store.comments?.resources[0].isRemoved).toBeFalsy();
    });
  });

  // ============================================================================
  // LIKE/UNLIKE COMMENT
  // ============================================================================

  describe("likeComment", () => {
    it("adds like to comment", async () => {
      const comment = createMockComment("comment-1", "Comment");
      const like = { id: "like-1", username: "liker" };

      mockPostCommentLike.mockResolvedValue({
        data: like,
        error: null,
      });

      const store = useBoardsStore();
      store.comments = { resources: [comment], paging: null } as any;

      await store.likeComment("comment-1");

      expect(store.comments?.resources[0].likes).toContainEqual(like);
    });

    // The item that was pressed says the refusal, so it has to get it.
    it("hands a refused like back and adds nobody", async () => {
      const refusal = { type: "", title: "Вы уже поставили лайк", status: 409 };
      mockPostCommentLike.mockResolvedValue({ data: null, error: refusal });

      const store = useBoardsStore();
      store.comments = {
        resources: [createMockComment("comment-1", "Comment")],
        paging: null,
      } as any;

      const { error } = await store.likeComment("comment-1");

      expect(error).toBe(refusal);
      expect(store.comments?.resources[0].likes ?? []).toHaveLength(0);
    });
  });

  describe("unlikeComment", () => {
    it("removes like from comment", async () => {
      const like = { id: "like-1", username: "testuser" };
      const comment = {
        ...createMockComment("comment-1", "Comment"),
        likes: [like],
      };

      mockDeleteCommentLike.mockResolvedValue({ error: null });

      const store = useBoardsStore();
      store.comments = { resources: [comment], paging: null } as any;

      await store.unlikeComment("comment-1");

      expect(store.comments?.resources[0].likes).toHaveLength(0);
    });

    it("hands a refused unlike back and keeps the like", async () => {
      const like = { id: "like-1", username: "testuser" };
      const refusal = {
        type: "",
        title: "Вы еще не ставили лайк",
        status: 409,
      };
      mockDeleteCommentLike.mockResolvedValue({ error: refusal });

      const store = useBoardsStore();
      store.comments = {
        resources: [
          { ...createMockComment("comment-1", "Comment"), likes: [like] },
        ],
        paging: null,
      } as any;

      const { error } = await store.unlikeComment("comment-1");

      expect(error).toBe(refusal);
      expect(store.comments?.resources[0].likes).toEqual([like]);
    });
  });

  // ============================================================================
  // LIKE/UNLIKE TOPIC
  // ============================================================================

  describe("likeTopic", () => {
    it("adds like to topic", async () => {
      const topic = createMockTopic("topic-1", "Topic", "board-1");
      topic.likes = asServed([]);
      const like = { id: "like-1", username: "liker" };

      mockPostTopicLike.mockResolvedValue({
        data: like,
        error: null,
      });

      const store = useBoardsStore();
      store.selectedTopic = topic;

      await store.likeTopic("topic-1");

      expect(store.selectedTopic?.likes).toContainEqual(like);
    });
  });

  describe("unlikeTopic", () => {
    it("removes like from topic", async () => {
      const like = { id: "like-1", username: "testuser" };
      const topic = createMockTopic("topic-1", "Topic", "board-1");
      topic.likes = [like] as any;

      mockDeleteTopicLike.mockResolvedValue({ error: null });

      const store = useBoardsStore();
      store.selectedTopic = topic;

      await store.unlikeTopic("topic-1");

      expect(store.selectedTopic?.likes).toHaveLength(0);
    });
  });

  // ============================================================================
  // FETCH NEWS
  // ============================================================================

  describe("fetchNews", () => {
    it("fetches news topics", async () => {
      const mockNews = [createMockTopic("news-1", "News 1", "news-board")];
      mockGetNews.mockResolvedValue({
        data: { resources: mockNews },
        error: null,
      });

      const store = useBoardsStore();
      await store.fetchNews();

      expect(store.news).toEqual(mockNews);
    });

    it("sets null on error", async () => {
      mockGetNews.mockResolvedValue({
        data: null,
        error: null,
      });

      const store = useBoardsStore();
      await store.fetchNews();

      expect(store.news).toBeNull();
    });
  });

  // ============================================================================
  // PINNED TOPICS (moderator actions)
  // ============================================================================

  describe("Pinned topics", () => {
    it("hands the caller the problem document when the pin request fails", async () => {
      const store = useBoardsStore();
      store.selectedBoard = createMockBoard("board-1", "Board");
      store.topics = {
        resources: [createMockTopic("topic-1", "Topic", "board-1")],
        paging: null,
      } as any;

      mockUpdateTopic.mockResolvedValue({
        data: null,
        error: { type: "", title: "", status: 409, traceId: "trace" },
      });

      const result = await store.togglePinTopic("topic-1");

      expect(result.error?.status).toBe(409);
      // Nothing changed, so nothing is refetched.
      expect(mockGetTopics).not.toHaveBeenCalled();
    });

    it("reports a mutation it never sent as a problem document, not as a JS error", async () => {
      const store = useBoardsStore();

      const result = await store.reorderPinnedTopics(["topic-1"]);

      // The page reads this out with notifyFailure, which needs a status.
      expect(result.error).toBe(requestNotSent);
    });

    it("keeps the listing when the refresh after a successful pin fails", async () => {
      const store = useBoardsStore();
      store.selectedBoard = createMockBoard("board-1", "Board");
      store.attachedTopics = [] as any;
      store.topics = {
        resources: [createMockTopic("topic-1", "Topic", "board-1")],
        paging: null,
      } as any;

      mockUpdateTopic.mockResolvedValue({ data: null, error: null });
      mockGetTopics.mockResolvedValue({
        data: null,
        error: { type: "", title: "", status: 500, traceId: "trace" },
      });

      const result = await store.togglePinTopic("topic-1");

      // The pin landed; a failed refresh must not turn the board into
      // "Топиков пока нет".
      expect(result.error).toBeUndefined();
      expect(store.topics?.resources).toHaveLength(1);
      expect(store.attachedTopics).toEqual([]);
      // The interceptor said the failure, and the strip over the rows says
      // nothing on top of it.
      expect(store.topicsError).toBeNull();
    });

    /** A board listing read by the page, as TopicsList reads it. */
    const listed = async (
      query: Parameters<ReturnType<typeof useBoardsStore>["searchTopics"]>[0],
    ) => {
      const store = useBoardsStore();
      store.selectedBoard = createMockBoard("board-1", "Board");
      await store.searchTopics(query);
      return store;
    };

    // Found on the stand: a search for "GURPS" left one row, a pin put every
    // topic of the board back into the table, and the search still stood in
    // its field.
    it("reads the filtered listing again with its filters after a pin", async () => {
      const gurps = createMockTopic("topic-1", "GURPS", "board-1");
      mockGetTopics.mockResolvedValue({
        data: {
          resources: [gurps],
          paging: { current: 1, pages: 1, total: 1 },
        },
        error: null,
      });
      const store = await listed({ search: "GURPS", number: 1 });
      mockGetTopics.mockClear();

      const pinned = { ...gurps, isAttached: true };
      mockUpdateTopic.mockResolvedValue({ data: null, error: null });
      mockGetTopics.mockResolvedValue({
        data: {
          resources: [pinned],
          paging: { current: 1, pages: 1, total: 1 },
        },
        error: null,
      });
      const result = await store.togglePinTopic("topic-1");

      expect(result.data).toBe(true);
      expect(mockGetTopics).toHaveBeenCalledTimes(1);
      expect(mockGetTopics).toHaveBeenCalledWith(
        "board",
        { search: "GURPS", number: 1, size: 20 },
        undefined,
      );
      expect(store.topics?.resources).toEqual([pinned]);
      expect(store.attachedTopics).toBeNull();
    });

    it("reads the page on screen again after a pin, not the first one", async () => {
      mockGetTopics.mockResolvedValue({
        data: {
          resources: [createMockTopic("topic-1", "Topic", "board-1")],
          paging: { current: 2, pages: 2, total: 21 },
        },
        error: null,
      });
      const store = await listed({ number: 2 });
      mockGetTopics.mockClear();
      mockUpdateTopic.mockResolvedValue({ data: null, error: null });

      await store.togglePinTopic("topic-1");

      expect(mockGetTopics.mock.calls.map((call) => call[1])).toEqual([
        { isAttached: true },
        { number: 2, size: 20, isAttached: false },
      ]);
    });

    // A fresh listing in the cache is handed out without a request for half a
    // minute: a board left and opened again showed the pins as they were.
    it("forgets the cached listings after a pin", async () => {
      mockGetTopics.mockResolvedValue({
        data: {
          resources: [createMockTopic("topic-1", "Topic", "board-1")],
          paging: { current: 1, pages: 2, total: 21 },
        },
        error: null,
      });
      const store = await listed({ number: 2 });
      await store.searchTopics({ number: 1 });
      mockUpdateTopic.mockResolvedValue({ data: null, error: null });
      await store.togglePinTopic("topic-1");
      mockGetTopics.mockClear();

      await store.searchTopics({ number: 2 });

      expect(mockGetTopics).toHaveBeenCalledTimes(2);
    });

    it("forgets the cached listings after a reorder", async () => {
      mockGetTopics.mockResolvedValue({
        data: { resources: [createMockTopic("topic-1", "Topic", "board-1")] },
        error: null,
      });
      const store = await listed({ number: 1 });
      mockReorderPinnedTopics.mockResolvedValue({ data: null, error: null });
      await store.reorderPinnedTopics(["topic-1"]);
      mockGetTopics.mockClear();

      await store.searchTopics({ number: 1 });

      expect(mockGetTopics).toHaveBeenCalledTimes(2);
    });

    it("stands the pinned block in the saved order when the refresh after a reorder fails", async () => {
      const store = useBoardsStore();
      store.selectedBoard = createMockBoard("board-1", "Board");
      const first = createMockTopic("topic-1", "First", "board-1");
      const second = createMockTopic("topic-2", "Second", "board-1");
      store.attachedTopics = [first, second] as any;

      mockReorderPinnedTopics.mockResolvedValue({ data: null, error: null });
      mockGetTopics.mockResolvedValue({
        data: null,
        error: { type: "", title: "", status: 500, traceId: "trace" },
      });

      const result = await store.reorderPinnedTopics(["topic-2", "topic-1"]);

      // The server keeps the new order; the block in the old one would say it
      // does not.
      expect(result.error).toBeUndefined();
      expect(store.attachedTopics?.map((topic) => topic.id)).toEqual([
        "topic-2",
        "topic-1",
      ]);
    });
  });

  // ============================================================================
  // UPDATE TOPIC (edit form: text, title and the closed state together)
  // ============================================================================

  describe("updateTopicContent", () => {
    it("closes the topic in the same request as the text and shows the answer", async () => {
      const store = useBoardsStore();
      const open = createMockTopic("topic-1", "Topic", "board-1");
      store.selectedTopic = open;
      const closed = { ...open, title: "Renamed", isClosed: true };
      mockUpdateTopic.mockResolvedValue({
        data: { resource: closed },
        error: null,
      });

      const result = await store.updateTopicContent("topic-1", {
        title: "Renamed",
        description: "Text",
        isClosed: true,
      });

      expect(mockUpdateTopic).toHaveBeenCalledTimes(1);
      expect(mockUpdateTopic).toHaveBeenCalledWith("topic-1", {
        title: "Renamed",
        description: "Text",
        isClosed: true,
      });
      expect(result.error).toBeUndefined();
      expect(store.selectedTopic?.isClosed).toBe(true);
    });

    // The pin of the edit form rides in the same request, and the board
    // listing, which shows the title, the lock and the pin, is read anew the
    // next time it is opened.
    it("pins the topic in the same request and forgets the cached listings", async () => {
      mockGetTopics.mockResolvedValue({
        data: { resources: [createMockTopic("topic-1", "Topic", "board-1")] },
        error: null,
      });
      const store = useBoardsStore();
      store.selectedBoard = createMockBoard("board-1", "Board");
      await store.searchTopics({ number: 1 });
      const open = createMockTopic("topic-1", "Topic", "board-1");
      store.selectedTopic = open;
      mockUpdateTopic.mockResolvedValue({
        data: { resource: { ...open, isAttached: true } },
        error: null,
      });

      await store.updateTopicContent("topic-1", {
        title: "Topic",
        description: "Text",
        isAttached: true,
      });
      mockGetTopics.mockClear();
      await store.searchTopics({ number: 1 });

      expect(mockUpdateTopic).toHaveBeenCalledWith("topic-1", {
        title: "Topic",
        description: "Text",
        isAttached: true,
      });
      expect(store.selectedTopic?.isAttached).toBe(true);
      expect(mockGetTopics).toHaveBeenCalledTimes(2);
    });

    it("keeps the topic as it was when the server refuses the save", async () => {
      const store = useBoardsStore();
      const open = createMockTopic("topic-1", "Topic", "board-1");
      store.selectedTopic = open;
      const refusal = { type: "", title: "", status: 403, traceId: "t" };
      mockUpdateTopic.mockResolvedValue({ data: null, error: refusal });

      const result = await store.updateTopicContent("topic-1", {
        title: "Renamed",
        description: "Text",
        isClosed: true,
      });

      expect(result.error).toBe(refusal);
      expect(store.selectedTopic).toEqual(open);
    });
  });
});

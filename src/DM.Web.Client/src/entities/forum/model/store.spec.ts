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

import { useBoardsStore } from "./store";
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
    });
  });
});

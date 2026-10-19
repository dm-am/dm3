/**
 * @vitest-environment jsdom
 */

/**
 * The comments of the open topic in useBoardsStore: the read that keeps the
 * rows on screen, and the writes patched in from the server's answer.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  mockGetBoard,
  mockGetTopic,
  mockGetComments,
  mockCreateComment,
  mockUpdateComment,
  mockDeleteComment,
  mockPostCommentLike,
  mockDeleteCommentLike,
} from "./store.doubles";
import { useBoardsStore } from "./store";
import {
  createMockBoard,
  createMockTopic,
  createMockComment,
  createMockUser,
  freshStore,
  listOf,
  pageOf,
} from "./store.fixtures";
import type { TopicId } from "./types";
import { asServed } from "@/shared/api/models";

vi.mock("../api/forumApi", async () =>
  (await import("./store.doubles")).forumApi(),
);

// The viewer, as a real store and not a bare object: store.doubles.ts says why.
vi.mock("@/shared/stores/auth", async () =>
  (await import("./store.doubles")).authStore(),
);

describe("useBoardsStore, the comments of a topic", () => {
  beforeEach(freshStore);

  afterEach(() => {
    vi.restoreAllMocks();
  });

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
      const shown = listOf([createMockComment("old", "old")]);
      store.comments = shown;
      store.selectedTopic = createMockTopic("topic-1", "Topic", "board-1");

      mockGetComments.mockResolvedValue({
        data: { resources: [], paging: null },
        error: null,
      });

      // A new query has no cached entry, and the rows on screen must stay.
      const fetchPromise = store.searchComments({ number: 1 });
      // Reactive wrapper, so identity is not the question: what matters is that
      // the rows are still there.
      expect(store.comments).toStrictEqual(shown);

      await fetchPromise;
      expect(store.comments?.resources).toEqual([]);
    });
  });

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
      store.comments = listOf([], pageOf(0));

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
      store.comments = listOf([], pageOf(5));

      await store.createComment("New comment");

      expect(store.selectedTopic?.commentsCount).toBe(asServed(6));
    });

    it("returns error if no topic selected", async () => {
      const store = useBoardsStore();
      const result = await store.createComment("Text");

      expect(result.error).toBeTruthy();
    });
  });

  describe("updateComment", () => {
    it("updates comment in list", async () => {
      const originalComment = createMockComment("comment-1", "Original");
      const updatedComment = { ...originalComment, text: "Updated" };

      mockUpdateComment.mockResolvedValue({
        data: updatedComment,
        error: null,
      });

      const store = useBoardsStore();
      store.comments = listOf([originalComment]);

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
      store.comments = listOf([original]);

      const { error } = await store.updateComment("comment-1", "Updated");

      expect(error).toBe(refusal);
      expect(store.comments?.resources[0].text).toBe("Original");
    });
  });

  describe("deleteComment", () => {
    it("marks comment as removed", async () => {
      const comment = createMockComment("comment-1", "To delete");

      mockDeleteComment.mockResolvedValue({ error: null });

      const store = useBoardsStore();
      store.comments = listOf([comment]);

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
      store.comments = listOf([comment]);

      const { error } = await store.deleteComment("comment-1");

      expect(error).toBe(refusal);
      expect(store.comments?.resources[0].isRemoved).toBeFalsy();
    });
  });

  describe("likeComment", () => {
    it("adds like to comment", async () => {
      const comment = createMockComment("comment-1", "Comment");
      const like = { id: "like-1", username: "liker" };

      mockPostCommentLike.mockResolvedValue({
        data: like,
        error: null,
      });

      const store = useBoardsStore();
      store.comments = listOf([comment]);

      await store.likeComment("comment-1");

      expect(store.comments?.resources[0].likes).toContainEqual(like);
    });

    // The item that was pressed says the refusal, so it has to get it.
    it("hands a refused like back and adds nobody", async () => {
      const refusal = {
        type: "",
        title: "Вы уже отметили Нравится",
        status: 409,
      };
      mockPostCommentLike.mockResolvedValue({ data: null, error: refusal });

      const store = useBoardsStore();
      store.comments = listOf([createMockComment("comment-1", "Comment")]);

      const { error } = await store.likeComment("comment-1");

      expect(error).toBe(refusal);
      expect(store.comments?.resources[0].likes ?? []).toHaveLength(0);
    });
  });

  describe("unlikeComment", () => {
    it("removes like from comment", async () => {
      const comment = {
        ...createMockComment("comment-1", "Comment"),
        likes: asServed([createMockUser("like-1", "testuser")]),
      };

      mockDeleteCommentLike.mockResolvedValue({ error: null });

      const store = useBoardsStore();
      store.comments = listOf([comment]);

      await store.unlikeComment("comment-1");

      expect(store.comments?.resources[0].likes).toHaveLength(0);
    });

    it("hands a refused unlike back and keeps the like", async () => {
      const like = createMockUser("like-1", "testuser");
      const refusal = {
        type: "",
        title: "Вы еще не отмечали Нравится",
        status: 409,
      };
      mockDeleteCommentLike.mockResolvedValue({ error: refusal });

      const store = useBoardsStore();
      store.comments = listOf([
        {
          ...createMockComment("comment-1", "Comment"),
          likes: asServed([like]),
        },
      ]);

      const { error } = await store.unlikeComment("comment-1");

      expect(error).toBe(refusal);
      expect(store.comments?.resources[0].likes).toEqual([like]);
    });
  });
});

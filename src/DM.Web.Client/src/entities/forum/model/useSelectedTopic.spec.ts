/**
 * @vitest-environment jsdom
 */

/**
 * The open topic in useBoardsStore: its selection, its likes and its edit.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  mockGetBoard,
  mockGetTopics,
  mockGetTopic,
  mockPostTopicLike,
  mockDeleteTopicLike,
  mockUpdateTopic,
} from "./store.doubles";
import { useBoardsStore } from "./store";
import {
  createMockBoard,
  createMockTopic,
  createMockUser,
  freshStore,
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

describe("useBoardsStore, the selected topic", () => {
  beforeEach(freshStore);

  afterEach(() => {
    vi.restoreAllMocks();
  });

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

      const loading = store.trySelectTopic("topic-2" as TopicId);
      expect(store.selectedTopic).toBeNull();
      await loading;

      expect(store.selectedTopic).toEqual(mockTopic2);
    });
  });

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
      const topic = createMockTopic("topic-1", "Topic", "board-1");
      topic.likes = asServed([createMockUser("like-1", "testuser")]);

      mockDeleteTopicLike.mockResolvedValue({ error: null });

      const store = useBoardsStore();
      store.selectedTopic = topic;

      await store.unlikeTopic("topic-1");

      expect(store.selectedTopic?.likes).toHaveLength(0);
    });
  });

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

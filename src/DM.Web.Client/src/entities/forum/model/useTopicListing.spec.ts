/**
 * @vitest-environment jsdom
 */

/**
 * The listing of a board in useBoardsStore: its two reads, who answers for a
 * failed one, and the pins and the reorder that read it again.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  mockGetBoard,
  mockGetTopics,
  mockUpdateTopic,
  mockReorderPinnedTopics,
} from "./store.doubles";
import { useBoardsStore } from "./store";
import {
  createMockBoard,
  createMockTopic,
  freshStore,
  listOf,
} from "./store.fixtures";
import type { BoardId } from "./types";
import { requestNotSent } from "@/shared/lib/errors";

vi.mock("../api/forumApi", async () =>
  (await import("./store.doubles")).forumApi(),
);

// The viewer, as a real store and not a bare object: store.doubles.ts says why.
vi.mock("@/shared/stores/auth", async () =>
  (await import("./store.doubles")).authStore(),
);

describe("useBoardsStore, the topics of a board", () => {
  beforeEach(freshStore);

  afterEach(() => {
    vi.restoreAllMocks();
  });

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

  // The reads that fill the strip take the refusal over; the pinned block refreshes do not.
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
      store.attachedTopics = [];
      store.topics = listOf([createMockTopic("topic-1", "Topic", "board-1")]);

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

  describe("Pinned topics", () => {
    it("hands the caller the problem document when the pin request fails", async () => {
      const store = useBoardsStore();
      store.selectedBoard = createMockBoard("board-1", "Board");
      store.topics = listOf([createMockTopic("topic-1", "Topic", "board-1")]);

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
      store.attachedTopics = [];
      store.topics = listOf([createMockTopic("topic-1", "Topic", "board-1")]);

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
      store.attachedTopics = [first, second];

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
});

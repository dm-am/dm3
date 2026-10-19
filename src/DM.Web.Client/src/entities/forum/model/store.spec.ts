/**
 * @vitest-environment jsdom
 */

/**
 * useBoardsStore: its shape, the boards, the board selection and who answers
 * for a board that would not open, and the news.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mockGetBoards, mockGetBoard, mockGetNews } from "./store.doubles";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";
import { useBoardsStore } from "./store";
import { createMockBoard, createMockTopic, freshStore } from "./store.fixtures";
import type { BoardId } from "./types";

vi.mock("../api/forumApi", async () =>
  (await import("./store.doubles")).forumApi(),
);

// The viewer, as a real store and not a bare object: store.doubles.ts says why.
vi.mock("@/shared/stores/auth", async () =>
  (await import("./store.doubles")).authStore(),
);

describe("useBoardsStore", () => {
  beforeEach(freshStore);

  afterEach(() => {
    vi.restoreAllMocks();
  });

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

  // The board page takes the refusal over; the other reads of a board leave it to the interceptor.
  describe("board refusals", () => {
    const answered = () =>
      mockGetBoard.mockResolvedValue({
        data: { resource: createMockBoard("board-1", "General") },
        error: null,
      });

    it("takes the refusal over on the read that opens a board for its page", async () => {
      answered();
      const store = useBoardsStore();

      await store.trySelectBoardByAlias("general", OWNS_REFUSAL);

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
        await store.trySelectBoardByAlias("general", OWNS_REFUSAL),
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
  });
});

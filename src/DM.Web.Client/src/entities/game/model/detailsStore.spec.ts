/**
 * @vitest-environment jsdom
 */

/**
 * useGameDetailsStore: its shape, the lists of the game, the reset, what reads
 * across the slices, and the comments of the discussion.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import type { Character } from "./characterTypes";
import {
  mockGetGame,
  mockGetCharacters,
  mockDeleteGameComment,
} from "./store.doubles";
import { createMockGame, freshPinia } from "./store.fixtures";
import { useGameDetailsStore } from "./detailsStore";

vi.mock("../api/gameApi", async () =>
  (await import("./store.doubles")).gameApi(),
);

vi.mock("../api/characterApi", async () =>
  (await import("./store.doubles")).characterApi(),
);

vi.mock("../api/discussionApi", async () =>
  (await import("./store.doubles")).discussionApi(),
);

describe("useGameDetailsStore", () => {
  beforeEach(freshPinia);

  describe("loadCharacters", () => {
    it("loads characters for game", async () => {
      const mockCharacters: Character[] = [
        { id: "char-1", name: "Character 1" } as Character,
      ];
      mockGetCharacters.mockResolvedValue({
        data: { resources: mockCharacters },
        error: null,
      });

      const store = useGameDetailsStore();
      await store.loadCharacters("game-1");

      expect(store.characters).toEqual(mockCharacters);
    });
  });

  describe("reset", () => {
    it("resets all state", async () => {
      const mockGame = createMockGame("game-1", "Game");
      mockGetGame.mockResolvedValue({ data: mockGame, error: null });

      const store = useGameDetailsStore();
      await store.loadGame("game-1");

      store.reset();

      expect(store.game).toBeNull();
      expect(store.rooms).toEqual([]);
      expect(store.posts).toEqual([]);
      expect(store.characters).toEqual([]);
      expect(store.comments).toEqual([]);
    });
  });

  describe("comment mutations", () => {
    const refusal = {
      type: "",
      title: "Недостаточно прав",
      status: 403,
      traceId: "t",
    };

    it("leaves the comment untouched when the delete is refused", async () => {
      mockDeleteGameComment.mockResolvedValue({ data: null, error: refusal });

      const store = useGameDetailsStore();
      store.comments = [{ id: "c-1", text: "Текст" }] as never;

      const { error } = await store.deleteComment("c-1");

      expect(error).toBe(refusal);
      expect(store.comments[0].isRemoved).toBeFalsy();
    });
  });
});

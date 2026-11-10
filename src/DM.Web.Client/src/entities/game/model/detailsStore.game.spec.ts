/**
 * @vitest-environment jsdom
 */

/**
 * useGameDetailsStore: the game itself, the viewer's roles in it, the
 * subscription, and replies that land out of order.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import type { Room } from "./roomTypes";
import {
  mockGetGame,
  mockGetPosts,
  mockSubscribe,
  mockUnsubscribe,
} from "./store.doubles";
import { createMockGame, createMockPost, freshPinia } from "./store.fixtures";
import { useGameDetailsStore } from "./detailsStore";
import { deferred } from "@/shared/lib/testing/tasks";
import { asServed } from "@/shared/api/models";

vi.mock("../api/gameApi", async () =>
  (await import("./store.doubles")).gameApi(),
);

vi.mock("../api/postApi", async () =>
  (await import("./store.doubles")).postApi(),
);

describe("useGameDetailsStore", () => {
  beforeEach(freshPinia);

  describe("loadGame", () => {
    it("loads game by id", async () => {
      const mockGame = createMockGame("game-1", "Test Game");
      mockGetGame.mockResolvedValue({ data: mockGame, error: null });

      const store = useGameDetailsStore();
      await store.loadGame("game-1");

      expect(mockGetGame).toHaveBeenCalledWith("game-1");
      expect(store.game).toEqual(mockGame);
    });

    it("handles load error", async () => {
      mockGetGame.mockResolvedValue({
        data: null,
        error: { status: 404, title: "Not found" },
      });

      const store = useGameDetailsStore();
      await store.loadGame("invalid");

      expect(store.gameError).toBeTruthy();
      expect(store.game).toBeNull();
    });

    // The status is the whole point of the refusal: the shell reads it out of
    // the store (GamePage.vue) and picks the error page from it, so a load that
    // keeps the sentence and drops the number sends a reader who was refused
    // access to "ошибка сервера". Both halves are asserted here because the
    // shell's own spec sets gameErrorStatus by hand — deleting the line that
    // fills it left every other test in the tree green.
    it.each([403, 404, 410, 500])(
      "keeps the %i the server answered",
      async (status) => {
        mockGetGame.mockResolvedValue({
          data: null,
          error: { status, title: "Refused" },
        });

        const store = useGameDetailsStore();
        const result = await store.loadGame("invalid");

        expect(store.gameErrorStatus).toBe(status);
        expect(result).toEqual({ ok: false, status });
      },
    );

    it("reports a refusal that carries no status as one", async () => {
      mockGetGame.mockResolvedValue({
        data: null,
        error: { title: "Network is down" },
      });

      const store = useGameDetailsStore();
      const result = await store.loadGame("invalid");

      expect(store.gameErrorStatus).toBeNull();
      expect(result.ok).toBe(false);
    });

    it("tracks loading state", async () => {
      let resolvePromise: (value: unknown) => void;
      const promise = new Promise((resolve) => {
        resolvePromise = resolve;
      });
      mockGetGame.mockReturnValue(promise);

      const store = useGameDetailsStore();
      const loadPromise = store.loadGame("game-1");

      expect(store.gameLoading).toBe(true);

      resolvePromise!({ data: createMockGame("1", "Game"), error: null });
      await loadPromise;

      expect(store.gameLoading).toBe(false);
    });
  });

  describe("participation flags", () => {
    // The wire carries GameParticipation names (Owner/Authority/Moderator), not GameRole.
    it.each([
      [
        ["Owner", "Authority"],
        { isMaster: true, isAssistant: false, isPlayer: true },
      ],
      [["Authority"], { isMaster: false, isAssistant: true, isPlayer: false }],
      [["Moderator"], { isMaster: false, isAssistant: false, isPlayer: true }],
      [["Player"], { isMaster: false, isAssistant: false, isPlayer: true }],
      [["Reader"], { isMaster: false, isAssistant: false, isPlayer: false }],
    ])("reads %j", async (participation, expected) => {
      const mockGame = {
        ...createMockGame("game-1", "Test Game"),
        participation: asServed(participation),
      };
      mockGetGame.mockResolvedValue({ data: mockGame, error: null });

      const store = useGameDetailsStore();
      await store.loadGame("game-1");

      expect(store.isMaster).toBe(expected.isMaster);
      expect(store.isAssistant).toBe(expected.isAssistant);
      expect(store.isPlayer).toBe(expected.isPlayer);
    });

    it("treats the master as subscribed only when Reader is present", async () => {
      const mockGame = {
        ...createMockGame("game-1", "Test Game"),
        participation: asServed(["Owner", "Authority"]),
      };
      mockGetGame.mockResolvedValue({ data: mockGame, error: null });

      const store = useGameDetailsStore();
      await store.loadGame("game-1");

      expect(store.isSubscribed).toBe(false);
    });
  });

  describe("subscribe", () => {
    it("subscribes to game", async () => {
      const mockGame = createMockGame("game-1", "Game");
      mockSubscribe.mockResolvedValue({ error: null });
      mockGetGame.mockResolvedValue({ data: mockGame, error: null });

      const store = useGameDetailsStore();
      store.game = mockGame;

      const result = await store.subscribe();

      expect(mockSubscribe).toHaveBeenCalledWith("game-1");
      // Null is success: the mutators return the problem document on failure.
      expect(result).toBeNull();
    });

    it("reports a failure when there is no game to subscribe to", async () => {
      const store = useGameDetailsStore();
      const result = await store.subscribe();

      // Not null, so the caller shows its message instead of silently
      // reporting success.
      expect(result).not.toBeNull();
      expect(mockSubscribe).not.toHaveBeenCalled();
    });

    it("hands back what the server said", async () => {
      const refused = {
        type: "",
        title: "Вы в черном списке",
        status: 403,
        traceId: "t",
      };
      const mockGame = createMockGame("game-1", "Game");
      mockSubscribe.mockResolvedValue({ error: refused });

      const store = useGameDetailsStore();
      store.game = mockGame;

      expect(await store.subscribe()).toBe(refused);
    });
  });

  describe("unsubscribe", () => {
    it("unsubscribes from game", async () => {
      const mockGame = createMockGame("game-1", "Game");
      mockUnsubscribe.mockResolvedValue({ error: null });
      mockGetGame.mockResolvedValue({ data: mockGame, error: null });

      const store = useGameDetailsStore();
      store.game = mockGame;

      const result = await store.unsubscribe();

      expect(mockUnsubscribe).toHaveBeenCalledWith("game-1");
      expect(result).toBeNull();
    });
  });

  // A slower reply for the game the user left must not redraw the page under the new title.
  describe("out-of-order responses", () => {
    it("keeps the newer game when the older reply lands last", async () => {
      const older = deferred<unknown>();
      const newer = deferred<unknown>();
      mockGetGame
        .mockReturnValueOnce(older.promise)
        .mockReturnValueOnce(newer.promise);

      const store = useGameDetailsStore();
      const loadA = store.loadGame("game-a");
      const loadB = store.loadGame("game-b");

      newer.resolve({ data: createMockGame("game-b", "Game B"), error: null });
      await loadB;
      older.resolve({ data: createMockGame("game-a", "Game A"), error: null });
      await loadA;

      expect(store.game?.title).toBe("Game B");
    });

    it("lets the newer request own the spinner", async () => {
      const older = deferred<unknown>();
      const newer = deferred<unknown>();
      mockGetGame
        .mockReturnValueOnce(older.promise)
        .mockReturnValueOnce(newer.promise);

      const store = useGameDetailsStore();
      const loadA = store.loadGame("game-a");
      const loadB = store.loadGame("game-b");

      older.resolve({ data: createMockGame("game-a", "Game A"), error: null });
      await loadA;

      // Game B is still on the wire: hiding the spinner here presents an
      // unfinished load as finished, with the wrong game underneath it.
      expect(store.gameLoading).toBe(true);
      expect(store.game).toBeNull();

      newer.resolve({ data: createMockGame("game-b", "Game B"), error: null });
      await loadB;

      expect(store.gameLoading).toBe(false);
      expect(store.game?.title).toBe("Game B");
    });

    it("does not let a stale failure blank the game that loaded", async () => {
      const older = deferred<unknown>();
      const newer = deferred<unknown>();
      mockGetGame
        .mockReturnValueOnce(older.promise)
        .mockReturnValueOnce(newer.promise);

      const store = useGameDetailsStore();
      const loadA = store.loadGame("game-a");
      const loadB = store.loadGame("game-b");

      newer.resolve({ data: createMockGame("game-b", "Game B"), error: null });
      await loadB;
      older.resolve({ data: null, error: { status: 404, title: "Not found" } });
      await loadA;

      expect(store.game?.title).toBe("Game B");
      expect(store.gameError).toBeNull();
    });

    it("keeps the room header and its posts in step", async () => {
      const store = useGameDetailsStore();
      store.rooms = [
        { id: "room-2", title: "Room 2", roomNumber: 2 },
        { id: "room-3", title: "Room 3", roomNumber: 3 },
      ] as unknown as Room[];

      const postsOfTwo = [createMockPost("post-2", "Room 2")];
      const postsOfThree = [createMockPost("post-3", "Room 3")];

      const older = deferred<unknown>();
      const newer = deferred<unknown>();
      mockGetPosts
        .mockReturnValueOnce(older.promise)
        .mockReturnValueOnce(newer.promise);

      const loadTwo = store.loadPosts("room-2");
      const loadThree = store.loadPosts("room-3");

      newer.resolve({
        data: { resources: postsOfThree, paging: null },
        error: null,
      });
      await loadThree;
      older.resolve({
        data: { resources: postsOfTwo, paging: null },
        error: null,
      });
      await loadTwo;

      // currentRoom is assigned synchronously, the posts arrive later: the two
      // must not come from different requests.
      expect(store.currentRoom?.id).toBe("room-3");
      expect(store.posts).toEqual(postsOfThree);
    });

    it("drops a reply that arrives after the store was reset", async () => {
      const pending = deferred<unknown>();
      mockGetGame.mockReturnValueOnce(pending.promise);

      const store = useGameDetailsStore();
      const load = store.loadGame("game-a");

      // What GamePage does when the id in the URL changes, and on unmount.
      store.reset();

      pending.resolve({
        data: createMockGame("game-a", "Game A"),
        error: null,
      });
      await load;

      expect(store.game).toBeNull();
    });
  });
});

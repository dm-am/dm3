/**
 * @vitest-environment jsdom
 */

/**
 * useGameDetailsStore: the rooms of the game and the posts of the room being
 * read, its cold load, where it opens and its filters.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import {
  MAX_API_PAGE_SIZE,
  DEFAULT_PAGE_SIZE,
} from "@/shared/lib/composables/usePaging";
import type { Room } from "./roomTypes";
import type { Post } from "./postTypes";
import {
  mockGetRooms,
  mockGetPosts,
  mockGetFirstUnreadRoomPost,
  mockGetPost,
} from "./store.doubles";
import { freshPinia } from "./store.fixtures";
import { useGameDetailsStore } from "./detailsStore";
import { useAuthStore } from "@/shared/stores";
import { deferred } from "@/shared/lib/testing/tasks";

vi.mock("../api/roomApi", async () =>
  (await import("./store.doubles")).roomApi(),
);

vi.mock("../api/postApi", async () =>
  (await import("./store.doubles")).postApi(),
);

describe("useGameDetailsStore", () => {
  beforeEach(freshPinia);

  describe("loadRooms", () => {
    it("loads rooms for game", async () => {
      const mockRooms: Room[] = [
        { id: "room-1", title: "Room 1" } as Room,
        { id: "room-2", title: "Room 2" } as Room,
      ];
      mockGetRooms.mockResolvedValue({
        data: { resources: mockRooms },
        error: null,
      });

      const store = useGameDetailsStore();
      await store.loadRooms("game-1");

      expect(store.rooms).toEqual(mockRooms);
    });
  });

  describe("loadPosts", () => {
    it("loads posts for room", async () => {
      const mockPosts: Post[] = [
        {
          id: "post-1",
          gameText: "Post 1",
          createdUtc: "2024-01-01T00:00:00Z",
        } as Post,
      ];
      mockGetPosts.mockResolvedValue({
        data: {
          resources: mockPosts,
          paging: { current: 1, pages: 1, total: 1 },
        },
        error: null,
      });

      const store = useGameDetailsStore();
      store.rooms = [{ id: "room-1", title: "Room" } as Room];
      await store.loadPosts("room-1");

      expect(store.posts).toEqual(mockPosts);
      expect(store.currentRoom?.id).toBe("room-1");
    });

    it("loads specific page", async () => {
      mockGetPosts.mockResolvedValue({
        data: { resources: [], paging: { current: 2, pages: 3, total: 30 } },
        error: null,
      });

      const store = useGameDetailsStore();
      await store.loadPosts("room-1", 2);

      expect(mockGetPosts).toHaveBeenCalledWith(
        "room-1",
        {
          number: 2,
          take: DEFAULT_PAGE_SIZE,
        },
        undefined,
      );
    });

    // The "posts per page" preference was saved and never read: the room asked
    // for the same twenty whatever the reader had chosen.
    it("asks for the page size the reader chose", async () => {
      mockGetPosts.mockResolvedValue({
        data: { resources: [], paging: { current: 1, pages: 1, total: 5 } },
        error: null,
      });
      useAuthStore().user = {
        settings: { pageSize: 50 },
      } as never;

      const store = useGameDetailsStore();
      await store.loadPosts("room-1");

      expect(mockGetPosts).toHaveBeenCalledWith(
        "room-1",
        {
          number: 1,
          take: 50,
        },
        undefined,
      );
    });

    // 200 is a legal preference and an illegal page: unclamped it reaches the
    // API as take=200 and comes back 400, which the room draws as "Не удалось
    // загрузить посты".
    it("asks for no more than the API serves", async () => {
      mockGetPosts.mockResolvedValue({
        data: { resources: [], paging: { current: 1, pages: 1, total: 5 } },
        error: null,
      });
      // Above every size the settings offer: the cap is what the API serves, and
      // a preference it refuses would be a setting that quietly does nothing.
      useAuthStore().user = {
        settings: { pageSize: 500 },
      } as never;

      const store = useGameDetailsStore();
      await store.loadPosts("room-1");

      expect(mockGetPosts).toHaveBeenCalledWith(
        "room-1",
        {
          number: 1,
          take: MAX_API_PAGE_SIZE,
        },
        undefined,
      );
    });
  });

  /**
   * Opening a room by its address, rather than by a click inside the site,
   * mounts two loaders that both need the rooms list: the game shell preloads
   * it for navigation, and the room page resolves the number in the URL
   * against it. The child mounts first, so the room page asked for the list,
   * and the shell asked for it again a moment later.
   *
   * Two requests, and the guard keeps one answer. Whichever request lost the
   * race had an awaiter attached to it, and that awaiter came back to an empty
   * list: the room page reported "Комната №N не найдена" and never asked for
   * the posts at all. A refresh of a room showed an empty room; the same room
   * reached by a link showed its posts.
   */
  describe("cold load of a room", () => {
    const ROOMS = [
      { id: "room-1", title: "Главная сцена", roomNumber: 1 },
      { id: "room-2", title: "За ширмой", roomNumber: 2 },
    ] as unknown as Room[];

    const POSTS = [
      {
        id: "post-1",
        gameText: "Первый пост",
        createdUtc: "2024-01-01T00:00:00Z",
      },
    ] as unknown as Post[];

    beforeEach(() => {
      mockGetRooms.mockResolvedValue({
        data: { resources: ROOMS },
        error: null,
      });
      mockGetPosts.mockResolvedValue({
        data: { resources: POSTS, paging: null },
        error: null,
      });
    });

    it("asks for the rooms once and goes on to the posts", async () => {
      const store = useGameDetailsStore();

      // Mount order, and the whole bug: the room page runs its onMounted
      // before the shell above it runs its own.
      const roomPage = store.loadPostsByRoomNumber("game-1", 1);
      const shell = store.loadRooms("game-1");
      await Promise.all([roomPage, shell]);

      expect(mockGetRooms).toHaveBeenCalledTimes(1);
      expect(mockGetPosts).toHaveBeenCalledWith(
        "room-1",
        expect.anything(),
        undefined,
      );
      expect(store.posts).toEqual(POSTS);
      expect(store.postsError).toBeNull();
      expect(store.currentRoom?.id).toBe("room-1");
    });

    it("waits rather than reporting an empty room while the list is on the wire", async () => {
      const rooms = deferred<unknown>();
      mockGetRooms.mockReturnValueOnce(rooms.promise);

      const store = useGameDetailsStore();
      const roomPage = store.loadPostsByRoomNumber("game-1", 1);

      // The page draws this moment. Loading is the honest answer; an empty
      // list under "В этой комнате пока нет постов" is not.
      expect(store.postsLoading).toBe(true);

      rooms.resolve({ data: { resources: ROOMS }, error: null });
      await roomPage;

      expect(store.postsLoading).toBe(false);
      expect(store.posts).toEqual(POSTS);
    });

    it("still names a room number the game does not have", async () => {
      // The join must not swallow the genuine miss it was covering for.
      const store = useGameDetailsStore();
      await store.loadPostsByRoomNumber("game-1", 7);

      expect(store.postsError).toBe("Комната №7 не найдена");
      expect(store.postsLoading).toBe(false);
      expect(mockGetPosts).not.toHaveBeenCalled();
    });

    it("reports a load failure as a failure, not as a missing room", async () => {
      // A network drop is not a missing room: nobody could ask.
      mockGetRooms.mockResolvedValueOnce({
        data: null,
        error: { status: 503, title: "unavailable" },
      });

      const store = useGameDetailsStore();
      await store.loadPostsByRoomNumber("game-1", 1);

      expect(store.postsError).toBe("Не удалось загрузить комнаты");
      expect(store.postsError).not.toContain("не найдена");
      expect(store.postsLoading).toBe(false);
      expect(mockGetPosts).not.toHaveBeenCalled();
    });

    it("starts a fresh rooms request instead of joining one being discarded", async () => {
      const leaving = deferred<unknown>();
      mockGetRooms.mockReturnValueOnce(leaving.promise);

      const store = useGameDetailsStore();
      const abandoned = store.loadRooms("game-1");

      // GamePage on an id change: the reply on the wire is about to be
      // dropped, so a caller after this point must not attach to it.
      store.reset();

      const afterReset = store.loadRooms("game-1");
      leaving.resolve({ data: { resources: ROOMS }, error: null });
      await Promise.all([abandoned, afterReset]);

      expect(mockGetRooms).toHaveBeenCalledTimes(2);
      expect(store.rooms).toEqual(ROOMS);
    });
  });

  /**
   * The room page asks the store where a room opens and what a filtered page
   * holds. The filter goes to the API as it came, and both locations are
   * counted at the reader's own page size, the same size the page is then
   * listed at: a page counted at another size lands on the wrong page.
   */
  describe("room opening and filters", () => {
    const ROOMS = [
      { id: "room-1", title: "Главная сцена", roomNumber: 1 },
    ] as unknown as Room[];

    beforeEach(() => {
      mockGetRooms.mockResolvedValue({
        data: { resources: ROOMS },
        error: null,
      });
      mockGetPosts.mockResolvedValue({
        data: { resources: [], paging: null },
        error: null,
      });
      useAuthStore().user = {
        settings: { pageSize: 25 },
      } as never;
    });

    it("hands the filter of the page to the API", async () => {
      const store = useGameDetailsStore();
      await store.loadPostsByRoomNumber("game-1", 1, 2, {
        search: "при",
        withoutCharacter: true,
      });

      expect(mockGetPosts).toHaveBeenCalledWith(
        "room-1",
        { number: 2, take: 25 },
        { search: "при", withoutCharacter: true },
      );
    });

    it("asks where the room opens at the reader's page size", async () => {
      mockGetFirstUnreadRoomPost.mockResolvedValue({
        data: {
          resource: {
            postId: "post-51",
            postNumber: 51,
            page: 3,
            pageSize: 25,
            totalUnreadCount: 4,
            hasUnread: true,
          },
        },
        error: null,
      });

      const found = await useGameDetailsStore().locateFirstUnreadPost(
        "game-1",
        1,
      );

      expect(mockGetFirstUnreadRoomPost).toHaveBeenCalledWith("room-1", 25);
      expect(found).toEqual({ page: 3, postId: "post-51", hasUnread: true });
    });

    it("has nowhere to land in an empty room or without an answer", async () => {
      const store = useGameDetailsStore();
      mockGetFirstUnreadRoomPost.mockResolvedValue({
        data: {
          resource: {
            postNumber: 0,
            page: 1,
            pageSize: 25,
            totalUnreadCount: 0,
            hasUnread: false,
          },
        },
        error: null,
      });
      expect(await store.locateFirstUnreadPost("game-1", 1)).toBeNull();

      mockGetFirstUnreadRoomPost.mockResolvedValue({
        data: null,
        error: { status: 500 },
      });
      expect(await store.locateFirstUnreadPost("game-1", 1)).toBeNull();
    });

    it("finds the page of a post from its number in the room", async () => {
      mockGetPost.mockResolvedValue({
        data: { resource: { id: "post-51", number: 51 } },
        error: null,
      });
      const store = useGameDetailsStore();

      expect(await store.locatePostPage("post-51")).toBe(3);
      expect(store.pageOfPostNumber(50)).toBe(2);
      expect(store.pageOfPostNumber(1)).toBe(1);
    });
  });
});

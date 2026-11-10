// The jump to the first unread post names the page the room will draw.
import { describe, it, expect, beforeEach, vi } from "vitest";
import { flushPromises } from "@vue/test-utils";
import type { Router } from "vue-router";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/entities/user";
import { mountPage } from "@/shared/lib/testing/mountPage";
import GameFirstUnreadPost from "./GameFirstUnreadPost.vue";

const { mockGetFirstUnreadPost, mockGetRooms } = vi.hoisted(() => ({
  mockGetFirstUnreadPost: vi.fn(),
  mockGetRooms: vi.fn(),
}));

vi.mock("@/entities/game/api/postApi", () => ({
  default: { getFirstUnreadPost: mockGetFirstUnreadPost },
}));

vi.mock("@/entities/game/api/roomApi", () => ({
  default: { getRooms: mockGetRooms },
}));

let pinia: ReturnType<typeof createPinia>;
/** The router of the screen last opened. */
let router: Router;

/** A reader with this many posts per page saved. */
function reader(pageSize: number) {
  useAuthStore().user = { settings: { pageSize } } as never;
}

/** Mounts the screen at its address and returns where it went. */
async function open() {
  ({ router } = await mountPage(GameFirstUnreadPost, "/game/g-1/posts/unread", {
    routes: [
      ["game-first-unread-post", "/game/:id/posts/unread"],
      ["game", "/game/:id"],
      ["game-room", "/game/:id/rooms/:num"],
    ],
    pinia,
  }));
  await flushPromises();
  const { name, params, query } = router.currentRoute.value;
  return { name, params, query };
}

/** Mounts the screen on an unread post and returns the route it redirected to. */
async function jumpTo(postNumber: number) {
  mockGetFirstUnreadPost.mockResolvedValue({
    data: {
      resource: {
        roomId: "r-1",
        postNumber,
        postId: "p-77",
        totalUnreadCount: 1,
        hasUnread: true,
      },
    },
    error: null,
  });
  mockGetRooms.mockResolvedValue({
    data: { resources: [{ id: "r-1", roomNumber: 3 }], paging: null },
    error: null,
  });

  return open();
}

describe("GameFirstUnreadPost", () => {
  beforeEach(() => {
    pinia = createPinia();
    setActivePinia(pinia);
    vi.clearAllMocks();
  });

  it("counts the page in the reader's own page size", async () => {
    reader(10);

    const target = await jumpTo(25);

    expect(target.query.number).toBe("3");
  });

  it("names the page under the key the room reads", async () => {
    reader(20);

    const target = await jumpTo(25);

    expect(target).toEqual({
      name: "game-room",
      params: { id: "g-1", num: "3" },
      query: { number: "2", scrollTo: "p-77" },
    });
  });

  it("writes no page at all when the post is on the first one", async () => {
    reader(50);

    const target = await jumpTo(25);

    expect(target.query).toEqual({ scrollTo: "p-77" });
  });

  // With the jump in the history, Back would land on it and it would jump again.
  it("takes the place of the jump, so Back does not land on it", async () => {
    reader(20);
    await jumpTo(25);

    router.back();
    await flushPromises();

    expect(router.currentRoute.value.name).toBe("game-room");
  });

  it("opens the game itself when the request failed", async () => {
    mockGetFirstUnreadPost.mockResolvedValue({
      data: null,
      error: { status: 500, title: "" },
    });

    expect(await open()).toEqual({
      name: "game",
      params: { id: "g-1" },
      query: {},
    });
  });
});

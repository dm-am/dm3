// The jump to the first unread post names the page the room will draw.
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/entities/user";
import GameFirstUnreadPost from "./GameFirstUnreadPost.vue";

const { mockGetFirstUnreadPost, mockGetRooms, mockReplace } = vi.hoisted(
  () => ({
    mockGetFirstUnreadPost: vi.fn(),
    mockGetRooms: vi.fn(),
    mockReplace: vi.fn(),
  }),
);

vi.mock("@/entities/game/api/postApi", () => ({
  default: { getFirstUnreadPost: mockGetFirstUnreadPost },
}));

vi.mock("@/entities/game/api/roomApi", () => ({
  default: { getRooms: mockGetRooms },
}));

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { id: "g-1" }, query: {} }),
  useRouter: () => ({ replace: mockReplace }),
}));

let pinia: ReturnType<typeof createPinia>;

/** A reader with this many posts per page saved. */
function reader(pageSize: number) {
  useAuthStore().user = { settings: { pageSize } } as never;
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

  mount(GameFirstUnreadPost, { global: { plugins: [pinia] } });
  await flushPromises();
  return mockReplace.mock.calls[0][0];
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
      params: { id: "g-1", num: 3 },
      query: { number: "2", scrollTo: "p-77" },
    });
  });

  it("writes no page at all when the post is on the first one", async () => {
    reader(50);

    const target = await jumpTo(25);

    expect(target.query).toEqual({ scrollTo: "p-77" });
  });

  it("opens the game itself when the request failed", async () => {
    mockGetFirstUnreadPost.mockResolvedValue({
      data: null,
      error: { status: 500, title: "" },
    });

    mount(GameFirstUnreadPost, { global: { plugins: [pinia] } });
    await flushPromises();

    expect(mockReplace).toHaveBeenCalledWith({
      name: "game",
      params: { id: "g-1" },
    });
  });
});

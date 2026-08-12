/**
 * The jump to the first unread post has to name the page the room will draw:
 * counted in the page size the room asks the API for, and handed over under the
 * query key the room reads.
 *
 * It used to divide by a literal 20 and write "?page=", a key the room never
 * reads, so the computed page was dropped and every jump landed on page one.
 * The twin screen for comments (GameFirstUnreadComment.vue) had already been
 * fixed both ways; this pins the same two facts here.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import GameFirstUnreadPost from "./GameFirstUnreadPost.vue";

const { mockGetFirstUnreadPost, mockGetRooms, mockReplace } = vi.hoisted(
  () => ({
    mockGetFirstUnreadPost: vi.fn(),
    mockGetRooms: vi.fn(),
    mockReplace: vi.fn(),
  }),
);

vi.mock("@/entities/game/api/gameApi", () => ({
  default: {
    getFirstUnreadPost: mockGetFirstUnreadPost,
    getRooms: mockGetRooms,
  },
}));

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { id: "g-1" }, query: {} }),
  useRouter: () => ({ replace: mockReplace }),
}));

let pinia: ReturnType<typeof createPinia>;

/** A reader with this many posts per page saved. */
function reader(postsPerPage: number) {
  useAuthStore().user = { settings: { paging: { postsPerPage } } } as never;
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
});

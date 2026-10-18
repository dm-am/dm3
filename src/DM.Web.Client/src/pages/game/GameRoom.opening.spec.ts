/**
 * @vitest-environment jsdom
 */

/**
 * Where the room opens and what reading it does.
 *
 * An address without a page used to open the first page, at its top, and then
 * mark the whole room read, so a reader coming back to a long room had to find
 * where he stopped by hand and could not ask again. It now asks the server
 * where the reader stopped before anything is marked, lands on that page with
 * the post at the top, and marks it the way the forum marks the comment it
 * brings the reader to. A filtered or re-sorted view is not reading the room:
 * it marks nothing, and the number of a post in it leads to the post's own
 * page. These run the page on a real router, so each landing is checked by
 * the address it ends on and the requests it made.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { defineComponent, h, type Ref } from "vue";
import { mount, flushPromises } from "@vue/test-utils";
import { createRouter, createMemoryHistory, type Router } from "vue-router";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/shared/stores/auth";
import { setScrollContainer } from "@/shared/lib/scroll";
import { provideZoneMarker } from "@/shared/lib/composables/useZoneSection";
import GameRoom from "./GameRoom.vue";

const api = vi.hoisted(() => ({
  getRooms: vi.fn(),
  getPosts: vi.fn(),
  getCharacters: vi.fn(),
  markRoomAsRead: vi.fn(),
  getFirstUnreadRoomPost: vi.fn(),
  getPost: vi.fn(),
  createPendency: vi.fn(),
  deletePendency: vi.fn(),
}));

vi.mock("@/entities/game/api/gameApi", () => ({ default: api }));

const PAGE_SIZE = 10;

const room = {
  id: "r-1",
  roomNumber: 1,
  title: "Главная сцена",
  unreadPostsCount: 0,
  isArchived: false,
  settings: { diceEnabled: false },
  pendencies: [],
};

/** Posts numbered in the room, `count` of them from `first`. */
function postsFrom(first: number, count: number) {
  return Array.from({ length: count }, (_, i) => ({
    id: `p-${first + i}`,
    number: first + i,
    createdUtc: "2026-02-01T10:00:00Z",
    gameText: "текст",
  }));
}

/** The page of the room the request asked for, the way the server pages it. */
function pageAnswer(page: number, total = 35) {
  const skip = (page - 1) * PAGE_SIZE;
  return {
    data: {
      resources: postsFrom(skip + 1, Math.min(PAGE_SIZE, total - skip)),
      paging: {
        skip,
        take: PAGE_SIZE,
        total,
        current: page,
        pages: Math.ceil(total / PAGE_SIZE),
      },
    },
    error: null,
  };
}

// The post as the room draws it, down to the card the mark goes on.
const GamePostStub = defineComponent({
  props: { post: { type: Object, required: true }, number: Number },
  setup(props) {
    return () =>
      h("article", { id: `post-${props.post.id}`, class: "game-post" }, [
        h("div", { class: "post-card" }, String(props.number ?? "")),
      ]);
  },
});

let scrollTo: ReturnType<typeof vi.fn>;

/** The page scrolls a container of its own, registered by the app shell. */
function registerScrollContainer() {
  const container = document.createElement("div");
  scrollTo = vi.fn();
  container.scrollTo = scrollTo as unknown as HTMLElement["scrollTo"];
  setScrollContainer(container);
}
let marker: Ref<string | undefined>;

async function openRoom(
  query: Record<string, string> = {},
  { signedIn = true } = {},
): Promise<Router> {
  const pinia = createPinia();
  setActivePinia(pinia);
  useAuthStore().user = signedIn
    ? ({
        username: "reader",
        settings: { paging: { postsPerPage: PAGE_SIZE } },
      } as never)
    : null;

  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: "/game/:id", name: "game", component: { render: () => null } },
      {
        path: "/game/:id/rooms/:num",
        name: "game-room",
        component: { render: () => null },
      },
    ],
  });
  await router.push({
    name: "game-room",
    params: { id: "g-1", num: "1" },
    query,
  });
  await router.isReady();

  // The shell: the marker it draws after the heading.
  const Shell = defineComponent({
    setup() {
      marker = provideZoneMarker();
      return () => h(GameRoom);
    },
  });
  mount(Shell, {
    global: {
      plugins: [router, pinia],
      stubs: {
        GamePost: GamePostStub,
        RoomPostFilter: true,
        BBCodeEditor: true,
        GamePostSkeleton: true,
        PagingWithSeparators: true,
        ErrorState: true,
        Select: true,
        SvgIcon: true,
      },
    },
    attachTo: document.body,
  });
  await flushPromises();
  await new Promise((resolve) => setTimeout(resolve, 20));
  await flushPromises();
  return router;
}

const requestedPages = () =>
  api.getPosts.mock.calls.map(([, paging]) => paging.number);

describe("GameRoom opening", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    document.body.innerHTML = "";
    localStorage.clear();

    registerScrollContainer();

    api.getRooms.mockResolvedValue({
      data: { resources: [room], paging: null },
      error: null,
    });
    api.getCharacters.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    api.getPosts.mockImplementation((_roomId, paging) =>
      Promise.resolve(pageAnswer(paging.number ?? 1)),
    );
    api.markRoomAsRead.mockResolvedValue({ data: null, error: null });
  });

  it("lands a bare address on the first unread post, marks it and only then the room", async () => {
    api.getFirstUnreadRoomPost.mockResolvedValue({
      data: {
        resource: {
          postId: "p-15",
          postNumber: 15,
          page: 2,
          pageSize: PAGE_SIZE,
          totalUnreadCount: 21,
          hasUnread: true,
        },
      },
      error: null,
    });

    const router = await openRoom();

    expect(api.getFirstUnreadRoomPost).toHaveBeenCalledWith("r-1", PAGE_SIZE);
    expect(router.currentRoute.value.query).toEqual({ number: "2" });
    expect(requestedPages()).toEqual([2]);
    expect(
      document.querySelector("#post-p-15 .post-card")?.classList,
    ).toContain("highlight-unread");
    expect(scrollTo).toHaveBeenCalled();
    expect(api.markRoomAsRead).toHaveBeenCalledWith("r-1");
    expect(api.getFirstUnreadRoomPost.mock.invocationCallOrder[0]).toBeLessThan(
      api.markRoomAsRead.mock.invocationCallOrder[0],
    );
  });

  it("lands on the last page, unmarked, when everything is read", async () => {
    api.getFirstUnreadRoomPost.mockResolvedValue({
      data: {
        resource: {
          postId: "p-35",
          postNumber: 35,
          page: 4,
          pageSize: PAGE_SIZE,
          totalUnreadCount: 0,
          hasUnread: false,
        },
      },
      error: null,
    });

    const router = await openRoom();

    expect(router.currentRoute.value.query).toEqual({ number: "4" });
    expect(requestedPages()).toEqual([4]);
    expect(document.querySelector(".highlight-unread")).toBeNull();
    expect(scrollTo).toHaveBeenCalled();
  });

  it("opens the first page when the server cannot say where to land", async () => {
    api.getFirstUnreadRoomPost.mockResolvedValue({
      data: null,
      error: { status: 404, title: "", type: "", traceId: "" },
    });

    const router = await openRoom();

    expect(router.currentRoute.value.query).toEqual({});
    expect(requestedPages()).toEqual([1]);
  });

  it("opens the first page for a guest without asking", async () => {
    await openRoom({}, { signedIn: false });

    expect(api.getFirstUnreadRoomPost).not.toHaveBeenCalled();
    expect(requestedPages()).toEqual([1]);
    expect(api.markRoomAsRead).not.toHaveBeenCalled();
  });

  it("opens the page an address names", async () => {
    await openRoom({ number: "3" });

    expect(api.getFirstUnreadRoomPost).not.toHaveBeenCalled();
    expect(requestedPages()).toEqual([3]);
    expect(api.markRoomAsRead).toHaveBeenCalledWith("r-1");
  });

  it("keeps the filtered view when where the room opens is answered after it", async () => {
    // The reader filters the room while the server is still saying where it
    // opens, and that answer comes back after the filtered list. Applied late,
    // it would move the address to its own page on top of the filter, and the
    // reader would be taken off the view he asked for.
    let answerOpening: (answer: unknown) => void = () => undefined;
    api.getFirstUnreadRoomPost.mockReturnValue(
      new Promise((resolve) => {
        answerOpening = resolve;
      }),
    );
    api.getPosts.mockImplementation((_roomId, paging, filter) =>
      Promise.resolve(
        filter && Object.keys(filter).length
          ? {
              data: {
                resources: [18, 30].map((n) => ({
                  id: `p-${n}`,
                  number: n,
                  createdUtc: "2026-02-01T10:00:00Z",
                  gameText: "при",
                })),
                paging: {
                  skip: 0,
                  take: PAGE_SIZE,
                  total: 2,
                  current: 1,
                  pages: 1,
                },
              },
              error: null,
            }
          : pageAnswer(paging.number ?? 1),
      ),
    );

    const router = await openRoom();
    expect(api.getFirstUnreadRoomPost).toHaveBeenCalledTimes(1);
    expect(api.getPosts).not.toHaveBeenCalled();

    await router.push({
      name: "game-room",
      params: { id: "g-1", num: "1" },
      query: { q: "при" },
    });
    await flushPromises();
    await new Promise((resolve) => setTimeout(resolve, 20));
    await flushPromises();

    answerOpening({
      data: {
        resource: {
          postId: "p-25",
          postNumber: 25,
          page: 3,
          pageSize: PAGE_SIZE,
          totalUnreadCount: 11,
          hasUnread: true,
        },
      },
      error: null,
    });
    await flushPromises();
    await new Promise((resolve) => setTimeout(resolve, 20));
    await flushPromises();

    expect(router.currentRoute.value.query).toEqual({ q: "при" });
    expect(requestedPages()).toEqual([1]);
    const numbers = [...document.querySelectorAll(".post-card")].map(
      (c) => c.textContent,
    );
    expect(numbers).toEqual(["18", "30"]);
    expect(document.querySelector(".highlight-unread")).toBeNull();
    expect(api.markRoomAsRead).not.toHaveBeenCalled();
  });
});

describe("GameRoom filtered view", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    document.body.innerHTML = "";
    registerScrollContainer();

    api.getRooms.mockResolvedValue({
      data: { resources: [room], paging: null },
      error: null,
    });
    api.getCharacters.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    api.markRoomAsRead.mockResolvedValue({ data: null, error: null });
    // The filtered list: three posts from all over the room, each with its
    // number in the room.
    api.getPosts.mockImplementation((_roomId, _paging, filter) =>
      Promise.resolve(
        filter && Object.keys(filter).length
          ? {
              data: {
                resources: [18, 30, 54].map((n) => ({
                  id: `p-${n}`,
                  number: n,
                  createdUtc: "2026-02-01T10:00:00Z",
                  gameText: "при",
                })),
                paging: {
                  skip: 0,
                  take: PAGE_SIZE,
                  total: 3,
                  current: 1,
                  pages: 1,
                },
              },
              error: null,
            }
          : pageAnswer(_paging.number ?? 1, 60),
      ),
    );
  });

  it("asks with the filter, draws the room numbers and leaves the room unread", async () => {
    await openRoom({
      q: "при",
      characters: "none",
      sort: "rating",
      order: "desc",
    });

    expect(api.getFirstUnreadRoomPost).not.toHaveBeenCalled();
    expect(api.getPosts).toHaveBeenCalledWith(
      "r-1",
      { number: 1, take: PAGE_SIZE },
      {
        search: "при",
        withoutCharacter: true,
        sortBy: "rating",
        sortOrder: "desc",
      },
    );
    const numbers = [...document.querySelectorAll(".post-card")].map(
      (c) => c.textContent,
    );
    expect(numbers).toEqual(["18", "30", "54"]);
    expect(api.markRoomAsRead).not.toHaveBeenCalled();
  });

  it("leads the number of a post to the post on its own page", async () => {
    const router = await openRoom({ q: "при" });

    // The number links to #post-{id}; following it is a change of hash.
    await router.push({ ...router.currentRoute.value, hash: "#post-p-18" });
    await flushPromises();
    await new Promise((resolve) => setTimeout(resolve, 20));
    await flushPromises();

    expect(router.currentRoute.value.query).toEqual({ number: "2" });
    expect(router.currentRoute.value.hash).toBe("#post-p-18");
    expect(api.getPosts).toHaveBeenLastCalledWith(
      "r-1",
      { number: 2, take: PAGE_SIZE },
      {},
    );
    // Back on the room itself, reading it marks it read.
    expect(api.markRoomAsRead).toHaveBeenCalledWith("r-1");
  });
});

describe("GameRoom heading", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    document.body.innerHTML = "";
    api.getCharacters.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
    api.getPosts.mockResolvedValue(pageAnswer(1));
    api.markRoomAsRead.mockResolvedValue({ data: null, error: null });
  });

  it("hands the archive marker to the shell and draws no title of its own", async () => {
    api.getRooms.mockResolvedValue({
      data: { resources: [{ ...room, isArchived: true }], paging: null },
      error: null,
    });
    await openRoom({ number: "1" });

    expect(marker.value).toBe("архив");
    expect(document.querySelector(".game-room h2")).toBeNull();
  });

  it("gives the shell no marker for a room in play", async () => {
    api.getRooms.mockResolvedValue({
      data: { resources: [room], paging: null },
      error: null,
    });
    await openRoom({ number: "1" });

    expect(marker.value).toBeUndefined();
  });
});

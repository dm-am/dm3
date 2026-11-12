/**
 * @vitest-environment jsdom
 */

/** "Мои игры" stars the game that waits for the viewer (GameRef.awaitsViewerTurn), and no other. */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import { nextTick } from "vue";
import { createPinia, setActivePinia } from "pinia";
import OwnedGames from "./OwnedGames.vue";
import {
  GameParticipation,
  useGamesStore,
  type GameRef,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { layOutRowsAs } from "./rememberedLines.fixtures";

const stubs = {
  RouterLink: RouterLinkStub,
  Tooltip: {
    template: "<span class='tooltip'><slot /></span>",
    props: ["text"],
  },
};

const game = (id: string, title: string, awaiting: string[] | null): GameRef =>
  ({
    id,
    publicId: id,
    title,
    status: "Active",
    master: { id: "u-1", username: "master" },
    assistants: [],
    participation: [GameParticipation.Player],
    subscribersCount: 0,
    recruitment: { isOpen: false, pcCount: 0, isSubsequent: false },
    unreadPostsCount: 0,
    unreadCommentsCount: 0,
    gameReviewsCount: 0,
    postReviewsCount: 0,
    awaitsViewerTurn: awaiting !== null,
    awaitedCharacterNames: awaiting ?? [],
  }) as unknown as GameRef;

function mountPanel(games: GameRef[]) {
  const store = useGamesStore();
  // The block is the signed-in viewer's.
  useAuthStore().user = { username: "viewer" } as never;
  // Straight into the store, the read answered by nothing.
  vi.spyOn(store, "fetchParticipatingGames").mockResolvedValue(
    undefined as never,
  );
  store.participatingGames = games;
  return mount(OwnedGames, { global: { stubs } });
}

describe("OwnedGames", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("stars only the game that awaits the viewer", () => {
    const wrapper = mountPanel([
      game("g-1", "Ждущая игра", ["Гром"]),
      game("g-2", "Спокойная игра", null),
    ]);

    const starred = wrapper
      .findAll("li.link")
      .filter((row) => row.find(".star").exists());

    expect(starred).toHaveLength(1);
    expect(starred[0].text()).toContain("Ждущая игра");
  });

  it("draws no star when nothing awaits the viewer", () => {
    const wrapper = mountPanel([game("g-2", "Спокойная игра", null)]);

    expect(wrapper.find(".star").exists()).toBe(false);
  });
});

/** The skeleton is as long as the block was at the viewer's last visit in this browser. */
describe("OwnedGames skeleton", () => {
  const KEY = "dm_sidebar_lines:OwnedGames:viewer";

  beforeEach(() => {
    setActivePinia(createPinia());
    localStorage.clear();
    vi.restoreAllMocks();
  });

  /** The block while its list is being read. */
  function mountLoading() {
    const store = useGamesStore();
    useAuthStore().user = { username: "viewer" } as never;
    vi.spyOn(store, "fetchParticipatingGames").mockResolvedValue(
      undefined as never,
    );
    store.participatingGames = null;
    store.participatingGamesLoading = true;
    return mount(OwnedGames, { global: { stubs } });
  }

  const rows = (wrapper: ReturnType<typeof mount>) =>
    wrapper.findAll(".sidebar-skeleton .skeleton-row").length;

  it("draws as many lines as the block had last time", () => {
    localStorage.setItem(KEY, "9");

    expect(rows(mountLoading())).toBe(9);
  });

  it("keeps the rows the list drew, a separator between two groups a row too", () => {
    const owned = {
      ...game("g-3", "Своя игра", null),
      participation: [GameParticipation.Owner],
    } as GameRef;
    mountPanel([
      owned,
      game("g-1", "Ждущая игра", ["Гром"]),
      game("g-2", "Спокойная игра", null),
    ]);

    expect(localStorage.getItem(KEY)).toBe("4");
  });

  // Each row is a line until it wraps: a long title takes two.
  it("keeps the lines the rows took on screen", async () => {
    layOutRowsAs(3);
    mountPanel([
      game("g-1", "Очень длинное название игры, которое не влезает", null),
      game("g-2", "Спокойная игра", null),
    ]);
    await nextTick();

    expect(localStorage.getItem(KEY)).toBe("3");
  });

  it("keeps one line for an empty list, its line of nothing yet", () => {
    mountPanel([]);

    expect(localStorage.getItem(KEY)).toBe("1");
  });

  it.each([
    ["nothing kept", null],
    ["a broken value", "много"],
    ["a fraction", "2.5"],
    ["zero", "0"],
  ])("falls back to five lines on %s", (_case, kept) => {
    if (kept !== null) localStorage.setItem(KEY, kept);

    expect(rows(mountLoading())).toBe(5);
  });

  it("falls back to five lines when the browser keeps nothing", () => {
    // Only the count's key: the block's fold state has its own reader.
    const { getItem, setItem } = Storage.prototype;
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(function (
      this: Storage,
      key: string,
    ) {
      if (key === KEY) throw new Error("blocked");
      return getItem.call(this, key);
    });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(function (
      this: Storage,
      key: string,
      value: string,
    ) {
      if (key === KEY) throw new Error("blocked");
      setItem.call(this, key, value);
    });

    expect(rows(mountLoading())).toBe(5);
    expect(() =>
      mountPanel([game("g-2", "Спокойная игра", null)]),
    ).not.toThrow();
  });

  it("reads another viewer's count for another viewer", () => {
    localStorage.setItem("dm_sidebar_lines:OwnedGames:other", "12");

    expect(rows(mountLoading())).toBe(5);
  });
});

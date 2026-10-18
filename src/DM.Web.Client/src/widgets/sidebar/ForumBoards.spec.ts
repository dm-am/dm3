/**
 * @vitest-environment jsdom
 */

/**
 * The forum block of the left sidebar: the boards as a list, each row a dash,
 * the name of the board and its unread count in brackets. It closes the
 * column on every page (LeftSidebar), the forum's included, and it is one
 * block wherever it stands.
 *
 * It is not the menu of a forum page and marks no board: the board the reader
 * is on is named by the strip under the heading of the forum
 * (pages/forum/BoardNavigation.vue), as before 5 October 2026. The wave of
 * that day made the block the forum's menu, first in the column and the board
 * of the page bold on a row of another shape; the owner turned it down on 12
 * October ("верни как было").
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { defineComponent } from "vue";
import { createMemoryHistory, createRouter } from "vue-router";
import { useBoardsStore, type Board } from "@/entities/forum";
import ForumBoards from "./ForumBoards.vue";

const Empty = defineComponent({ render: () => null });

const ROUTES = [
  { path: "/", name: "home", component: Empty },
  {
    path: "/forum",
    component: Empty,
    children: [
      { path: "", name: "forum-index", component: Empty },
      { path: ":alias", name: "forum", component: Empty },
      { path: ":alias/:num", name: "topic", component: Empty },
    ],
  },
];

const board = (alias: string, title: string, unread = 0): Board =>
  ({
    id: `id-${alias}`,
    alias,
    title,
    description: "",
    unreadCommentsCount: unread,
  }) as unknown as Board;

const BOARDS = [
  board("general", "Общий", 2),
  board("game-systems", "Игровые системы"),
  board("ideas", "Котел идей", 1),
];

async function blockAt(location: string, boards: Board[] | null = BOARDS) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: ROUTES,
  });
  await router.push(location);
  await router.isReady();

  const store = useBoardsStore();
  vi.spyOn(store, "fetchBoards").mockResolvedValue(undefined);
  store.boards = boards;

  const wrapper = mount(ForumBoards, {
    global: {
      plugins: [router],
      stubs: {
        Tooltip: { template: "<span><slot /></span>" },
      },
    },
  });
  await flushPromises();
  return wrapper;
}

describe("the forum block of the sidebar", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    localStorage.clear();
  });

  it("draws a row per board: a dash, the name, the count in brackets", async () => {
    const wrapper = await blockAt("/");
    const rows = wrapper.findAll("li.board-link");

    expect(rows.map((row) => row.text())).toEqual([
      "- Общий (2)",
      "- Игровые системы",
      "- Котел идей (1)",
    ]);
    // The dash is a mark of the list, not a word of it.
    const dash = rows[0].get(".muted");
    expect(dash.text()).toBe("-");
    expect(dash.attributes("aria-hidden")).toBe("true");
    // The name and the count both lead to the board.
    const links = rows[0].findAll("a");
    expect(links.map((a) => a.text())).toEqual(["Общий", "2"]);
    for (const a of links) expect(a.attributes("href")).toBe("/forum/general");
  });

  it("marks no board bold on a page of the forum, a board's or a topic's", async () => {
    for (const location of ["/forum", "/forum/general", "/forum/general/5"]) {
      const wrapper = await blockAt(location);

      expect(wrapper.findAll(".current"), location).toHaveLength(0);
      expect(
        wrapper.findComponent({ name: "MenuLink" }).exists(),
        location,
      ).toBe(false);
      wrapper.unmount();
    }
  });

  it("is one block on every page, folding under one key", async () => {
    for (const location of ["/", "/forum", "/forum/general/5"]) {
      const wrapper = await blockAt(location);

      expect(wrapper.find("#sidebar-list-ForumBoards").exists(), location).toBe(
        true,
      );
      expect(wrapper.find("#sidebar-list-ForumPanel").exists(), location).toBe(
        false,
      );
      wrapper.unmount();
    }
  });

  it("holds the place of the boards while they load", async () => {
    const wrapper = await blockAt("/forum/general", null);

    expect(wrapper.findAll(".skeleton-row")).toHaveLength(6);
  });
});

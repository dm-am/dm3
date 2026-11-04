/**
 * @vitest-environment jsdom
 */

/** The forum block: the boards as a list of dashed rows with their unread counts, one block everywhere. */
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

  it("is one block on every page, folding under one key", async () => {
    for (const location of ["/", "/forum", "/forum/general/5"]) {
      const wrapper = await blockAt(location);

      expect(wrapper.find("#sidebar-list-ForumBoards").exists(), location).toBe(
        true,
      );
      wrapper.unmount();
    }
  });

  it("holds the place of the boards while they load", async () => {
    const wrapper = await blockAt("/forum/general", null);

    expect(wrapper.findAll(".skeleton-row")).toHaveLength(6);
  });
});

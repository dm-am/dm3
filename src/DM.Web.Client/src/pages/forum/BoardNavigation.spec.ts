/**
 * @vitest-environment jsdom
 */

// The strip of boards under the forum heading, the page's board first and
// bold; it wraps like text, after a bar and never before one.
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { defineComponent } from "vue";
import { createMemoryHistory, createRouter } from "vue-router";
import { useBoardsStore, type Board } from "@/entities/forum";
import BoardNavigation from "./BoardNavigation.vue";

const Empty = defineComponent({ render: () => null });

const ROUTES = [
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

const board = (alias: string, title: string): Board =>
  ({ id: `id-${alias}`, alias, title, description: "" }) as unknown as Board;

const BOARDS = [
  board("general", "Общий"),
  board("game-systems", "Игровые системы"),
  board("ideas", "Котел идей"),
];

async function stripAt(location: string, boards: Board[] | null = BOARDS) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: ROUTES,
  });
  await router.push(location);
  await router.isReady();
  const store = useBoardsStore();
  store.boards = boards;
  const wrapper = mount(BoardNavigation, { global: { plugins: [router] } });
  await flushPromises();
  return wrapper;
}

describe("the strip of boards", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
  });

  it("names the boards in one run, the board of the page first and bold", async () => {
    const wrapper = await stripAt("/forum/game-systems");
    const nav = wrapper.get("nav");

    expect(nav.attributes("aria-label")).toBe("Разделы форума");
    expect(nav.text()).toBe("Игровые системы | Общий | Котел идей");
    const links = wrapper.findAll("a.board-link");
    expect(links.map((a) => a.text())).toEqual([
      "Игровые системы",
      "Общий",
      "Котел идей",
    ]);
    expect(links[0].classes()).toContain("active");
    expect(links[0].attributes("aria-current")).toBe("page");
    expect(links.slice(1).map((a) => a.classes("active"))).toEqual([
      false,
      false,
    ]);
    expect(links[1].attributes("href")).toBe("/forum/general");
  });

  it("marks the board of a topic as well", async () => {
    const wrapper = await stripAt("/forum/ideas/5");

    expect(wrapper.get("a.board-link.active").text()).toBe("Котел идей");
    expect(wrapper.findAll("a.board-link")[0].text()).toBe("Котел идей");
  });

  it("breaks a line after a bar only, never before one", async () => {
    const wrapper = await stripAt("/forum/general");
    const nav = wrapper.get("nav").element;

    // A board and its bar are one item; the space between items is the one break.
    const items = wrapper.findAll(".board-item");
    expect(items.map((item) => item.element.textContent)).toEqual([
      "Общий |",
      "Игровые системы |",
      "Котел идей",
    ]);
    const between = [...nav.children]
      .filter((el) => !el.classList.contains("board-item"))
      .map((el) => el.textContent);
    expect(between).toEqual([" ", " "]);
    for (const one of wrapper.findAll(".separator"))
      expect(one.attributes("aria-hidden")).toBe("true");
    expect(nav.textContent).not.toContain("\u00a0");
  });

  it("is not drawn before the boards are there", async () => {
    const wrapper = await stripAt("/forum/general", null);

    expect(wrapper.find("nav").exists()).toBe(false);
  });
});

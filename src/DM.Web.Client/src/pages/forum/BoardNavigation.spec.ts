/**
 * @vitest-environment jsdom
 */

/**
 * The strip of boards under the heading of the forum: "Общий | Игровые
 * системы | ...", the board of the page first and bold, as it stood before
 * 5 October 2026. That wave took it away for a menu in the left column; the
 * owner asked for it back on 12 October ("верни как было"), on a phone too,
 * where it used to scroll sideways and now wraps like text: no row of the
 * site scrolls sideways.
 *
 * The wrap falls after a bar and never before one: a board and the bar after
 * it are one unbreakable item, so no line of the strip opens with "|".
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { defineComponent } from "vue";
import { createMemoryHistory, createRouter } from "vue-router";
import { useBoardsStore, type Board } from "@/entities/forum";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import BoardNavigation from "./BoardNavigation.vue";

const HERE = dirname(fileURLToPath(import.meta.url));

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

    // A board and the bar after it are one item, the space between items is
    // outside them: the one place the line can break.
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

    const rules = ruleMap(componentCss(resolve(HERE, "BoardNavigation.vue")));
    expect(rules.get(".board-item")?.["white-space"]).toBe("nowrap");
  });

  it("wraps like text at every width and never scrolls sideways", () => {
    const rules = ruleMap(componentCss(resolve(HERE, "BoardNavigation.vue")));
    const strip = rules.get(".board-navigation") ?? {};

    expect(strip.display).toBe("block");
    for (const property of ["overflow", "overflow-x", "white-space"])
      expect(strip[property], property).toBeUndefined();
    expect(rules.get(".board-link")?.["white-space"]).toBe("nowrap");
    expect(rules.get(".board-link")?.display).toBe("inline-block");
    // No @media takes a phone's strip anywhere else.
    expect(componentCss(resolve(HERE, "BoardNavigation.vue"))).not.toMatch(
      /@media|@container/,
    );
  });

  it("is not drawn before the boards are there", async () => {
    const wrapper = await stripAt("/forum/general", null);

    expect(wrapper.find("nav").exists()).toBe(false);
  });
});

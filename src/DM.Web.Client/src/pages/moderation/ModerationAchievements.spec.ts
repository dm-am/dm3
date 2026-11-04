/**
 * @vitest-environment jsdom
 */

// A category: its action is the caption line under the description; its tiers
// are a table with the pencil and the bin in a row and "+ Тир" under it.
import { describe, it, expect, vi, afterEach } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import ModerationAchievements from "./ModerationAchievements.vue";
import { mountPage, windows } from "@/shared/lib/testing/mountPage";

const api = vi.hoisted(() => {
  const category = {
    id: "c-1",
    title: "Игровые посты",
    description: "Игровые посты во всех играх, включая удаленные",
    code: "game_posts_authored",
    metric: "GamePostsAuthored",
    iconName: "quill",
    sortOrder: 2,
    isActive: true,
  };
  const listed = <T>(resources: T[]) =>
    Promise.resolve({ data: { resources }, error: null });
  return {
    getModerationAchievementCategories: () => listed([category]),
    getModerationAchievementTypes: () =>
      listed([
        {
          id: "t-2",
          code: "game_posts_100",
          title: "Летописец",
          threshold: 100,
          tier: 2,
          category,
        },
        {
          id: "t-1",
          code: "game_posts_10",
          title: "Рассказчик",
          threshold: 10,
          tier: 3,
          category,
        },
      ]),
  };
});

// The network only: the catalog, the page and its windows are the real ones.
vi.mock("@/entities/achievement/api/achievementApi", () => ({ default: api }));

enableAutoUnmount(afterEach);

async function render() {
  const { wrapper } = await mountPage(
    ModerationAchievements,
    "/moderation/achievements",
    {
      routes: [["moderation-achievements", "/moderation/achievements"]],
    },
  );
  await flushPromises();
  return wrapper;
}

/** The title of the window open over the page, if one is. */
const openWindow = () => windows().find(".dialog-frame .dialog h2");

describe("a category of the achievement catalogue", () => {
  // One Russian word for the order, the one the award types use.
  it("names its order in Russian", async () => {
    const wrapper = await render();

    const meta = wrapper.get(".cat-row .cat-meta");
    expect(meta.get(".cat-order").text()).toBe("порядок 2");
    expect(meta.text()).not.toContain("sort");
  });

  it("writes its action as the caption line under the description", async () => {
    const wrapper = await render();

    const info = wrapper.get(".cat-row .cat-info");
    expect(info.get(".caption-line").text()).toBe("Редактировать");
  });

  it("opens the category from its caption, and a new tier from under its table", async () => {
    const wrapper = await render();
    await wrapper.get(".caption-line .caption-action").trigger("click");
    await flushPromises();
    expect(openWindow().text()).toBe("Категория: game_posts_authored");
    await windows().get(".dialog-frame .dialog-close").trigger("click");
    await flushPromises();
    expect(openWindow().exists()).toBe(false);

    // Right under the table of the tiers it adds a row to.
    const add = wrapper.get(".add-tier");
    expect(add.text()).toBe("+ Тир");
    expect(add.element.previousElementSibling).toBe(
      wrapper.get(".tier-table").element,
    );
    await add.trigger("click");
    await flushPromises();

    expect(openWindow().text()).toBe("Новый тир");
    // The metal after the highest one the chain has: bronze and silver, then gold.
    expect(windows().get<HTMLInputElement>("#tier-number").element.value).toBe(
      "1",
    );
  });
});

// The tiers are the site's table, every column at every width.
describe("the tiers of a category", () => {
  it("are the site's table, every column standing", async () => {
    const wrapper = await render();

    const headers = wrapper.findAll(".tier-table th");
    expect(headers.map((header) => header.text())).toEqual([
      "Тир",
      "Код",
      "Название",
      "Порог",
      "",
    ]);
  });

  it("lists the tiers by their threshold, each with the pencil and the bin", async () => {
    const wrapper = await render();

    const rows = wrapper.findAll("tr.table-row");
    expect(rows.map((row) => row.get("td.col-title").text())).toEqual([
      "Рассказчик",
      "Летописец",
    ]);
    expect(rows[0].get("td.col-code code").text()).toBe("game_posts_10");
    // The tier as its metal, not its number on the scale.
    expect(rows.map((row) => row.get("td.col-tier").text())).toEqual([
      "Бронза",
      "Серебро",
    ]);
    const signs = rows[0].findAll("td.col-actions button");
    expect(signs.map((sign) => sign.attributes("aria-label"))).toEqual([
      "Редактировать тир Рассказчик",
      "Удалить тир Рассказчик",
    ]);
    // Signs, not words.
    expect(rows[0].get("td.col-actions").text()).toBe("");

    await signs[0].trigger("click");
    await flushPromises();
    expect(openWindow().text()).toBe("Тир: game_posts_10");
  });
});

/**
 * @vitest-environment jsdom
 */

/**
 * A category of the achievement catalogue: its icon, its words, and its own
 * action as the caption line under the description, "Редактировать". Its
 * tiers are a table whose rows are added and removed in place, drawn as the
 * editor of a character sheet draws its own (AttributeSchemaEditor): the
 * pencil and the bin at the end of a row, "+ Тир" right under the table.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import type { Column } from "@/shared/ui/DataTable";
import ModerationAchievements from "./ModerationAchievements.vue";

const { openModal } = vi.hoisted(() => ({ openModal: vi.fn() }));

vi.mock("@/entities/achievement", async () => {
  const { ref } = await import("vue");
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
  return {
    achievementApi: { deleteAchievementType: vi.fn() },
    useAchievementCatalog: () => ({
      adminCategories: ref([category]),
      adminTypes: ref([
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
          tier: 1,
          category,
        },
      ]),
      loadAdmin: vi.fn().mockResolvedValue(undefined),
      reload: vi.fn().mockResolvedValue(undefined),
    }),
    formatThreshold: (_metric: string, threshold: number) => String(threshold),
  };
});

vi.mock("vue-final-modal", () => ({
  useModal: () => ({ open: openModal, close: vi.fn() }),
}));

/** The gate has its own spec; here the viewer may open the page. */
vi.mock("./lib/useRoleGate", async () => {
  const { ref } = await import("vue");
  return { useRoleGate: () => ({ hasAccess: ref(true), deniedText: "" }) };
});

enableAutoUnmount(afterEach);
afterEach(() => openModal.mockClear());

function render() {
  return mount(ModerationAchievements, {
    global: {
      stubs: { PageTitle: true, "page-title": true, GameIcon: true },
    },
  });
}

describe("a category of the achievement catalogue", () => {
  it("writes its action as the caption line under the description", () => {
    const wrapper = render();

    const info = wrapper.get(".cat-row .cat-info");
    const parts = Array.from(info.element.children).map(
      (child) => child.className.split(" ")[0],
    );
    expect(parts).toEqual([
      "cat-title",
      "cat-meta",
      "cat-desc",
      "caption-line",
    ]);
    expect(info.get(".caption-line").text()).toBe("Редактировать");
    expect(wrapper.find(".scroll-row").exists()).toBe(false);
  });

  it("opens the category from its caption, and a new tier from under its table", async () => {
    const wrapper = render();
    await wrapper.get(".caption-line .caption-action").trigger("click");
    expect(openModal).toHaveBeenCalledTimes(1);

    // Right under the table of the tiers it adds a row to.
    const add = wrapper.get(".add-tier");
    expect(add.text()).toBe("+ Тир");
    expect(add.element.previousElementSibling).toBe(
      wrapper.get(".tier-table").element,
    );
    await add.trigger("click");
    await flushPromises();

    expect(openModal).toHaveBeenCalledTimes(2);
  });
});

/**
 * The tiers of a category are the site's table (DataTable), as every list of
 * moderation is, with every column at every width: no column has a place in
 * an order of need.
 */
describe("the tiers of a category", () => {
  it("are the site's table, every column standing", () => {
    const wrapper = render();

    const columns = wrapper
      .getComponent({ name: "DataTable" })
      .props("columns") as Column[];
    expect(columns.map((column) => column.label)).toEqual([
      "Тир",
      "Код",
      "Название",
      "Порог",
      "",
    ]);
    for (const column of columns) {
      expect(column).not.toHaveProperty("rank");
    }
  });

  it("lists the tiers by their threshold, each with the pencil and the bin", async () => {
    const wrapper = render();

    const rows = wrapper.findAll("tr.table-row");
    expect(rows.map((row) => row.get("td.col-title").text())).toEqual([
      "Рассказчик",
      "Летописец",
    ]);
    expect(rows[0].get("td.col-code code").text()).toBe("game_posts_10");
    const signs = rows[0].findAll("td.col-actions button");
    expect(signs.map((sign) => sign.attributes("aria-label"))).toEqual([
      "Редактировать тир Рассказчик",
      "Удалить тир Рассказчик",
    ]);
    // Signs, not words.
    expect(rows[0].get("td.col-actions").text()).toBe("");

    await signs[0].trigger("click");
    expect(openModal).toHaveBeenCalledTimes(1);
  });
});

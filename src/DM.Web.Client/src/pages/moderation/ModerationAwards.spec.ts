/**
 * @vitest-environment jsdom
 */

// The series table, with "+ Новая серия" under it.
import { describe, it, expect, vi, afterEach } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  RouterLinkStub,
} from "@vue/test-utils";
import type { Ref } from "vue";
import type { Column } from "@/shared/ui/DataTable";
import { achievementApi } from "@/entities/achievement";
import ModerationAwards from "./ModerationAwards.vue";

/** The series the page lists, set by a test before it mounts the page. */
const listed = vi.hoisted(() => ({ series: null as Ref<unknown[]> | null }));

vi.mock("@/entities/achievement", async () => {
  const { ref } = await import("vue");
  listed.series = ref([]);
  return {
    achievementApi: { updateContestSeries: vi.fn() },
    useContestSeries: () => ({
      adminSeries: listed.series,
      loading: ref(false),
      loadAdmin: vi.fn().mockResolvedValue(undefined),
      reload: vi.fn().mockResolvedValue(undefined),
    }),
    formatContestSeriesTitle: (_type: number, number: number) =>
      `Конкурс ${number}`,
  };
});

vi.mock("vue-final-modal", () => ({
  useModal: () => ({ open: vi.fn(), close: vi.fn() }),
}));

enableAutoUnmount(afterEach);
afterEach(() => {
  listed.series!.value = [];
});

function render() {
  return mount(ModerationAwards, {
    global: {
      stubs: {
        RouterLink: RouterLinkStub,
        PageTitle: true,
        "page-title": true,
      },
    },
  });
}

describe("the head of the contest series", () => {
  it("puts the way to the catalogue under the heading", () => {
    const wrapper = render();

    const header = wrapper.get(".awards-admin__header");
    const [heading, link] = Array.from(header.element.children);
    expect(heading.tagName).toBe("H2");
    expect(heading.textContent?.trim()).toBe("Серии конкурсов");
    expect(link.textContent?.trim()).toBe("Каталог типов наград");
    expect(header.text()).not.toContain("+ Новая серия");
  });
});

// The site's table, every column at every width.
describe("the list of the contest series", () => {
  const SERIES = [
    {
      id: "s-1",
      contestType: 1,
      number: 23,
      year: 2024,
      topicUrl: "/forum/topic/7",
      isActive: true,
    },
    {
      id: "s-2",
      contestType: 1,
      number: 22,
      year: 2023,
      topicUrl: null,
      isActive: false,
    },
  ];

  function columnsOf(wrapper: ReturnType<typeof render>): Column[] {
    return wrapper
      .getComponent({ name: "DataTable" })
      .props("columns") as Column[];
  }

  it("is the site's table, every column standing, and the new series right under it", () => {
    listed.series!.value = SERIES;
    const wrapper = render();

    const columns = columnsOf(wrapper);
    expect(columns.map((column) => column.label)).toEqual([
      "Конкурс",
      "Год",
      "Топик",
      "Статус",
      "",
    ]);

    const add = wrapper.get(".new-series");
    expect(add.text()).toBe("+ Новая серия");
    expect(add.element.previousElementSibling).toBe(
      wrapper.get(".series-table").element,
    );
  });

  it("offers a new series under the line of an empty list too", () => {
    const wrapper = render();

    expect(wrapper.text()).toContain("Нет серий конкурсов");
    expect(wrapper.get(".new-series").text()).toBe("+ Новая серия");
  });

  it("draws a row of every series, a hidden one dimmed with its way back", () => {
    listed.series!.value = SERIES;
    const wrapper = render();

    const rows = wrapper.findAll("tr.table-row");
    expect(rows.map((row) => row.classes("is-inactive"))).toEqual([
      false,
      true,
    ]);
    expect(rows[0].get("td.col-title a").text()).toBe("Конкурс 23");
    expect(rows[0].get("td.col-actions").text()).toBe("Скрыть");
    expect(rows[1].get("td.col-status").text()).toBe("Скрыта");
    expect(rows[1].get("td.col-actions").text()).toBe("Вернуть");
  });

  it("prints a refused hide on a line under its row and keeps the row", async () => {
    listed.series!.value = SERIES;
    vi.mocked(achievementApi.updateContestSeries).mockResolvedValue({
      data: null,
      error: { type: "Conflict", title: "", status: 409, traceId: "t" },
    } as never);
    const wrapper = render();

    await wrapper.get("tr.table-row td.col-actions button").trigger("click");
    await flushPromises();

    const line = wrapper.get("tr.table-details-row .series-refusal");
    expect(line.text()).toBe("Не удалось скрыть серию");
    expect(
      line.element.closest("tr")?.previousElementSibling?.textContent,
    ).toContain("Конкурс 23");
    expect(wrapper.findAll("tr.table-row")).toHaveLength(2);
  });

  it("draws skeleton rows, not the empty line, until the series are read", () => {
    listed.series!.value = null as unknown as unknown[];
    const wrapper = render();

    expect(wrapper.text()).not.toContain("Нет серий конкурсов");
    expect(wrapper.find(".skeleton-row").exists()).toBe(true);
  });
});

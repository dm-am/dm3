/**
 * @vitest-environment jsdom
 */

/**
 * The keyboard of the month and year grids.
 *
 * Each grid is one stop of Tab, the way the day grid of the calendar is: the
 * twelve cells used to be twelve stops, and the arrows did nothing. The grid
 * is shared by the calendar and the statistics period picker, so both take
 * the same keys.
 */
import { describe, it, expect, afterEach } from "vitest";
import { mount } from "@vue/test-utils";
import MonthYearGrid from "./MonthYearGrid.vue";

type Wrapper = ReturnType<typeof mount<typeof MonthYearGrid>>;

afterEach(() => {
  document.body.innerHTML = "";
});

const mountGrid = (props: Record<string, unknown> = {}) =>
  mount(MonthYearGrid, {
    props: { mode: "month", year: 2026, month: 7, ...props },
    attachTo: document.body,
  });

const tabStop = (wrapper: Wrapper) =>
  wrapper.findAll('.myp-cell[tabindex="0"]').map((cell) => cell.text());

const press = async (wrapper: Wrapper, key: string) => {
  await wrapper.find(".myp-grid").trigger("keydown", { key });
  await wrapper.vm.$nextTick();
};

const focused = () =>
  (document.activeElement as HTMLElement | null)?.textContent?.trim();

describe("MonthYearGrid keyboard", () => {
  it("is one stop of Tab, on the selected month", () => {
    const wrapper = mountGrid();
    expect(tabStop(wrapper)).toEqual(["Июл"]);
    expect(wrapper.findAll('.myp-cell[tabindex="-1"]')).toHaveLength(11);
  });

  it("walks months with the side arrows and rows of three with the others", async () => {
    const wrapper = mountGrid();
    await press(wrapper, "ArrowRight");
    expect(focused()).toBe("Авг");
    await press(wrapper, "ArrowDown");
    expect(focused()).toBe("Ноя");
    await press(wrapper, "ArrowLeft");
    expect(focused()).toBe("Окт");
    await press(wrapper, "ArrowUp");
    expect(focused()).toBe("Июл");
    await press(wrapper, "Home");
    expect(focused()).toBe("Янв");
    await press(wrapper, "End");
    expect(focused()).toBe("Дек");
  });

  it("does not step onto a month out of reach", async () => {
    const wrapper = mountGrid({ maxYear: 2026, maxMonth: 7 });
    await press(wrapper, "ArrowRight");
    expect(tabStop(wrapper)).toEqual(["Июл"]);
  });

  it("turns the year with PageUp and PageDown", async () => {
    const wrapper = mountGrid();
    await press(wrapper, "PageUp");
    expect(wrapper.find(".myp-year").text()).toBe("2025");
  });

  it("walks the years the same way", async () => {
    const wrapper = mountGrid({ mode: "year", year: 2020, maxYear: 2026 });
    expect(tabStop(wrapper)).toEqual(["2020"]);
    await press(wrapper, "ArrowDown");
    expect(focused()).toBe("2023");
    await press(wrapper, "PageUp");
    expect(wrapper.find(".myp-year").text()).toBe("2003–2014");
  });
});

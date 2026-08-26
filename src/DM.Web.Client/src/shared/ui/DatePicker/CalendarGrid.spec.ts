/**
 * @vitest-environment jsdom
 */

/**
 * The calendar's way up.
 *
 * The panel used to be one grid with a caption over it: "Июль 2026" was a
 * span, and the only route to another year was the ‹ arrow at one month per
 * click. That is not a slow route, it is no route — a birthday in 1970 sits
 * 670 clicks behind the panel, and the day grid never says which of them you
 * are on until you stop.
 *
 * So the caption is a button into a grid of months, the year in that grid's
 * header is a button into a grid of years, and a pick drops back one level.
 * The two grids are MonthYearGrid, the panel the statistics picker already
 * had; what is asserted here is the wiring, the bounds it is handed and the
 * one thing that quietly does not work by itself — the level swap, where Vue
 * patches one component instance across two branches of the same v-if and
 * carries the navigated year over with it unless the branches are keyed.
 */
import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import CalendarGrid from "./CalendarGrid.vue";

type Wrapper = ReturnType<typeof mount<typeof CalendarGrid>>;

const mountGrid = (props: Record<string, unknown> = {}) =>
  mount(CalendarGrid, { props: { modelValue: "2026-07-15", ...props } });

/** Opens the month grid the way a reader does. */
const openMonths = async (wrapper: Wrapper) => {
  await wrapper.find(".dp-month").trigger("click");
};

/** Opens the year grid from the month grid's header. */
const openYears = async (wrapper: Wrapper) => {
  await wrapper.find(".myp-year-button").trigger("click");
};

const cellNamed = (wrapper: Wrapper, text: string) =>
  wrapper.findAll(".myp-cell").find((cell) => cell.text() === text)!;

describe("CalendarGrid levels", () => {
  it("starts on the day grid, captioned with the month it shows", () => {
    const wrapper = mountGrid();
    expect(wrapper.find(".dp-month").text()).toBe("Июль 2026");
    expect(wrapper.findAll(".dp-day")).toHaveLength(42);
  });

  it("opens the month grid from the caption", async () => {
    const wrapper = mountGrid();
    await openMonths(wrapper);
    expect(wrapper.find(".dp-grid").exists()).toBe(false);
    expect(wrapper.findAll(".myp-grid--months .myp-cell")).toHaveLength(12);
    expect(wrapper.find(".myp-year").text()).toBe("2026");
  });

  it("returns to the days of the month that was picked", async () => {
    const wrapper = mountGrid();
    await openMonths(wrapper);
    await cellNamed(wrapper, "Мар").trigger("click");
    expect(wrapper.find(".dp-month").text()).toBe("Март 2026");
    expect(wrapper.findAll(".dp-day")).toHaveLength(42);
  });

  it("opens the year grid from the year of the month grid's header", async () => {
    const wrapper = mountGrid();
    await openMonths(wrapper);
    await openYears(wrapper);
    const years = wrapper
      .findAll(".myp-grid--years .myp-cell")
      .map((cell) => Number(cell.text()));
    expect(years).toHaveLength(12);
    expect(years).toContain(2026);
    expect(wrapper.find(".myp-year").text()).toBe("2015–2026");
  });

  it("drops back to the months of the year that was picked", async () => {
    const wrapper = mountGrid();
    await openMonths(wrapper);
    await openYears(wrapper);
    await cellNamed(wrapper, "2019").trigger("click");
    expect(wrapper.findAll(".myp-grid--months .myp-cell")).toHaveLength(12);
    expect(wrapper.find(".myp-year").text()).toBe("2019");
  });

  it("carries the year picked above all the way down to the day grid", async () => {
    const wrapper = mountGrid();
    await openMonths(wrapper);
    await openYears(wrapper);
    await cellNamed(wrapper, "2019").trigger("click");
    await cellNamed(wrapper, "Ноя").trigger("click");
    expect(wrapper.find(".dp-month").text()).toBe("Ноябрь 2019");
  });

  it("does not carry a navigated year across a level swap", async () => {
    // The month grid navigates within itself (‹ 2025 ›) without picking. Both
    // levels are the same component, so an unkeyed v-else patches the instance
    // instead of remounting it and the year navigated to survives the trip.
    const wrapper = mountGrid();
    await openMonths(wrapper);
    await wrapper
      .find('.myp-nav[aria-label="Предыдущий год"]')
      .trigger("click");
    expect(wrapper.find(".myp-year").text()).toBe("2025");

    await openYears(wrapper);
    await cellNamed(wrapper, "2022").trigger("click");
    expect(wrapper.find(".myp-year").text()).toBe("2022");
  });

  it("pages years by blocks of twelve when nothing bounds the calendar", async () => {
    const wrapper = mountGrid();
    await openMonths(wrapper);
    await openYears(wrapper);
    const back = wrapper.find('.myp-nav[aria-label="Предыдущие годы"]');
    expect(back.attributes("disabled")).toBeUndefined();
    await back.trigger("click");
    expect(wrapper.find(".myp-year").text()).toBe("2003–2014");
    await back.trigger("click");
    expect(wrapper.find(".myp-year").text()).toBe("1991–2002");
  });
});

describe("CalendarGrid bounds", () => {
  it("disables the months past max inside the max year", async () => {
    const wrapper = mountGrid({ max: "2026-07-31" });
    await openMonths(wrapper);
    const cells = wrapper.findAll(".myp-grid--months .myp-cell");
    expect(cells[6].attributes("disabled")).toBeUndefined(); // July
    expect(cells[7].attributes("disabled")).toBeDefined(); // August
  });

  it("disables the months before min inside the min year", async () => {
    const wrapper = mountGrid({ min: "2026-05-01" });
    await openMonths(wrapper);
    const cells = wrapper.findAll(".myp-grid--months .myp-cell");
    expect(cells[3].attributes("disabled")).toBeDefined(); // April
    expect(cells[4].attributes("disabled")).toBeUndefined(); // May
  });

  it("stops the year blocks at the earliest selectable year", async () => {
    const wrapper = mountGrid({ min: "2024-01-01", max: "2026-12-31" });
    await openMonths(wrapper);
    await openYears(wrapper);
    const years = wrapper
      .findAll(".myp-grid--years .myp-cell")
      .map((cell) => Number(cell.text()));
    expect(years).toEqual([2024, 2025, 2026]);
    expect(
      wrapper
        .find('.myp-nav[aria-label="Предыдущие годы"]')
        .attributes("disabled"),
    ).toBeDefined();
  });
});

/**
 * @vitest-environment jsdom
 */

/**
 * The calendar: its weeks, its way up, its keyboard and its range.
 *
 * The weeks. The grid used to be six weeks whatever the month, 42 cells, so a
 * month of five weeks carried a whole row of the next one in grey, and with the
 * future locked the reader saw two grey rows under the month and could not tell
 * which were the next month and which were out of reach. Only the weeks of the
 * month are drawn now, and nothing of the months around it.
 *
 * The way up. "Сентябрь" is a button into a grid of months and "2026" beside it
 * a button straight into a grid of years; the year in the month grid's header
 * leads there too, and a pick drops back one level. The two grids are
 * MonthYearGrid, the panel the statistics picker already had; what is asserted
 * here is the wiring, the bounds it is handed and the one thing that quietly
 * does not work by itself — the level swap, where Vue patches one component
 * instance across two branches of the same v-if and carries the navigated year
 * over with it unless the branches are keyed.
 *
 * The keyboard. The day grid is one stop of Tab, and the keys move within it;
 * it used to be 42 stops and the arrows did nothing.
 *
 * The range. Both ends are filled and the band runs between them; while the
 * range is open the band follows the pointer to where the next click would
 * close it.
 *
 * Escape on the months or the years steps back down to the days and closes
 * nothing; on the days it is the owner's. A calendar with no date opens on
 * the day it is given (openOn): an empty birthday on the years around it.
 *
 * "Today" is pinned to 28 September 2026 by a fake clock, so the ring of today
 * and the days after it are the same on every run.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { mount } from "@vue/test-utils";
import CalendarGrid from "./CalendarGrid.vue";

type Wrapper = ReturnType<typeof mount<typeof CalendarGrid>>;

beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date(2026, 8, 28, 12, 0));
});

afterEach(() => {
  vi.useRealTimers();
  document.body.innerHTML = "";
});

const mountGrid = (props: Record<string, unknown> = {}) =>
  mount(CalendarGrid, {
    props: { modelValue: "2026-07-15", ...props },
    attachTo: document.body,
  });

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

/** The caption of the day grid, as the reader reads it. */
const caption = (wrapper: Wrapper) =>
  `${wrapper.find(".dp-month").text()} ${wrapper.find(".dp-year").text()}`;

const day = (wrapper: Wrapper, n: number) =>
  wrapper.findAll(".dp-day").find((cell) => cell.text() === String(n))!;

/** The day that holds the grid's one stop of Tab. */
const tabStop = (wrapper: Wrapper) =>
  wrapper.findAll('.dp-day[tabindex="0"]').map((cell) => cell.text());

const press = async (wrapper: Wrapper, key: string, shiftKey = false) => {
  await wrapper.find(".dp-grid").trigger("keydown", { key, shiftKey });
  // The focus moves after the grid re-renders on the new month.
  await wrapper.vm.$nextTick();
};

const focused = () =>
  (document.activeElement as HTMLElement | null)?.textContent?.trim();

describe("CalendarGrid weeks", () => {
  it("draws the days of the month and none of the months around it", () => {
    // September 2026 starts on a Tuesday: 30 days over five weeks, with the
    // Monday of the first week left empty rather than filled with 31 August.
    const wrapper = mountGrid({ modelValue: "2026-09-15" });
    const cells = wrapper.findAll(".dp-day");
    expect(cells.map((cell) => Number(cell.text()))).toEqual(
      Array.from({ length: 30 }, (_, i) => i + 1),
    );
    expect(cells[0].attributes("style")).toContain("grid-column-start: 2");
  });

  it("draws four weeks for a month of four, and six for a month of six", () => {
    // February 2021 starts on a Monday and ends on a Sunday: exactly four
    // weeks. May 2021 starts on a Saturday: the 31st falls in a sixth week.
    const february = mountGrid({ modelValue: "2021-02-10" });
    expect(february.findAll(".dp-day")).toHaveLength(28);
    expect(february.findAll(".dp-day")[0].attributes("style")).toContain(
      "grid-column-start: 1",
    );
    const may = mountGrid({ modelValue: "2021-05-10" });
    expect(may.findAll(".dp-day")).toHaveLength(31);
    expect(may.findAll(".dp-day")[0].attributes("style")).toContain(
      "grid-column-start: 6",
    );
  });

  it("fills the selected day and rings today, apart", () => {
    const wrapper = mountGrid({ modelValue: "2026-09-15" });
    expect(day(wrapper, 15).classes()).toContain("selected");
    expect(day(wrapper, 15).classes()).not.toContain("today");
    expect(day(wrapper, 28).classes()).toContain("today");
    expect(day(wrapper, 28).classes()).not.toContain("selected");
    expect(day(wrapper, 28).attributes("aria-current")).toBe("date");
  });

  it("keeps the days after max out of reach", () => {
    const wrapper = mountGrid({
      modelValue: "2026-09-15",
      max: "2026-09-28",
    });
    expect(day(wrapper, 28).attributes("disabled")).toBeUndefined();
    expect(day(wrapper, 29).attributes("disabled")).toBeDefined();
    expect(day(wrapper, 30).attributes("disabled")).toBeDefined();
  });
});

describe("CalendarGrid levels", () => {
  it("starts on the day grid, captioned with the month it shows", () => {
    const wrapper = mountGrid();
    expect(caption(wrapper)).toBe("Июль 2026");
    expect(wrapper.findAll(".dp-day")).toHaveLength(31);
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
    expect(caption(wrapper)).toBe("Март 2026");
    expect(wrapper.findAll(".dp-day")).toHaveLength(31);
  });

  it("opens the year grid straight from the year of the caption", async () => {
    const wrapper = mountGrid();
    await wrapper.find(".dp-year").trigger("click");
    const years = wrapper
      .findAll(".myp-grid--years .myp-cell")
      .map((cell) => Number(cell.text()));
    expect(years).toHaveLength(12);
    expect(years).toContain(2026);
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

  it("opens on the year grid when asked to", () => {
    const wrapper = mountGrid({ modelValue: null, initialLevel: "years" });
    expect(wrapper.find(".dp-grid").exists()).toBe(false);
    expect(wrapper.findAll(".myp-grid--years .myp-cell")).toHaveLength(12);
  });

  it("fills the month and the year of the date, and nothing without one", async () => {
    // The upper grids open on the page on view but fill the date: an empty
    // birthday opening on the years must not show a year as already chosen.
    const dated = mountGrid({
      modelValue: "1991-03-14",
      max: "2026-09-28",
    });
    await dated.find(".dp-year").trigger("click");
    expect(dated.find(".myp-year").text()).toBe("1991–2002");
    expect(dated.findAll(".myp-cell.selected").map((c) => c.text())).toEqual([
      "1991",
    ]);
    await cellNamed(dated, "1991").trigger("click");
    expect(dated.findAll(".myp-cell.selected").map((c) => c.text())).toEqual([
      "Мар",
    ]);

    const empty = mountGrid({ modelValue: null, initialLevel: "years" });
    expect(empty.findAll(".myp-cell.selected")).toHaveLength(0);
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
    expect(caption(wrapper)).toBe("Ноябрь 2019");
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

  it("shows a date set from outside among its days, from any grid", async () => {
    // An empty birthday opens on the years; the date then typed into the
    // field is what the reader wants to see, not the block of years.
    const wrapper = mountGrid({ modelValue: null, initialLevel: "years" });
    await wrapper.setProps({ modelValue: "1991-03-14" });
    expect(caption(wrapper)).toBe("Март 1991");
    expect(day(wrapper, 14).classes()).toContain("selected");
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

describe("CalendarGrid keyboard", () => {
  it("is one stop of Tab, on the selected day", () => {
    const wrapper = mountGrid({ modelValue: "2026-09-15" });
    expect(tabStop(wrapper)).toEqual(["15"]);
    expect(wrapper.findAll('.dp-day[tabindex="-1"]')).toHaveLength(29);
  });

  it("puts the stop on today when nothing is selected", () => {
    const wrapper = mountGrid({ modelValue: null });
    expect(tabStop(wrapper)).toEqual(["28"]);
  });

  it("takes the focus on open when asked to", async () => {
    const wrapper = mountGrid({ modelValue: "2026-09-15", focusOnOpen: true });
    await wrapper.vm.$nextTick();
    expect(focused()).toBe("15");
  });

  it("walks days with the side arrows and weeks with the others", async () => {
    const wrapper = mountGrid({ modelValue: "2026-09-15" });
    await press(wrapper, "ArrowRight");
    expect(tabStop(wrapper)).toEqual(["16"]);
    expect(focused()).toBe("16");
    await press(wrapper, "ArrowDown");
    expect(focused()).toBe("23");
    await press(wrapper, "ArrowLeft");
    expect(focused()).toBe("22");
    await press(wrapper, "ArrowUp");
    expect(focused()).toBe("15");
  });

  it("follows the arrows into the next month", async () => {
    const wrapper = mountGrid({ modelValue: "2026-07-30" });
    await press(wrapper, "ArrowDown");
    expect(caption(wrapper)).toBe("Август 2026");
    expect(focused()).toBe("6");
  });

  it("turns the month with PageUp and PageDown, the year with Shift", async () => {
    const wrapper = mountGrid({ modelValue: "2026-07-15" });
    await press(wrapper, "PageDown");
    expect(caption(wrapper)).toBe("Август 2026");
    expect(focused()).toBe("15");
    await press(wrapper, "PageUp", true);
    expect(caption(wrapper)).toBe("Август 2025");
    expect(focused()).toBe("15");
  });

  it("goes to the ends of the week with Home and End", async () => {
    // 15 September 2026 is a Tuesday: its week runs from the 14th to the 20th.
    const wrapper = mountGrid({ modelValue: "2026-09-15" });
    await press(wrapper, "Home");
    expect(focused()).toBe("14");
    await press(wrapper, "End");
    expect(focused()).toBe("20");
  });

  it("does not walk past max", async () => {
    const wrapper = mountGrid({
      modelValue: "2026-09-27",
      max: "2026-09-28",
    });
    await press(wrapper, "ArrowRight");
    await press(wrapper, "ArrowRight");
    expect(focused()).toBe("28");
    await press(wrapper, "PageDown");
    expect(caption(wrapper)).toBe("Сентябрь 2026");
    expect(focused()).toBe("28");
  });

  it("picks the day of the stop with Enter and with Space", async () => {
    const wrapper = mountGrid({ modelValue: "2026-09-15" });
    await press(wrapper, "ArrowRight");
    await press(wrapper, "Enter");
    expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([
      "2026-09-16",
    ]);
    await press(wrapper, "ArrowRight");
    await press(wrapper, " ");
    expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([
      "2026-09-17",
    ]);
  });
});

describe("CalendarGrid range", () => {
  const inRange = (wrapper: Wrapper) =>
    wrapper.findAll(".dp-day.in-range").map((cell) => Number(cell.text()));

  it("fills both ends and runs the band between them", () => {
    const wrapper = mountGrid({
      modelValue: null,
      range: true,
      rangeStart: "2026-09-01",
      rangeEnd: "2026-09-15",
    });
    expect(day(wrapper, 1).classes()).toEqual(
      expect.arrayContaining(["selected", "range-from"]),
    );
    expect(day(wrapper, 15).classes()).toEqual(
      expect.arrayContaining(["selected", "range-to"]),
    );
    expect(inRange(wrapper)).toEqual(
      Array.from({ length: 13 }, (_, i) => i + 2),
    );
  });

  it("previews the range under the pointer while it is open", async () => {
    const wrapper = mountGrid({
      modelValue: null,
      range: true,
      rangeStart: "2026-09-08",
      rangeOpen: true,
    });
    expect(inRange(wrapper)).toEqual([]);
    await day(wrapper, 20).trigger("mouseenter");
    expect(inRange(wrapper)).toEqual([
      9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19,
    ]);
    // The pointer's end is previewed, not picked.
    expect(day(wrapper, 20).classes()).toContain("range-to");
    expect(day(wrapper, 20).classes()).not.toContain("selected");
  });

  it("previews backwards when the pointer is before the start", async () => {
    const wrapper = mountGrid({
      modelValue: null,
      range: true,
      rangeStart: "2026-09-08",
      rangeOpen: true,
    });
    await day(wrapper, 3).trigger("mouseenter");
    expect(day(wrapper, 3).classes()).toContain("range-from");
    expect(inRange(wrapper)).toEqual([4, 5, 6, 7]);
    expect(day(wrapper, 8).classes()).toContain("range-to");
  });

  it("previews nothing once the range is closed", async () => {
    const wrapper = mountGrid({
      modelValue: null,
      range: true,
      rangeStart: "2026-09-08",
    });
    await day(wrapper, 20).trigger("mouseenter");
    expect(inRange(wrapper)).toEqual([]);
  });

  it("turns to a date typed into a field, but not to its own pick", async () => {
    const wrapper = mountGrid({
      modelValue: null,
      range: true,
      rangeStart: null,
    });
    await day(wrapper, 10).trigger("click");
    await wrapper
      .find('.dp-nav[aria-label="Предыдущий месяц"]')
      .trigger("click");
    expect(caption(wrapper)).toBe("Август 2026");
    // The pick comes back as the start: the grid stays where the reader is.
    await wrapper.setProps({ rangeStart: "2026-09-10" });
    expect(caption(wrapper)).toBe("Август 2026");
    // A date typed into the other field is shown.
    await wrapper.setProps({ rangeEnd: "2025-05-05" });
    expect(caption(wrapper)).toBe("Май 2025");
  });
});

describe("CalendarGrid Escape", () => {
  /**
   * Escape as a keyboard sends it: from the focused cell, bubbling up. The
   * clock is moved on first. Vue stamps an event with the time it first met
   * a listener and lets a listener higher up hear it only if that listener
   * was attached earlier; under a clock that stands still the two times are
   * equal, and the calendar's own listener above the grid would never hear
   * a key at all.
   */
  const escapeFrom = (target: Element) => {
    vi.setSystemTime(Date.now() + 1000);
    target.dispatchEvent(
      new KeyboardEvent("keydown", {
        key: "Escape",
        bubbles: true,
        cancelable: true,
      }),
    );
  };

  /** The Escapes that reach the page, where an owner closes the calendar. */
  const ownerCloses = () => {
    const seen: string[] = [];
    const listener = (e: KeyboardEvent) => seen.push(e.key);
    document.addEventListener("keydown", listener);
    return {
      seen,
      stop: () => document.removeEventListener("keydown", listener),
    };
  };

  it("goes back down from the months to the days, and closes nothing", async () => {
    const wrapper = mountGrid();
    await openMonths(wrapper);
    const owner = ownerCloses();
    escapeFrom(cellNamed(wrapper, "Июл").element);
    await wrapper.vm.$nextTick();
    owner.stop();
    expect(owner.seen).toEqual([]);
    expect(caption(wrapper)).toBe("Июль 2026");
    expect(wrapper.findAll(".dp-day")).toHaveLength(31);
  });

  it("goes back down from the years to the days of the month on screen", async () => {
    const wrapper = mountGrid();
    await openMonths(wrapper);
    // Paged within the months, nothing picked: the month on screen stays.
    await wrapper.find('[aria-label="Предыдущий год"]').trigger("click");
    await openYears(wrapper);
    const owner = ownerCloses();
    escapeFrom(cellNamed(wrapper, "2020").element);
    await wrapper.vm.$nextTick();
    owner.stop();
    expect(owner.seen).toEqual([]);
    expect(caption(wrapper)).toBe("Июль 2026");
  });

  it("takes the keyboard down with it", async () => {
    const wrapper = mountGrid({ modelValue: "2026-07-15" });
    await openMonths(wrapper);
    const cell = cellNamed(wrapper, "Июл").element as HTMLElement;
    cell.focus();
    escapeFrom(cell);
    await wrapper.vm.$nextTick();
    await wrapper.vm.$nextTick();
    expect(focused()).toBe("15");
  });

  it("leaves Escape on the days to the owner, who closes the calendar", () => {
    const wrapper = mountGrid();
    const owner = ownerCloses();
    escapeFrom(day(wrapper, 15).element);
    owner.stop();
    expect(owner.seen).toEqual(["Escape"]);
  });
});

describe("CalendarGrid opening day", () => {
  it("opens an empty calendar on the day it is given", () => {
    const wrapper = mountGrid({ modelValue: null, openOn: "2014-03-10" });
    expect(caption(wrapper)).toBe("Март 2014");
  });

  it("opens an empty birthday on the block of years around that day", () => {
    const wrapper = mountGrid({
      modelValue: null,
      openOn: "2014-09-28",
      max: "2026-09-28",
      initialLevel: "years",
    });
    expect(wrapper.find(".myp-header .myp-year").text()).toBe("2003–2014");
    expect(wrapper.findAll(".myp-cell.selected")).toHaveLength(0);
  });

  it("opens on the date itself when there is one", () => {
    const wrapper = mountGrid({
      modelValue: "1991-03-14",
      openOn: "2014-09-28",
    });
    expect(caption(wrapper)).toBe("Март 1991");
  });
});

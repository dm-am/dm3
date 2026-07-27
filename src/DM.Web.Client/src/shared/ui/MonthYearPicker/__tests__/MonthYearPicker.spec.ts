/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import MonthYearPicker from "../MonthYearPicker.vue";

describe("MonthYearPicker", () => {
  const mountComponent = (props: Record<string, unknown> = {}) => {
    return mount(MonthYearPicker, {
      attachTo: document.body,
      props: {
        year: 2026,
        month: 7,
        maxYear: 2026,
        maxMonth: 7,
        minYear: 2007,
        ...props,
      },
    });
  };

  const open = async (wrapper: ReturnType<typeof mountComponent>) => {
    await wrapper.find(".myp-trigger").trigger("click");
  };

  // ============================================================================
  // MONTH MODE
  // ============================================================================

  describe("Month mode", () => {
    it("labels the trigger with the capitalized month and year", () => {
      const wrapper = mountComponent();
      expect(wrapper.find(".myp-trigger").text()).toBe("Июль 2026");
    });

    it("disables months past maxMonth in the max year", async () => {
      const wrapper = mountComponent();
      await open(wrapper);
      const cells = wrapper.findAll(".myp-grid--months .myp-cell");
      // July (index 6) enabled, August (index 7) disabled in the current year.
      expect(cells[6].attributes("disabled")).toBeUndefined();
      expect(cells[7].attributes("disabled")).toBeDefined();
    });

    it("clamps year navigation at minYear", async () => {
      const wrapper = mountComponent({ minYear: 2025 });
      await open(wrapper);
      const prev = wrapper.find('.myp-nav[aria-label="Предыдущий год"]');
      await prev.trigger("click"); // 2026 -> 2025
      expect(wrapper.find(".myp-year").text()).toBe("2025");
      expect(prev.attributes("disabled")).toBeDefined();
    });

    it("emits year and month when picking in a navigated year", async () => {
      const wrapper = mountComponent();
      await open(wrapper);
      await wrapper
        .find('.myp-nav[aria-label="Предыдущий год"]')
        .trigger("click"); // 2025
      await wrapper.findAll(".myp-grid--months .myp-cell")[2].trigger("click");
      expect(wrapper.emitted("update:year")?.at(-1)).toEqual([2025]);
      expect(wrapper.emitted("update:month")?.at(-1)).toEqual([3]);
    });
  });

  // ============================================================================
  // YEAR MODE (12-year blocks)
  // ============================================================================

  describe("Year mode", () => {
    const yearMode = { mode: "year" as const };

    it("shows the latest 12-year block ascending", async () => {
      const wrapper = mountComponent(yearMode);
      await open(wrapper);
      const years = wrapper
        .findAll(".myp-grid--years .myp-cell")
        .map((c) => Number(c.text()));
      expect(years).toHaveLength(12);
      expect(years[0]).toBe(2015);
      expect(years[11]).toBe(2026);
      expect(wrapper.find(".myp-year").text()).toBe("2015–2026");
    });

    it("pages to a partial oldest block bounded by minYear", async () => {
      const wrapper = mountComponent(yearMode);
      await open(wrapper);
      const prev = wrapper.find('.myp-nav[aria-label="Предыдущие годы"]');
      await prev.trigger("click");
      const years = wrapper
        .findAll(".myp-grid--years .myp-cell")
        .map((c) => Number(c.text()));
      expect(years).toEqual([2007, 2008, 2009, 2010, 2011, 2012, 2013, 2014]);
      expect(prev.attributes("disabled")).toBeDefined();
    });

    it("opens on the block containing the selected year", async () => {
      const wrapper = mountComponent({ ...yearMode, year: 2010 });
      await open(wrapper);
      expect(wrapper.find(".myp-year").text()).toBe("2007–2014");
      expect(wrapper.find(".myp-cell.selected").text()).toBe("2010");
    });

    it("degenerates to a single cell when minYear equals maxYear", async () => {
      const wrapper = mountComponent({
        ...yearMode,
        minYear: 2026,
        maxYear: 2026,
      });
      await open(wrapper);
      const cells = wrapper.findAll(".myp-grid--years .myp-cell");
      expect(cells).toHaveLength(1);
      expect(cells[0].text()).toBe("2026");
      expect(
        wrapper
          .find('.myp-nav[aria-label="Предыдущие годы"]')
          .attributes("disabled"),
      ).toBeDefined();
      expect(
        wrapper
          .find('.myp-nav[aria-label="Следующие годы"]')
          .attributes("disabled"),
      ).toBeDefined();
    });

    it("emits the picked year", async () => {
      const wrapper = mountComponent(yearMode);
      await open(wrapper);
      const cell2020 = wrapper
        .findAll(".myp-grid--years .myp-cell")
        .find((c) => c.text() === "2020")!;
      await cell2020.trigger("click");
      expect(wrapper.emitted("update:year")?.at(-1)).toEqual([2020]);
    });
  });

  // ============================================================================
  // DIALOG BEHAVIOR
  // ============================================================================

  describe("Dialog behavior", () => {
    it("marks the selected cell with aria-current", async () => {
      const wrapper = mountComponent();
      await open(wrapper);
      const selected = wrapper.find(".myp-cell.selected");
      expect(selected.attributes("aria-current")).toBe("date");
    });

    it("closes on pick", async () => {
      const wrapper = mountComponent();
      await open(wrapper);
      await wrapper.findAll(".myp-grid--months .myp-cell")[0].trigger("click");
      expect(wrapper.find(".myp-popover").exists()).toBe(false);
    });
  });
});

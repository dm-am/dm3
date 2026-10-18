/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { mount } from "@vue/test-utils";
import DateRangePicker from "./DateRangePicker.vue";
import FilterApplyButton from "../primitives/FilterApplyButton.vue";
import DateInput from "@/shared/ui/DatePicker/DateInput.vue";
import Tooltip from "@/shared/ui/Tooltip/Tooltip.vue";

// "Today" for the calendar and the quick periods: 28 September 2026.
beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date(2026, 8, 28, 12, 0));
});

afterEach(() => {
  vi.useRealTimers();
  document.body.innerHTML = "";
});

describe("DateRangePicker", () => {
  const mountComponent = (props = {}) => {
    return mount(DateRangePicker, {
      props: {
        fromValue: null,
        toValue: null,
        ...props,
      },
      global: {
        components: {
          FilterApplyButton,
        },
      },
      attachTo: document.body,
    });
  };

  const applyOf = (wrapper: ReturnType<typeof mountComponent>) =>
    wrapper.findAllComponents(FilterApplyButton)[0];

  // ============================================================================
  // BASIC RENDERING
  // ============================================================================

  describe("Basic Rendering", () => {
    it("renders date range container", () => {
      const wrapper = mountComponent();
      expect(wrapper.find(".dropdown-date-range").exists()).toBe(true);
    });

    it("renders from and to date inputs (site calendar, not native)", () => {
      const wrapper = mountComponent();
      const dateInputs = wrapper.findAllComponents(DateInput);
      expect(dateInputs.length).toBe(2);
      expect(wrapper.find('input[type="date"]').exists()).toBe(false);
    });

    it("renders default labels", () => {
      const wrapper = mountComponent();
      const labels = wrapper.findAll(".date-label");
      expect(labels[0].text()).toBe("От");
      expect(labels[1].text()).toBe("До");
    });

    it("uses custom labels", () => {
      const wrapper = mountComponent({
        fromLabel: "Начало",
        toLabel: "Конец",
      });
      const labels = wrapper.findAll(".date-label");
      expect(labels[0].text()).toBe("Начало");
      expect(labels[1].text()).toBe("Конец");
    });

    it("renders apply button", () => {
      const wrapper = mountComponent();
      expect(wrapper.findComponent(FilterApplyButton).exists()).toBe(true);
    });
  });

  // ============================================================================
  // VALUE BINDING
  // ============================================================================

  describe("Value Binding", () => {
    it("displays from value as DD.MM.YYYY", () => {
      const wrapper = mountComponent({ fromValue: "2024-01-01" });
      const inputs = wrapper.findAll(".di-input");
      expect((inputs[0].element as HTMLInputElement).value).toBe("01.01.2024");
    });

    it("displays to value as DD.MM.YYYY", () => {
      const wrapper = mountComponent({ toValue: "2024-12-31" });
      const inputs = wrapper.findAll(".di-input");
      expect((inputs[1].element as HTMLInputElement).value).toBe("31.12.2024");
    });

    it("updates input when prop changes", async () => {
      const wrapper = mountComponent({ fromValue: "2024-01-01" });
      await wrapper.setProps({ fromValue: "2024-06-15" });
      const inputs = wrapper.findAll(".di-input");
      expect((inputs[0].element as HTMLInputElement).value).toBe("15.06.2024");
    });
  });

  // ============================================================================
  // APPLY BUTTON
  // ============================================================================

  describe("Apply Button", () => {
    it("emits apply event with values typed as DD.MM.YYYY", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".di-input");

      await inputs[0].setValue("01.01.2024");
      await inputs[1].setValue("31.12.2024");

      const applyBtn = wrapper.findComponent(FilterApplyButton);
      await applyBtn.find("button").trigger("click");

      expect(wrapper.emitted("apply")).toBeTruthy();
      expect(wrapper.emitted("apply")![0]).toEqual([
        "2024-01-01",
        "2024-12-31",
      ]);
    });

    it("accepts ISO YYYY-MM-DD typed manually", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".di-input");

      await inputs[0].setValue("2024-01-01");

      const applyBtn = wrapper.findComponent(FilterApplyButton);
      await applyBtn.find("button").trigger("click");

      expect(wrapper.emitted("apply")![0]).toEqual(["2024-01-01", null]);
    });

    it("emits null when clearing existing values", async () => {
      // Start with values so clearing is a change
      const wrapper = mountComponent({
        fromValue: "2024-01-01",
        toValue: "2024-12-31",
      });
      const inputs = wrapper.findAll(".di-input");

      // Clear the inputs
      await inputs[0].setValue("");
      await inputs[1].setValue("");

      const applyBtn = wrapper.findComponent(FilterApplyButton);
      await applyBtn.find("button").trigger("click");

      expect(wrapper.emitted("apply")).toBeTruthy();
      expect(wrapper.emitted("apply")![0]).toEqual([null, null]);
    });
  });

  // ============================================================================
  // APPLY BUTTON STATE
  // ============================================================================

  /**
   * The lock on the apply button has one source, `canApply`. It used to have
   * two: `!canApply && !hasValues` kept the button alive as soon as either
   * field held anything, so a range typed back to front ("От" later than "До")
   * applied inverted and the reason wired next to it never reached the screen.
   */
  describe("Apply Button State", () => {
    it("keeps apply locked, with no reason, while nothing has changed", () => {
      const wrapper = mountComponent({
        fromValue: "2024-01-01",
        toValue: "2024-12-31",
      });
      const apply = applyOf(wrapper);

      expect(apply.find("button").attributes("disabled")).toBeDefined();
      expect(apply.props("disabledReason")).toBeUndefined();
      expect(apply.findComponent(Tooltip).props("text")).toBeUndefined();
    });

    it("unlocks apply on a valid change", async () => {
      const wrapper = mountComponent({
        fromValue: "2024-01-01",
        toValue: "2024-12-31",
      });

      await wrapper.findAll(".di-input")[1].setValue("31.10.2024");

      expect(
        applyOf(wrapper).find("button").attributes("disabled"),
      ).toBeUndefined();
    });

    it("locks apply on an inverted range and shows the reason", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".di-input");

      await inputs[0].setValue("31.12.2024");
      await inputs[1].setValue("01.01.2024");

      const apply = applyOf(wrapper);
      expect(apply.find("button").attributes("disabled")).toBeDefined();
      expect(apply.props("disabledReason")).toBe(
        "Начало не может быть позже конца",
      );
      // The reason is only on screen if it reaches the tooltip around the
      // button: a locked button of its own shows nothing.
      expect(apply.findComponent(Tooltip).props("text")).toBe(
        "Начало не может быть позже конца",
      );
    });

    it("does not apply an inverted range", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".di-input");

      await inputs[0].setValue("31.12.2024");
      await inputs[1].setValue("01.01.2024");
      await applyOf(wrapper).find("button").trigger("click");

      expect(wrapper.emitted("apply")).toBeFalsy();
    });

    it("unlocks apply once the inverted range is fixed", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".di-input");

      await inputs[0].setValue("31.12.2024");
      await inputs[1].setValue("01.01.2024");
      await inputs[1].setValue("31.12.2025");

      const apply = applyOf(wrapper);
      expect(apply.find("button").attributes("disabled")).toBeUndefined();
      expect(apply.props("disabledReason")).toBeUndefined();
    });

    it("leaves the clear button alone while apply is locked", async () => {
      const wrapper = mountComponent({
        fromValue: "2024-01-01",
        toValue: "2024-12-31",
        showClearButton: true,
      });
      const inputs = wrapper.findAll(".di-input");

      // Invert the applied range: apply locks, clear keeps working
      await inputs[1].setValue("01.01.2023");

      const buttons = wrapper.findAllComponents(FilterApplyButton);
      expect(buttons[0].find("button").attributes("disabled")).toBeDefined();
      expect(buttons[1].find("button").attributes("disabled")).toBeUndefined();

      await buttons[1].find("button").trigger("click");
      expect(wrapper.emitted("clear")).toBeTruthy();
    });
  });

  // ============================================================================
  // CLEAR BUTTON
  // ============================================================================

  describe("Clear Button", () => {
    it("shows clear button when values exist", () => {
      const wrapper = mountComponent({
        fromValue: "2024-01-01",
        showClearButton: true,
      });
      const buttons = wrapper.findAllComponents(FilterApplyButton);
      expect(buttons.length).toBe(2); // Apply + Clear
    });

    it("hides clear button when no values", () => {
      const wrapper = mountComponent({
        fromValue: null,
        toValue: null,
        showClearButton: true,
      });
      const buttons = wrapper.findAllComponents(FilterApplyButton);
      expect(buttons.length).toBe(1); // Only Apply
    });

    it("emits clear event when clear button clicked", async () => {
      const wrapper = mountComponent({
        fromValue: "2024-01-01",
        showClearButton: true,
      });
      const clearBtn = wrapper.findAllComponents(FilterApplyButton)[1];
      await clearBtn.find("button").trigger("click");

      expect(wrapper.emitted("clear")).toBeTruthy();
    });
  });

  // ============================================================================
  // THE CALENDAR OF THE RANGE
  // ============================================================================

  /**
   * One calendar under both fields, set into the panel. Each field used to
   * open a calendar of its own over the panel, which covered the other field
   * and "Применить" and showed no range at all.
   */
  describe("The calendar of the range", () => {
    const day = (wrapper: ReturnType<typeof mountComponent>, n: number) =>
      wrapper.findAll(".dp-day").find((cell) => cell.text() === String(n))!;

    const fields = (wrapper: ReturnType<typeof mountComponent>) =>
      wrapper
        .findAll(".di-input")
        .map((input) => (input.element as HTMLInputElement).value);

    it("stands in the panel, one for both fields, with no popover", () => {
      const wrapper = mountComponent();
      expect(wrapper.findAll(".calendar-grid")).toHaveLength(1);
      expect(wrapper.find(".calendar-grid--embedded").exists()).toBe(true);
      expect(wrapper.find(".di-toggle").exists()).toBe(false);
    });

    it("sets the start with the first click and the end with the second", async () => {
      const wrapper = mountComponent();
      await day(wrapper, 8).trigger("click");
      expect(fields(wrapper)).toEqual(["08.09.2026", ""]);
      await day(wrapper, 20).trigger("click");
      expect(fields(wrapper)).toEqual(["08.09.2026", "20.09.2026"]);

      await applyOf(wrapper).find("button").trigger("click");
      expect(wrapper.emitted("apply")![0]).toEqual([
        "2026-09-08",
        "2026-09-20",
      ]);
    });

    it("swaps the ends when the second day is earlier than the first", async () => {
      const wrapper = mountComponent();
      await day(wrapper, 20).trigger("click");
      await day(wrapper, 8).trigger("click");
      expect(fields(wrapper)).toEqual(["08.09.2026", "20.09.2026"]);
    });

    it("starts a new range on a click after a finished one", async () => {
      const wrapper = mountComponent({
        fromValue: "2026-09-01",
        toValue: "2026-09-15",
      });
      await day(wrapper, 20).trigger("click");
      expect(fields(wrapper)).toEqual(["20.09.2026", ""]);
    });

    it("draws the band between the ends before the second click lands", async () => {
      const wrapper = mountComponent();
      await day(wrapper, 8).trigger("click");
      await day(wrapper, 12).trigger("mouseenter");
      expect(
        wrapper.findAll(".dp-day.in-range").map((cell) => cell.text()),
      ).toEqual(["9", "10", "11"]);
    });

    it("draws dates typed into the fields at once", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".di-input");
      await inputs[0].setValue("03.09.2026");
      await inputs[1].setValue("05.09.2026");
      expect(day(wrapper, 3).classes()).toContain("selected");
      expect(day(wrapper, 4).classes()).toContain("in-range");
      expect(day(wrapper, 5).classes()).toContain("selected");
    });

    it("hands the keyboard to the calendar on the down arrow in a field", async () => {
      const wrapper = mountComponent({ fromValue: "2026-09-03" });
      await wrapper.findAll(".di-input")[1].trigger("keydown", {
        key: "ArrowDown",
      });
      await wrapper.vm.$nextTick();
      await wrapper.vm.$nextTick();
      const active = document.activeElement as HTMLElement;
      expect(active.classList.contains("dp-day")).toBe(true);
      expect(active.textContent?.trim()).toBe("3");
    });
  });

  // ============================================================================
  // QUICK PERIODS
  // ============================================================================

  describe("Quick periods", () => {
    const preset = (wrapper: ReturnType<typeof mountComponent>, name: string) =>
      wrapper.findAll(".date-preset").find((one) => one.text() === name)!;

    const fields = (wrapper: ReturnType<typeof mountComponent>) =>
      wrapper
        .findAll(".di-input")
        .map((input) => (input.element as HTMLInputElement).value);

    it("offers a week, a month and a year", () => {
      const wrapper = mountComponent();
      expect(wrapper.findAll(".date-preset").map((one) => one.text())).toEqual([
        "Неделя",
        "Месяц",
        "Год",
      ]);
    });

    it("fills the fields with a period that ends today", async () => {
      const wrapper = mountComponent();
      await preset(wrapper, "Неделя").trigger("click");
      expect(fields(wrapper)).toEqual(["22.09.2026", "28.09.2026"]);
      await preset(wrapper, "Месяц").trigger("click");
      expect(fields(wrapper)).toEqual(["29.08.2026", "28.09.2026"]);
      await preset(wrapper, "Год").trigger("click");
      expect(fields(wrapper)).toEqual(["29.09.2025", "28.09.2026"]);
    });

    it("leaves applying to Применить", async () => {
      const wrapper = mountComponent();
      await preset(wrapper, "Неделя").trigger("click");
      expect(wrapper.emitted("apply")).toBeFalsy();
      await applyOf(wrapper).find("button").trigger("click");
      expect(wrapper.emitted("apply")![0]).toEqual([
        "2026-09-22",
        "2026-09-28",
      ]);
    });
  });

  // ============================================================================
  // THE FUTURE
  // ============================================================================

  describe("The future", () => {
    const day = (wrapper: ReturnType<typeof mountComponent>, n: number) =>
      wrapper.findAll(".dp-day").find((cell) => cell.text() === String(n))!;

    it("is out of reach by default, on the calendar and in the fields", async () => {
      const wrapper = mountComponent();
      expect(day(wrapper, 28).attributes("disabled")).toBeUndefined();
      expect(day(wrapper, 29).attributes("disabled")).toBeDefined();
      await wrapper.findAll(".di-input")[1].setValue("01.10.2026");
      expect(applyOf(wrapper).find("button").attributes("disabled")).toBe("");
    });

    it("is open to a filter that allows it (the dates of a poll)", async () => {
      const wrapper = mountComponent({ allowFuture: true });
      expect(day(wrapper, 29).attributes("disabled")).toBeUndefined();
      await wrapper.findAll(".di-input")[1].setValue("01.10.2026");
      await applyOf(wrapper).find("button").trigger("click");
      expect(wrapper.emitted("apply")![0]).toEqual([null, "2026-10-01"]);
    });
  });
});

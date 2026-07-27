/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import DateRangePicker from "../inputs/DateRangePicker.vue";
import FilterApplyButton from "../primitives/FilterApplyButton.vue";
import DateInput from "@/shared/ui/DatePicker/DateInput.vue";

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
    });
  };

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
  // CALENDAR POPOVER
  // ============================================================================

  describe("Calendar Popover", () => {
    it("opens the calendar from the toggle button and applies a picked day", async () => {
      const wrapper = mountComponent();

      await wrapper.findAll(".di-toggle")[0].trigger("click");
      const calendar = wrapper.find(".calendar-grid");
      expect(calendar.exists()).toBe(true);

      // Pick the first enabled in-month day
      const day = wrapper.find(".dp-day:not(.out-month):not(:disabled)");
      await day.trigger("click");

      // Calendar closed, value landed in the from input
      expect(wrapper.find(".calendar-grid").exists()).toBe(false);
      const fromInput = wrapper.findAll(".di-input")[0]
        .element as HTMLInputElement;
      expect(fromInput.value).toMatch(/^\d{2}\.\d{2}\.\d{4}$/);
    });

    it("closes the calendar on Escape without closing anything else", async () => {
      const wrapper = mountComponent();

      await wrapper.findAll(".di-toggle")[0].trigger("click");
      expect(wrapper.find(".calendar-grid").exists()).toBe(true);

      await wrapper.findAll(".di-input")[0].trigger("keydown", {
        key: "Escape",
      });
      expect(wrapper.find(".calendar-grid").exists()).toBe(false);
    });
  });
});

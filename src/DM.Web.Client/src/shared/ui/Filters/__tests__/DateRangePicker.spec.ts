/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import DateRangePicker from "../inputs/DateRangePicker.vue";
import FilterApplyButton from "../primitives/FilterApplyButton.vue";

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

    it("renders from and to inputs", () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll('input[type="date"]');
      expect(inputs.length).toBe(2);
    });

    it("renders default labels", () => {
      const wrapper = mountComponent();
      const labels = wrapper.findAll(".date-label");
      expect(labels[0].text()).toBe("От:");
      expect(labels[1].text()).toBe("До:");
    });

    it("uses custom labels", () => {
      const wrapper = mountComponent({
        fromLabel: "С:",
        toLabel: "По:",
      });
      const labels = wrapper.findAll(".date-label");
      expect(labels[0].text()).toBe("С:");
      expect(labels[1].text()).toBe("По:");
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
    it("sets from input value from prop", () => {
      const wrapper = mountComponent({ fromValue: "2024-01-01" });
      const inputs = wrapper.findAll('input[type="date"]');
      expect((inputs[0].element as HTMLInputElement).value).toBe("2024-01-01");
    });

    it("sets to input value from prop", () => {
      const wrapper = mountComponent({ toValue: "2024-12-31" });
      const inputs = wrapper.findAll('input[type="date"]');
      expect((inputs[1].element as HTMLInputElement).value).toBe("2024-12-31");
    });

    it("updates input when prop changes", async () => {
      const wrapper = mountComponent({ fromValue: "2024-01-01" });
      await wrapper.setProps({ fromValue: "2024-06-15" });
      const inputs = wrapper.findAll('input[type="date"]');
      expect((inputs[0].element as HTMLInputElement).value).toBe("2024-06-15");
    });
  });

  // ============================================================================
  // APPLY BUTTON
  // ============================================================================

  describe("Apply Button", () => {
    it("emits apply event with values", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll('input[type="date"]');

      await inputs[0].setValue("2024-01-01");
      await inputs[1].setValue("2024-12-31");

      const applyBtn = wrapper.findComponent(FilterApplyButton);
      await applyBtn.trigger("click");

      expect(wrapper.emitted("apply")).toBeTruthy();
      expect(wrapper.emitted("apply")![0]).toEqual(["2024-01-01", "2024-12-31"]);
    });

    it("emits null when clearing existing values", async () => {
      // Start with values so clearing is a change
      const wrapper = mountComponent({
        fromValue: "2024-01-01",
        toValue: "2024-12-31",
      });
      const inputs = wrapper.findAll(".date-input");

      // Clear the inputs
      await inputs[0].setValue("");
      await inputs[1].setValue("");

      const applyBtn = wrapper.findComponent(FilterApplyButton);
      await applyBtn.trigger("click");

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
      await clearBtn.trigger("click");

      expect(wrapper.emitted("clear")).toBeTruthy();
    });
  });
});

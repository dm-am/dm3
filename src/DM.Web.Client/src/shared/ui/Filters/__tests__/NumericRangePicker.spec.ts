/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { nextTick } from "vue";
import NumericRangePicker from "../inputs/NumericRangePicker.vue";

// Mock FilterApplyButton
const FilterApplyButtonStub = {
  template: '<button class="apply-btn" :class="variant" :disabled="disabled" @click="$emit(\'click\')">{{ label }}</button>',
  props: ["disabled", "label", "variant"],
  emits: ["click"],
};

describe("NumericRangePicker", () => {
  const mountComponent = (props = {}) => {
    return mount(NumericRangePicker, {
      props: {
        minValue: null,
        maxValue: null,
        ...props,
      },
      global: {
        stubs: {
          FilterApplyButton: FilterApplyButtonStub,
        },
      },
    });
  };

  // ============================================================================
  // BASIC RENDERING
  // ============================================================================

  describe("Basic Rendering", () => {
    it("renders two range inputs", () => {
      const wrapper = mountComponent();
      expect(wrapper.findAll(".range-input")).toHaveLength(2);
    });

    it("renders default labels", () => {
      const wrapper = mountComponent();
      const labels = wrapper.findAll(".range-label");
      expect(labels[0].text()).toBe("От:");
      expect(labels[1].text()).toBe("До:");
    });

    it("renders custom labels", () => {
      const wrapper = mountComponent({
        minLabel: "Минимум:",
        maxLabel: "Максимум:",
      });
      const labels = wrapper.findAll(".range-label");
      expect(labels[0].text()).toBe("Минимум:");
      expect(labels[1].text()).toBe("Максимум:");
    });

    it("renders placeholders when provided", () => {
      const wrapper = mountComponent({
        minPlaceholder: "0",
        maxPlaceholder: "100",
      });
      const inputs = wrapper.findAll(".range-input");
      expect(inputs[0].attributes("placeholder")).toBe("0");
      expect(inputs[1].attributes("placeholder")).toBe("100");
    });

    it("renders apply button", () => {
      const wrapper = mountComponent();
      expect(wrapper.find(".apply-btn").exists()).toBe(true);
    });

    it("shows clear button when values are set", () => {
      const wrapper = mountComponent({ minValue: 10 });
      const buttons = wrapper.findAll(".apply-btn");
      expect(buttons).toHaveLength(2);
      expect(buttons[1].text()).toBe("Сбросить");
    });

    it("hides clear button when no values", () => {
      const wrapper = mountComponent({ minValue: null, maxValue: null });
      const buttons = wrapper.findAll(".apply-btn");
      expect(buttons).toHaveLength(1);
    });
  });

  // ============================================================================
  // INPUT VALUES
  // ============================================================================

  describe("Input Values", () => {
    it("displays initial values", () => {
      const wrapper = mountComponent({ minValue: 10, maxValue: 50 });
      const inputs = wrapper.findAll(".range-input");
      expect((inputs[0].element as HTMLInputElement).value).toBe("10");
      expect((inputs[1].element as HTMLInputElement).value).toBe("50");
    });

    it("updates when props change", async () => {
      const wrapper = mountComponent({ minValue: 10 });
      const inputs = wrapper.findAll(".range-input");
      expect((inputs[0].element as HTMLInputElement).value).toBe("10");

      await wrapper.setProps({ minValue: 20 });
      expect((inputs[0].element as HTMLInputElement).value).toBe("20");
    });

    it("allows typing in inputs", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".range-input");

      await inputs[0].setValue("25");
      expect((inputs[0].element as HTMLInputElement).value).toBe("25");
    });
  });

  // ============================================================================
  // APPLY BUTTON STATE
  // ============================================================================

  describe("Apply Button State", () => {
    it("disables apply when no changes", () => {
      const wrapper = mountComponent({ minValue: 10, maxValue: 50 });
      const applyBtn = wrapper.find(".apply-btn");
      expect(applyBtn.attributes("disabled")).toBeDefined();
    });

    it("enables apply when values change", async () => {
      const wrapper = mountComponent({ minValue: 10, maxValue: 50 });
      const inputs = wrapper.findAll(".range-input");

      await inputs[0].setValue("20");
      await nextTick();

      const applyBtn = wrapper.find(".apply-btn");
      expect(applyBtn.attributes("disabled")).toBeUndefined();
    });

    it("enables apply when adding new value", async () => {
      const wrapper = mountComponent({ minValue: null, maxValue: null });
      const inputs = wrapper.findAll(".range-input");

      await inputs[0].setValue("10");
      await nextTick();

      const applyBtn = wrapper.find(".apply-btn");
      expect(applyBtn.attributes("disabled")).toBeUndefined();
    });
  });

  // ============================================================================
  // EVENTS
  // ============================================================================

  describe("Events", () => {
    it("emits apply with parsed values", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".range-input");

      await inputs[0].setValue("10");
      await inputs[1].setValue("50");
      await wrapper.find(".apply-btn").trigger("click");

      expect(wrapper.emitted("apply")).toBeTruthy();
      expect(wrapper.emitted("apply")![0]).toEqual([10, 50]);
    });

    it("emits apply with null for empty values", async () => {
      const wrapper = mountComponent({ minValue: 10, maxValue: 50 });
      const inputs = wrapper.findAll(".range-input");

      await inputs[0].setValue("");
      await inputs[1].setValue("");
      await wrapper.find(".apply-btn").trigger("click");

      expect(wrapper.emitted("apply")![0]).toEqual([null, null]);
    });

    it("emits clear when clear button clicked", async () => {
      const wrapper = mountComponent({ minValue: 10, maxValue: 50 });
      const buttons = wrapper.findAll(".apply-btn");

      await buttons[1].trigger("click"); // Clear button

      expect(wrapper.emitted("clear")).toBeTruthy();
    });

    it("clears inputs when clear is clicked", async () => {
      const wrapper = mountComponent({ minValue: 10, maxValue: 50 });
      const inputs = wrapper.findAll(".range-input");
      const buttons = wrapper.findAll(".apply-btn");

      await buttons[1].trigger("click");

      expect((inputs[0].element as HTMLInputElement).value).toBe("");
      expect((inputs[1].element as HTMLInputElement).value).toBe("");
    });

    it("emits apply on Enter key", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".range-input");

      await inputs[0].setValue("10");
      await inputs[0].trigger("keydown", { key: "Enter" });

      expect(wrapper.emitted("apply")).toBeTruthy();
    });
  });

  // ============================================================================
  // NEGATIVE NUMBERS
  // ============================================================================

  describe("Negative Numbers", () => {
    it("converts negative to 0 when allowNegative is false", async () => {
      const wrapper = mountComponent({ allowNegative: false });
      const inputs = wrapper.findAll(".range-input");

      await inputs[0].setValue("-10");
      await wrapper.find(".apply-btn").trigger("click");

      expect(wrapper.emitted("apply")![0]).toEqual([0, null]);
    });

    it("allows negative when allowNegative is true", async () => {
      const wrapper = mountComponent({ allowNegative: true });
      const inputs = wrapper.findAll(".range-input");

      await inputs[0].setValue("-10");
      await wrapper.find(".apply-btn").trigger("click");

      expect(wrapper.emitted("apply")![0]).toEqual([-10, null]);
    });

    it("sets min attribute based on allowNegative", () => {
      const wrapper = mountComponent({ allowNegative: false });
      const inputs = wrapper.findAll(".range-input");
      expect(inputs[0].attributes("min")).toBe("0");
    });

    it("omits min attribute when allowNegative is true", () => {
      const wrapper = mountComponent({ allowNegative: true });
      const inputs = wrapper.findAll(".range-input");
      expect(inputs[0].attributes("min")).toBeUndefined();
    });
  });

  // ============================================================================
  // STEP
  // ============================================================================

  describe("Step", () => {
    it("uses default step of 1", () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".range-input");
      expect(inputs[0].attributes("step")).toBe("1");
    });

    it("uses custom step", () => {
      const wrapper = mountComponent({ step: 0.5 });
      const inputs = wrapper.findAll(".range-input");
      expect(inputs[0].attributes("step")).toBe("0.5");
    });
  });

  // ============================================================================
  // EDGE CASES
  // ============================================================================

  describe("Edge Cases", () => {
    it("handles invalid input gracefully", async () => {
      // Start with a value so that changing to null is a change
      const wrapper = mountComponent({ minValue: 10 });
      const inputs = wrapper.findAll(".range-input");

      await inputs[0].setValue("abc");
      await wrapper.find(".apply-btn").trigger("click");

      expect(wrapper.emitted("apply")![0]).toEqual([null, null]);
    });

    it("handles decimal numbers", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".range-input");

      await inputs[0].setValue("10.5");
      await inputs[1].setValue("20.75");
      await wrapper.find(".apply-btn").trigger("click");

      expect(wrapper.emitted("apply")![0]).toEqual([10.5, 20.75]);
    });

    it("handles whitespace-only input", async () => {
      // Start with a value so that changing to null is a change
      const wrapper = mountComponent({ minValue: 5 });
      const inputs = wrapper.findAll(".range-input");

      await inputs[0].setValue("   ");
      await wrapper.find(".apply-btn").trigger("click");

      expect(wrapper.emitted("apply")![0]).toEqual([null, null]);
    });

    it("handles zero as valid value", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".range-input");

      await inputs[0].setValue("0");
      await wrapper.find(".apply-btn").trigger("click");

      expect(wrapper.emitted("apply")![0]).toEqual([0, null]);
    });
  });
});

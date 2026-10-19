/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import { nextTick } from "vue";
import NumericRangePicker from "./NumericRangePicker.vue";

describe("NumericRangePicker", () => {
  const mountComponent = (props = {}) =>
    mount(NumericRangePicker, {
      props: { minValue: null, maxValue: null, ...props },
    });

  describe("Basic Rendering", () => {
    it("renders two stepper inputs", () => {
      const wrapper = mountComponent();
      expect(wrapper.findAll(".stepper-input")).toHaveLength(2);
    });

    it("renders default labels", () => {
      const wrapper = mountComponent();
      const labels = wrapper.findAll(".range-label");
      expect(labels[0].text()).toBe("От");
      expect(labels[1].text()).toBe("До");
    });

    it("shows clear button when values are set", () => {
      const wrapper = mountComponent({ minValue: 10 });
      const buttons = wrapper.findAll(".filter-apply-btn");
      expect(buttons).toHaveLength(2);
      expect(buttons[1].text()).toBe("Сбросить");
    });

    it("hides clear button when no values", () => {
      const wrapper = mountComponent({ minValue: null, maxValue: null });
      const buttons = wrapper.findAll(".filter-apply-btn");
      expect(buttons).toHaveLength(1);
    });
  });

  describe("Input Values", () => {
    it("displays initial values", () => {
      const wrapper = mountComponent({ minValue: 10, maxValue: 50 });
      const inputs = wrapper.findAll(".stepper-input");
      expect((inputs[0].element as HTMLInputElement).value).toBe("10");
      expect((inputs[1].element as HTMLInputElement).value).toBe("50");
    });

    it("updates when props change", async () => {
      const wrapper = mountComponent({ minValue: 10 });
      const inputs = wrapper.findAll(".stepper-input");
      expect((inputs[0].element as HTMLInputElement).value).toBe("10");

      await wrapper.setProps({ minValue: 20 });
      expect((inputs[0].element as HTMLInputElement).value).toBe("20");
    });
  });

  describe("Apply Button State", () => {
    it("disables apply when no changes", () => {
      const wrapper = mountComponent({ minValue: 10, maxValue: 50 });
      const applyBtn = wrapper.find(".filter-apply-btn");
      expect(applyBtn.attributes("disabled")).toBeDefined();
    });

    it("enables apply when values change", async () => {
      const wrapper = mountComponent({ minValue: 10, maxValue: 50 });
      const inputs = wrapper.findAll(".stepper-input");

      await inputs[0].setValue("20");
      await nextTick();

      expect(
        wrapper.find(".filter-apply-btn").attributes("disabled"),
      ).toBeUndefined();
    });

    it("enables apply when adding new value", async () => {
      const wrapper = mountComponent({ minValue: null, maxValue: null });
      const inputs = wrapper.findAll(".stepper-input");

      await inputs[0].setValue("10");
      await nextTick();

      expect(
        wrapper.find(".filter-apply-btn").attributes("disabled"),
      ).toBeUndefined();
    });

    it("disables apply when min > max (invalid range)", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".stepper-input");

      await inputs[0].setValue("10");
      await inputs[1].setValue("5");
      await nextTick();

      expect(
        wrapper.find(".filter-apply-btn").attributes("disabled"),
      ).toBeDefined();
    });
  });

  describe("Events", () => {
    it("emits apply with parsed values", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".stepper-input");

      await inputs[0].setValue("10");
      await inputs[1].setValue("50");
      await wrapper.find(".filter-apply-btn").trigger("click");

      expect(wrapper.emitted("apply")).toBeTruthy();
      expect(wrapper.emitted("apply")![0]).toEqual([10, 50]);
    });

    it("emits apply with null for empty values", async () => {
      const wrapper = mountComponent({ minValue: 10, maxValue: 50 });
      const inputs = wrapper.findAll(".stepper-input");

      await inputs[0].setValue("");
      await inputs[1].setValue("");
      await wrapper.find(".filter-apply-btn").trigger("click");

      expect(wrapper.emitted("apply")![0]).toEqual([null, null]);
    });

    it("emits clear when clear button clicked", async () => {
      const wrapper = mountComponent({ minValue: 10, maxValue: 50 });
      const buttons = wrapper.findAll(".filter-apply-btn");

      await buttons[1].trigger("click");

      expect(wrapper.emitted("clear")).toBeTruthy();
    });

    it("clears inputs when clear is clicked", async () => {
      const wrapper = mountComponent({ minValue: 10, maxValue: 50 });
      const inputs = wrapper.findAll(".stepper-input");
      const buttons = wrapper.findAll(".filter-apply-btn");

      await buttons[1].trigger("click");

      expect((inputs[0].element as HTMLInputElement).value).toBe("");
      expect((inputs[1].element as HTMLInputElement).value).toBe("");
    });
  });

  describe("Negative Numbers", () => {
    it("converts negative to 0 when allowNegative is false", async () => {
      const wrapper = mountComponent({ allowNegative: false });
      const inputs = wrapper.findAll(".stepper-input");

      await inputs[0].setValue("-10");
      await wrapper.find(".filter-apply-btn").trigger("click");

      expect(wrapper.emitted("apply")![0]).toEqual([0, null]);
    });

    it("allows negative when allowNegative is true", async () => {
      const wrapper = mountComponent({ allowNegative: true });
      const inputs = wrapper.findAll(".stepper-input");

      await inputs[0].setValue("-10");
      await wrapper.find(".filter-apply-btn").trigger("click");

      expect(wrapper.emitted("apply")![0]).toEqual([-10, null]);
    });
  });

  // Each field, 0 for "От" and 1 for "До", answers to its own buttons and keys.
  describe("Steppers and keys of each field", () => {
    type Wrapper = ReturnType<typeof mountComponent>;
    const stepper = (wrapper: Wrapper, label: string, side: number) =>
      wrapper.findAll(`.stepper-btn[aria-label="${label}"]`)[side];
    const field = (wrapper: Wrapper, side: number) =>
      wrapper.findAll(".stepper-input")[side].element as HTMLInputElement;
    /** Whether the field let the text through, typed or pasted at its caret. */
    const takes = (
      wrapper: Wrapper,
      side: number,
      data: string,
      inputType = "insertText",
    ) =>
      field(wrapper, side).dispatchEvent(
        new InputEvent("beforeinput", { data, inputType, cancelable: true }),
      );

    it.each([0, 1])("steps the field %i by one either way", async (side) => {
      const wrapper = mountComponent({ minValue: 5, maxValue: 5 });

      await stepper(wrapper, "Увеличить", side).trigger("click");
      expect(field(wrapper, side).value).toBe("6");
      await stepper(wrapper, "Уменьшить", side).trigger("click");
      await stepper(wrapper, "Уменьшить", side).trigger("click");
      expect(field(wrapper, side).value).toBe("4");
      expect(field(wrapper, 1 - side).value).toBe("5");
    });

    it.each([0, 1])(
      "stops the field %i at 0 without negatives",
      async (side) => {
        const wrapper = mountComponent({ minValue: 1, maxValue: 1 });
        const down = stepper(wrapper, "Уменьшить", side);

        await down.trigger("click");
        expect(field(wrapper, side).value).toBe("0");
        expect(down.attributes("disabled")).toBeDefined();
        expect(
          stepper(wrapper, "Уменьшить", 1 - side).attributes("disabled"),
        ).toBeUndefined();
      },
    );

    it("steps below 0 with negatives allowed", async () => {
      const wrapper = mountComponent({ allowNegative: true });

      await stepper(wrapper, "Уменьшить", 1).trigger("click");
      expect(field(wrapper, 1).value).toBe("-1");
    });

    it.each([0, 1])(
      "keeps only the digits of a paste into the field %i",
      async (side) => {
        const wrapper = mountComponent();

        expect(takes(wrapper, side, "1a2", "insertFromPaste")).toBe(false);
        await nextTick();
        expect(field(wrapper, side).value).toBe("12");
        expect(field(wrapper, 1 - side).value).toBe("");
      },
    );

    it("takes digits and refuses letters", () => {
      const wrapper = mountComponent();

      expect(takes(wrapper, 0, "7")).toBe(true);
      expect(takes(wrapper, 1, "a")).toBe(false);
      expect(takes(wrapper, 1, "-")).toBe(false);
    });

    it("takes a minus once, at the start of its own field", () => {
      const wrapper = mountComponent({ allowNegative: true, minValue: -5 });
      field(wrapper, 0).setSelectionRange(0, 0);

      expect(takes(wrapper, 0, "-")).toBe(false);
      expect(takes(wrapper, 1, "-")).toBe(true);
    });
  });

  describe("Edge Cases", () => {
    it("handles invalid input gracefully", async () => {
      const wrapper = mountComponent({ minValue: 10 });
      const inputs = wrapper.findAll(".stepper-input");

      await inputs[0].setValue("abc");
      await wrapper.find(".filter-apply-btn").trigger("click");

      expect(wrapper.emitted("apply")![0]).toEqual([null, null]);
    });

    it("handles whitespace-only input", async () => {
      const wrapper = mountComponent({ minValue: 5 });
      const inputs = wrapper.findAll(".stepper-input");

      await inputs[0].setValue("   ");
      await wrapper.find(".filter-apply-btn").trigger("click");

      expect(wrapper.emitted("apply")![0]).toEqual([null, null]);
    });

    it("handles zero as valid value", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".stepper-input");

      await inputs[0].setValue("0");
      await wrapper.find(".filter-apply-btn").trigger("click");

      expect(wrapper.emitted("apply")![0]).toEqual([0, null]);
    });

    it("truncates decimals to integer (component is integer-only)", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".stepper-input");

      await inputs[0].setValue("10.5");
      await inputs[1].setValue("20.75");
      await wrapper.find(".filter-apply-btn").trigger("click");

      // parseInt("10.5") = 10, parseInt("20.75") = 20
      expect(wrapper.emitted("apply")![0]).toEqual([10, 20]);
    });
  });

  /**
   * Enter is the second way to apply, and for a while it was the unguarded
   * one: the button carries `:disabled="!canApply"` and explains itself with
   * "Минимум не может быть больше максимума", while the keydown handler
   * emitted whatever was typed. An inverted range went through from the
   * keyboard while the control on screen said it could not.
   */
  describe("Apply from the keyboard", () => {
    it("does not emit apply on Enter while the range is inverted", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".stepper-input");

      await inputs[0].setValue("10");
      await inputs[1].setValue("5");
      await nextTick();
      // The lock the keyboard has to obey, read off the button itself.
      expect(
        wrapper.find(".filter-apply-btn").attributes("disabled"),
      ).toBeDefined();

      await inputs[0].trigger("keydown", { key: "Enter" });
      await inputs[1].trigger("keydown", { key: "Enter" });

      expect(wrapper.emitted("apply")).toBeFalsy();
    });

    it("emits apply on Enter when the range is valid", async () => {
      const wrapper = mountComponent();
      const inputs = wrapper.findAll(".stepper-input");

      await inputs[0].setValue("10");
      await inputs[1].setValue("50");
      await nextTick();

      await inputs[0].trigger("keydown", { key: "Enter" });

      expect(wrapper.emitted("apply")![0]).toEqual([10, 50]);
    });

    it("does not emit apply on Enter when nothing changed", async () => {
      // The other half of canApply: the button is locked in this state too,
      // and the keyboard now answers to the same condition.
      const wrapper = mountComponent({ minValue: 10, maxValue: 50 });

      await wrapper
        .findAll(".stepper-input")[0]
        .trigger("keydown", { key: "Enter" });

      expect(wrapper.emitted("apply")).toBeFalsy();
    });
  });
});

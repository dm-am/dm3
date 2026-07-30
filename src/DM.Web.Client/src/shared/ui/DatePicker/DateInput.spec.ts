/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import DateInput from "./DateInput.vue";

describe("DateInput", () => {
  const mountComponent = (props: Record<string, unknown> = {}) => {
    return mount(DateInput, {
      props: {
        modelValue: null,
        ...props,
      },
    });
  };

  // ============================================================================
  // DISPLAY
  // ============================================================================

  describe("Display", () => {
    it("shows the model value as DD.MM.YYYY", () => {
      const wrapper = mountComponent({ modelValue: "2024-03-07" });
      const input = wrapper.find(".di-input").element as HTMLInputElement;
      expect(input.value).toBe("07.03.2024");
    });

    it("shows placeholder for empty value", () => {
      const wrapper = mountComponent();
      const input = wrapper.find(".di-input");
      expect(input.attributes("placeholder")).toBe("дд.мм.гггг");
      expect((input.element as HTMLInputElement).value).toBe("");
    });
  });

  // ============================================================================
  // MANUAL TYPING
  // ============================================================================

  describe("Manual typing", () => {
    it("emits ISO value for DD.MM.YYYY input", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".di-input").setValue("09.05.2025");
      expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([
        "2025-05-09",
      ]);
    });

    it("emits ISO value for single-digit day and month", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".di-input").setValue("9.5.2025");
      expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([
        "2025-05-09",
      ]);
    });

    it("accepts YYYY-MM-DD input", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".di-input").setValue("2025-05-09");
      expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([
        "2025-05-09",
      ]);
    });

    it("does not emit for partial or impossible dates", async () => {
      const wrapper = mountComponent();
      const input = wrapper.find(".di-input");
      await input.setValue("09.05");
      await input.setValue("31.02.2025");
      expect(wrapper.emitted("update:modelValue")).toBeUndefined();
    });

    it("emits null when cleared", async () => {
      const wrapper = mountComponent({ modelValue: "2024-03-07" });
      await wrapper.find(".di-input").setValue("");
      expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([null]);
    });

    it("reverts unparsed text to the committed value on blur", async () => {
      const wrapper = mountComponent({ modelValue: "2024-03-07" });
      const input = wrapper.find(".di-input");
      await input.setValue("garbage");
      await input.trigger("blur");
      expect((input.element as HTMLInputElement).value).toBe("07.03.2024");
    });
  });

  // ============================================================================
  // CALENDAR POPOVER
  // ============================================================================

  describe("Calendar popover", () => {
    it("opens on toggle button click and closes after picking a day", async () => {
      const wrapper = mountComponent({ modelValue: "2024-03-07" });
      await wrapper.find(".di-toggle").trigger("click");
      expect(wrapper.find(".calendar-grid").exists()).toBe(true);

      // The 15th of the visible month (March 2024)
      const day = wrapper
        .findAll(".dp-day:not(.out-month)")
        .find((d) => d.text() === "15");
      expect(day).toBeTruthy();
      await day!.trigger("click");

      expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([
        "2024-03-15",
      ]);
      expect(wrapper.find(".calendar-grid").exists()).toBe(false);
    });

    it("opens on input click", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".di-input").trigger("click");
      expect(wrapper.find(".calendar-grid").exists()).toBe(true);
    });

    it("closes on Escape and stops the event from bubbling further", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".di-toggle").trigger("click");
      expect(wrapper.find(".calendar-grid").exists()).toBe(true);

      let escaped = false;
      wrapper.element.parentElement?.addEventListener(
        "keydown",
        () => (escaped = true),
      );
      await wrapper.find(".di-input").trigger("keydown", { key: "Escape" });

      expect(wrapper.find(".calendar-grid").exists()).toBe(false);
      expect(escaped).toBe(false);
    });

    it("disables days outside min/max", async () => {
      const wrapper = mountComponent({
        modelValue: "2024-03-15",
        min: "2024-03-10",
        max: "2024-03-20",
      });
      await wrapper.find(".di-toggle").trigger("click");

      const days = wrapper.findAll(".dp-day:not(.out-month)");
      const day5 = days.find((d) => d.text() === "5");
      const day25 = days.find((d) => d.text() === "25");
      expect(day5!.attributes("disabled")).toBeDefined();
      expect(day25!.attributes("disabled")).toBeDefined();
      const day15 = days.find((d) => d.text() === "15");
      expect(day15!.attributes("disabled")).toBeUndefined();
    });
  });
});

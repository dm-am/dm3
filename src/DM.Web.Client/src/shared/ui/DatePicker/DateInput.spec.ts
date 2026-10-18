/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { IN_DIALOG } from "@/shared/ui/Layout/dialogContext";
import DateInput from "./DateInput.vue";

// "Today" for the bounds below: 28 September 2026.
beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(new Date(2026, 8, 28, 12, 0));
});

afterEach(() => {
  vi.useRealTimers();
  document.body.innerHTML = "";
});

describe("DateInput", () => {
  const mountComponent = (props: Record<string, unknown> = {}) => {
    return mount(DateInput, {
      props: {
        modelValue: null,
        ...props,
      },
      attachTo: document.body,
    });
  };

  const inputOf = (wrapper: ReturnType<typeof mountComponent>) =>
    wrapper.find(".di-input").element as HTMLInputElement;

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

    it("writes a date it takes out in full on blur", async () => {
      // Bound both ways, as v-model binds it.
      const wrapper = mountComponent({
        "onUpdate:modelValue": (value: string | null) =>
          wrapper.setProps({ modelValue: value }),
      });
      const input = wrapper.find(".di-input");
      await input.setValue("9.5.2025");
      await input.trigger("blur");
      expect(inputOf(wrapper).value).toBe("09.05.2025");
      expect(wrapper.emitted("reason")).toBeUndefined();
    });
  });

  // ============================================================================
  // WHAT IS NOT TAKEN
  // ============================================================================

  /**
   * A text the field does not take stays in it, and the field says why. It
   * used to be wiped on blur without a word: the reader saw the date they had
   * typed vanish and the old one come back.
   */
  describe("What is not taken", () => {
    const reasons = (wrapper: ReturnType<typeof mountComponent>) =>
      wrapper.emitted("reason")?.map(([value]) => value);

    it("keeps a date that does not exist, and says so on leaving", async () => {
      const wrapper = mountComponent({ modelValue: "2024-03-07" });
      const input = wrapper.find(".di-input");
      await input.setValue("31.02.2025");
      // Not while it is being typed: a date half typed is not a mistake.
      expect(wrapper.emitted("reason")).toBeUndefined();
      await input.trigger("blur");
      expect(inputOf(wrapper).value).toBe("31.02.2025");
      expect(reasons(wrapper)).toEqual(["Такой даты нет"]);
      expect(wrapper.emitted("update:modelValue")).toBeUndefined();
    });

    it("says a half typed date has no day either", async () => {
      const wrapper = mountComponent();
      const input = wrapper.find(".di-input");
      await input.setValue("14.03");
      await input.trigger("blur");
      expect(inputOf(wrapper).value).toBe("14.03");
      expect(reasons(wrapper)).toEqual(["Такой даты нет"]);
    });

    it("names the bound a date is out of", async () => {
      const early = mountComponent({ min: "2026-10-05" });
      await early.find(".di-input").setValue("04.10.2026");
      await early.find(".di-input").trigger("blur");
      expect(reasons(early)).toEqual(["Не раньше 05.10.2026"]);

      const late = mountComponent({ max: "2026-09-28" });
      await late.find(".di-input").setValue("29.09.2026");
      await late.find(".di-input").trigger("blur");
      expect(reasons(late)).toEqual(["Не позже 28.09.2026"]);
    });

    it("takes the reason back the moment the text is right", async () => {
      const wrapper = mountComponent();
      const input = wrapper.find(".di-input");
      await input.setValue("31.02.2025");
      await input.trigger("blur");
      await input.setValue("28.02.2025");
      expect(reasons(wrapper)).toEqual(["Такой даты нет", null]);
      expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([
        "2025-02-28",
      ]);
    });

    it("takes it back for a day picked in the calendar", async () => {
      const wrapper = mountComponent();
      const input = wrapper.find(".di-input");
      await input.setValue("31.02.2026");
      await input.trigger("blur");
      await wrapper.find(".di-toggle").trigger("click");
      const day = wrapper.findAll(".dp-day").find((d) => d.text() === "15");
      await day!.trigger("click");
      expect(reasons(wrapper)).toEqual(["Такой даты нет", null]);
    });

    it("says so at once when the bounds move from under a date", async () => {
      // The end of a period whose start moved past it.
      const wrapper = mountComponent({ modelValue: "2026-10-10" });
      await wrapper.setProps({ min: "2026-10-12" });
      expect(reasons(wrapper)).toEqual(["Не раньше 12.10.2026"]);
      await wrapper.setProps({ min: "2026-10-01" });
      expect(reasons(wrapper)).toEqual(["Не раньше 12.10.2026", null]);
    });

    it("says it of a date handed in out of bounds", () => {
      const wrapper = mountComponent({
        modelValue: "2026-10-10",
        max: "2026-09-28",
      });
      expect(reasons(wrapper)).toEqual(["Не позже 28.09.2026"]);
    });

    it("says nothing of a field emptied", async () => {
      const wrapper = mountComponent({ modelValue: "2024-03-07" });
      await wrapper.find(".di-input").setValue("");
      await wrapper.find(".di-input").trigger("blur");
      expect(wrapper.emitted("reason")).toBeUndefined();
    });
  });

  // ============================================================================
  // DOTS AS YOU TYPE
  // ============================================================================

  /**
   * The numeric keyboard of a phone often has no dot, and on a desktop the
   * dots were three keystrokes nobody wanted to type: "14031991" did not
   * parse, and the field threw it away on blur.
   */
  describe("Dots as you type", () => {
    it("puts the dots into a run of digits", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".di-input").setValue("14031991");
      expect(inputOf(wrapper).value).toBe("14.03.1991");
      expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([
        "1991-03-14",
      ]);
    });

    it("puts each dot in once the next group starts", async () => {
      const wrapper = mountComponent();
      const input = wrapper.find(".di-input");
      await input.setValue("14");
      expect(inputOf(wrapper).value).toBe("14");
      await input.setValue("140");
      expect(inputOf(wrapper).value).toBe("14.0");
      await input.setValue("14.031");
      expect(inputOf(wrapper).value).toBe("14.03.1");
    });

    it("lets backspace take a dot with the digit before it", async () => {
      // "14.0" and backspace is "14.": the dot goes too, rather than stand
      // there for the reader to delete a second time.
      const wrapper = mountComponent();
      await wrapper.find(".di-input").setValue("14.");
      expect(inputOf(wrapper).value).toBe("14");
    });

    it("leaves the dots a reader typed in places of their own", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".di-input").setValue("9.5.2025");
      expect(inputOf(wrapper).value).toBe("9.5.2025");
    });
  });

  // ============================================================================
  // BOUNDS
  // ============================================================================

  describe("Bounds", () => {
    it("does not take a typed date after max", async () => {
      const wrapper = mountComponent({ max: "2026-09-28" });
      const input = wrapper.find(".di-input");
      await input.setValue("01.10.2026");
      expect(wrapper.emitted("update:modelValue")).toBeUndefined();
      await input.setValue("28.09.2026");
      expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([
        "2026-09-28",
      ]);
    });

    it("does not take a typed date before min", async () => {
      const wrapper = mountComponent({ min: "2026-01-01" });
      await wrapper.find(".di-input").setValue("31.12.2025");
      expect(wrapper.emitted("update:modelValue")).toBeUndefined();
    });
  });

  // ============================================================================
  // CLEAR
  // ============================================================================

  describe("Clear", () => {
    it("offers the cross only when the field holds something", () => {
      const empty = mountComponent();
      expect(empty.find(".di-clear").exists()).toBe(false);
      const filled = mountComponent({ modelValue: "1991-03-14" });
      expect(filled.find(".di-clear").attributes("aria-label")).toBe(
        "Очистить дату",
      );
    });

    it("has no cross where a date must be given", () => {
      const wrapper = mountComponent({
        modelValue: "1991-03-14",
        clearable: false,
      });
      expect(wrapper.find(".di-clear").exists()).toBe(false);
      expect(wrapper.find(".di-toggle").exists()).toBe(true);
    });

    it("empties the field and says so", async () => {
      const wrapper = mountComponent({ modelValue: "1991-03-14" });
      await wrapper.find(".di-clear").trigger("click");
      expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([null]);
      expect(inputOf(wrapper).value).toBe("");
      expect(document.activeElement).toBe(inputOf(wrapper));
    });
  });

  // ============================================================================
  // KEYBOARD
  // ============================================================================

  describe("Keyboard", () => {
    const openByKey = async (wrapper: ReturnType<typeof mountComponent>) => {
      const input = wrapper.find(".di-input");
      (input.element as HTMLInputElement).focus();
      await input.trigger("keydown", { key: "ArrowDown" });
      await wrapper.vm.$nextTick();
      await wrapper.vm.$nextTick();
    };

    it("moves the focus from the field into the grid on the down arrow", async () => {
      const wrapper = mountComponent({ modelValue: "2024-03-07" });
      await openByKey(wrapper);
      expect(wrapper.find(".calendar-grid").exists()).toBe(true);
      const active = document.activeElement as HTMLElement;
      expect(active.classList.contains("dp-day")).toBe(true);
      expect(active.textContent?.trim()).toBe("7");
    });

    it("keeps the focus in the field when the calendar opens by click", async () => {
      const wrapper = mountComponent({ modelValue: "2024-03-07" });
      inputOf(wrapper).focus();
      await wrapper.find(".di-input").trigger("click");
      await wrapper.vm.$nextTick();
      expect(wrapper.find(".calendar-grid").exists()).toBe(true);
      expect(document.activeElement).toBe(inputOf(wrapper));
    });

    it("picks a day with Enter from the grid", async () => {
      // The field takes Enter for itself (normalize and close). Taken on the
      // whole control, it cancelled the Enter of a day in the grid.
      const wrapper = mountComponent({ modelValue: "2024-03-07" });
      await openByKey(wrapper);
      await wrapper.find(".dp-grid").trigger("keydown", { key: "ArrowRight" });
      await wrapper.find(".dp-grid").trigger("keydown", { key: "Enter" });
      expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([
        "2024-03-08",
      ]);
      expect(wrapper.find(".calendar-grid").exists()).toBe(false);
    });

    it("gives the focus back to the field on Escape from the grid", async () => {
      const wrapper = mountComponent({ modelValue: "2024-03-07" });
      await openByKey(wrapper);
      await wrapper.find(".dp-day.selected").trigger("keydown", {
        key: "Escape",
      });
      expect(wrapper.find(".calendar-grid").exists()).toBe(false);
      expect(document.activeElement).toBe(inputOf(wrapper));
    });
  });

  // ============================================================================
  // A FIELD OF A RANGE
  // ============================================================================

  describe("A field of a range (bare)", () => {
    it("has no calendar, icon or cross of its own", async () => {
      const wrapper = mountComponent({ bare: true, modelValue: "2024-03-07" });
      expect(wrapper.find(".di-toggle").exists()).toBe(false);
      expect(wrapper.find(".di-clear").exists()).toBe(false);
      await wrapper.find(".di-input").trigger("click");
      expect(wrapper.find(".calendar-grid").exists()).toBe(false);
    });

    it("asks for the calendar beside it on the down arrow", async () => {
      const wrapper = mountComponent({ bare: true });
      await wrapper.find(".di-input").trigger("keydown", { key: "ArrowDown" });
      expect(wrapper.emitted("focus-calendar")).toHaveLength(1);
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
      const day = wrapper.findAll(".dp-day").find((d) => d.text() === "15");
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

    it("opens an empty field on the years when asked to", async () => {
      const wrapper = mountComponent({ emptyLevel: "years" });
      await wrapper.find(".di-toggle").trigger("click");
      expect(wrapper.find(".myp-grid--years").exists()).toBe(true);
    });

    it("opens an empty field on the day it is asked to", async () => {
      // A birthday: the block of years round the current year less twelve,
      // and not the block of the last twelve, where nobody's birthday is.
      const wrapper = mountComponent({
        emptyLevel: "years",
        max: "2026-09-28",
        openOn: "2014-09-28",
      });
      await wrapper.find(".di-toggle").trigger("click");
      const years = wrapper.find(".myp-grid--years").text();
      expect(years).toContain("2003");
      expect(years).toContain("2014");
      expect(years).not.toContain("2015");
    });

    it("opens a filled field on its days all the same", async () => {
      const wrapper = mountComponent({
        emptyLevel: "years",
        modelValue: "1991-03-14",
      });
      await wrapper.find(".di-toggle").trigger("click");
      expect(wrapper.find(".dp-grid").exists()).toBe(true);
      expect(wrapper.find(".dp-day.selected").text()).toBe("14");
    });

    it("disables days outside min/max", async () => {
      const wrapper = mountComponent({
        modelValue: "2024-03-15",
        min: "2024-03-10",
        max: "2024-03-20",
      });
      await wrapper.find(".di-toggle").trigger("click");

      const days = wrapper.findAll(".dp-day");
      const day5 = days.find((d) => d.text() === "5");
      const day25 = days.find((d) => d.text() === "25");
      expect(day5!.attributes("disabled")).toBeDefined();
      expect(day25!.attributes("disabled")).toBeDefined();
      const day15 = days.find((d) => d.text() === "15");
      expect(day15!.attributes("disabled")).toBeUndefined();
    });
  });
});

// ============================================================================
// TOUCH
// ============================================================================

/**
 * On a phone the tap that opened the calendar also raised the numeric
 * keyboard, which covered the calendar. The first tap now opens the calendar
 * with the keyboard held down, and a second tap closes it and gives the
 * keyboard back. A mouse is as it was.
 */
describe("DateInput by touch", () => {
  const mountField = () =>
    mount(DateInput, {
      props: { modelValue: null },
      attachTo: document.body,
    });

  /** A tap: the press, then the click it ends in. */
  const tap = async (
    wrapper: ReturnType<typeof mountField>,
    pointerType = "touch",
  ) => {
    const input = wrapper.find(".di-input");
    const press = new Event("pointerdown", { bubbles: true });
    Object.defineProperty(press, "pointerType", { value: pointerType });
    input.element.dispatchEvent(press);
    await input.trigger("click");
  };

  const mode = (wrapper: ReturnType<typeof mountField>) =>
    (wrapper.find(".di-input").element as HTMLInputElement).inputMode;

  it("opens the calendar on the first tap with the keyboard down", async () => {
    const wrapper = mountField();
    await tap(wrapper);
    expect(wrapper.find(".calendar-grid").exists()).toBe(true);
    expect(mode(wrapper)).toBe("none");
    expect(wrapper.find(".di-input").attributes("inputmode")).toBe("none");
  });

  it("closes it on the second tap and gives the numeric keyboard", async () => {
    const wrapper = mountField();
    await tap(wrapper);
    await tap(wrapper);
    expect(wrapper.find(".calendar-grid").exists()).toBe(false);
    expect(mode(wrapper)).toBe("numeric");
  });

  it("keeps the keyboard down when a day is picked by touch", async () => {
    const wrapper = mountField();
    await tap(wrapper);
    const day = wrapper.findAll(".dp-day:not([disabled])")[0];
    await day.trigger("click");
    expect(wrapper.find(".calendar-grid").exists()).toBe(false);
    expect(document.activeElement).toBe(wrapper.find(".di-input").element);
    expect(mode(wrapper)).toBe("none");
  });

  it("leaves the mouse as it was: a click opens, a second keeps it open", async () => {
    const wrapper = mountField();
    await tap(wrapper, "mouse");
    expect(wrapper.find(".calendar-grid").exists()).toBe(true);
    expect(mode(wrapper)).toBe("numeric");
    await tap(wrapper, "mouse");
    expect(wrapper.find(".calendar-grid").exists()).toBe(true);
  });
});

// ============================================================================
// IN A WINDOW
// ============================================================================

describe("DateInput in a window", () => {
  it("is told by the window, and takes its measure", () => {
    const inside = mount(DateInput, {
      props: { modelValue: null },
      global: { provide: { [IN_DIALOG as symbol]: true } },
    });
    expect(inside.classes()).toContain("date-input-control--dialog");
    const outside = mount(DateInput, { props: { modelValue: null } });
    expect(outside.classes()).not.toContain("date-input-control--dialog");
  });

  it("gives its input the id a caption points at", () => {
    const wrapper = mount(DateInput, {
      props: { modelValue: null, inputId: "birthday" },
    });
    expect(wrapper.find(".di-input").attributes("id")).toBe("birthday");
  });
});

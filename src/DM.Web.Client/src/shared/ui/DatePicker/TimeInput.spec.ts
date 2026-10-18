/**
 * @vitest-environment jsdom
 */

/**
 * The time of day, typed in digits with the colon put in by the field, in
 * place of the browser's <input type="time"> with its own look and its "PM".
 */
import { describe, it, expect, afterEach } from "vitest";
import { mount } from "@vue/test-utils";
import { IN_DIALOG } from "@/shared/ui/Layout/dialogContext";
import TimeInput from "./TimeInput.vue";

afterEach(() => {
  document.body.innerHTML = "";
});

const mountField = (props: Record<string, unknown> = {}) =>
  mount(TimeInput, {
    props: { modelValue: null, ...props },
    attachTo: document.body,
  });

/** The input itself, as the reader types into it. */
const field = (wrapper: ReturnType<typeof mountField>) => wrapper.find("input");

const valueOf = (wrapper: ReturnType<typeof mountField>) =>
  (wrapper.element as HTMLInputElement).value;

const reasons = (wrapper: ReturnType<typeof mountField>) =>
  wrapper.emitted("reason")?.map(([value]) => value);

describe("TimeInput", () => {
  it("is a field of text and not the browser's time", () => {
    const wrapper = mountField();
    expect(wrapper.element.tagName).toBe("INPUT");
    expect(wrapper.attributes("type")).toBe("text");
    expect(wrapper.attributes("inputmode")).toBe("numeric");
    expect(wrapper.attributes("placeholder")).toBe("чч:мм");
  });

  it("shows the time it is given", () => {
    expect(valueOf(mountField({ modelValue: "09:30" }))).toBe("09:30");
  });

  it("puts the colon in: 2330 is 23:30", async () => {
    const wrapper = mountField();
    await field(wrapper).setValue("2330");
    expect(valueOf(wrapper)).toBe("23:30");
    expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual(["23:30"]);
  });

  it("puts it in once the minutes start, and not before", async () => {
    const wrapper = mountField();
    await field(wrapper).setValue("23");
    expect(valueOf(wrapper)).toBe("23");
    await field(wrapper).setValue("233");
    expect(valueOf(wrapper)).toBe("23:3");
    // Backspace over "23:" takes the colon with the digit.
    await field(wrapper).setValue("23:");
    expect(valueOf(wrapper)).toBe("23");
  });

  it("leaves a colon the reader put in a place of their own", async () => {
    const wrapper = mountField();
    await field(wrapper).setValue("9:30");
    expect(valueOf(wrapper)).toBe("9:30");
    expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual(["09:30"]);
  });

  it("writes a time out in full on leaving", async () => {
    const wrapper = mountField({
      "onUpdate:modelValue": (value: string | null) =>
        wrapper.setProps({ modelValue: value }),
    });
    await field(wrapper).setValue("9:30");
    await field(wrapper).trigger("blur");
    expect(valueOf(wrapper)).toBe("09:30");
    expect(wrapper.emitted("reason")).toBeUndefined();
  });

  it("does not take 25:00, keeps it and says why on leaving", async () => {
    const wrapper = mountField({ modelValue: "10:00" });
    await field(wrapper).setValue("2500");
    expect(valueOf(wrapper)).toBe("25:00");
    expect(wrapper.emitted("update:modelValue")).toBeUndefined();
    expect(wrapper.emitted("reason")).toBeUndefined();
    await field(wrapper).trigger("blur");
    expect(valueOf(wrapper)).toBe("25:00");
    expect(reasons(wrapper)).toEqual(["Такого времени нет"]);
  });

  it("does not take a minute of 60, nor a time half typed", async () => {
    const wrapper = mountField();
    await field(wrapper).setValue("1260");
    await field(wrapper).trigger("blur");
    await field(wrapper).setValue("125");
    await field(wrapper).trigger("blur");
    expect(wrapper.emitted("update:modelValue")).toBeUndefined();
    expect(reasons(wrapper)).toEqual(["Такого времени нет"]);
  });

  it("takes the reason back as soon as the time is right", async () => {
    const wrapper = mountField();
    await field(wrapper).setValue("2500");
    await field(wrapper).trigger("blur");
    await field(wrapper).setValue("2300");
    expect(reasons(wrapper)).toEqual(["Такого времени нет", null]);
    expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual(["23:00"]);
  });

  it("is emptied to null and says nothing", async () => {
    const wrapper = mountField({ modelValue: "10:00" });
    await field(wrapper).setValue("");
    await field(wrapper).trigger("blur");
    expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([null]);
    expect(wrapper.emitted("reason")).toBeUndefined();
  });

  it("takes the window's measure in a window", () => {
    const inside = mount(TimeInput, {
      props: { modelValue: null },
      global: { provide: { [IN_DIALOG as symbol]: true } },
    });
    expect(inside.classes()).toContain("time-input--dialog");
    expect(mountField().classes()).not.toContain("time-input--dialog");
  });
});

/**
 * @vitest-environment jsdom
 */

/**
 * A year, four digits, in place of the browser's number field with its arrows
 * up and down.
 */
import { describe, it, expect, afterEach } from "vitest";
import { mount } from "@vue/test-utils";
import { IN_DIALOG } from "@/shared/ui/Layout/dialogContext";
import YearInput from "./YearInput.vue";

afterEach(() => {
  document.body.innerHTML = "";
});

const mountField = (props: Record<string, unknown> = {}) =>
  mount(YearInput, {
    props: { modelValue: null, min: 2000, max: 2100, ...props },
    attachTo: document.body,
  });

/** The input itself, as the reader types into it. */
const field = (wrapper: ReturnType<typeof mountField>) => wrapper.find("input");

const valueOf = (wrapper: ReturnType<typeof mountField>) =>
  (wrapper.element as HTMLInputElement).value;

const reasons = (wrapper: ReturnType<typeof mountField>) =>
  wrapper.emitted("reason")?.map(([value]) => value);

describe("YearInput", () => {
  it("is a field of text of four, and not the browser's number", () => {
    const wrapper = mountField({ modelValue: 2026 });
    expect(wrapper.element.tagName).toBe("INPUT");
    expect(wrapper.attributes("type")).toBe("text");
    expect(wrapper.attributes("inputmode")).toBe("numeric");
    expect(wrapper.attributes("maxlength")).toBe("4");
    expect(valueOf(wrapper)).toBe("2026");
  });

  it("takes a year of four digits", async () => {
    const wrapper = mountField();
    await field(wrapper).setValue("2027");
    expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([2027]);
  });

  it("lets in digits only", async () => {
    const wrapper = mountField();
    await field(wrapper).setValue("2e0-2");
    expect(valueOf(wrapper)).toBe("202");
    expect(wrapper.emitted("update:modelValue")).toBeUndefined();
  });

  it("keeps a year too short and says why on leaving", async () => {
    const wrapper = mountField({ modelValue: 2026 });
    await field(wrapper).setValue("202");
    expect(wrapper.emitted("reason")).toBeUndefined();
    await field(wrapper).trigger("blur");
    expect(valueOf(wrapper)).toBe("202");
    expect(reasons(wrapper)).toEqual(["Год из четырех цифр"]);
    expect(wrapper.emitted("update:modelValue")).toBeUndefined();
  });

  it("names the bound a year is out of", async () => {
    const wrapper = mountField();
    await field(wrapper).setValue("1999");
    await field(wrapper).trigger("blur");
    await field(wrapper).setValue("2101");
    await field(wrapper).trigger("blur");
    expect(reasons(wrapper)).toEqual(["Не раньше 2000", "Не позже 2100"]);
    expect(wrapper.emitted("update:modelValue")).toBeUndefined();
  });

  it("takes the reason back as soon as the year is right", async () => {
    const wrapper = mountField();
    await field(wrapper).setValue("1999");
    await field(wrapper).trigger("blur");
    await field(wrapper).setValue("2009");
    expect(reasons(wrapper)).toEqual(["Не раньше 2000", null]);
    expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([2009]);
  });

  // Enter in a field of a form sends it: that is the browser's own default
  // action of the key, and a field that cancels the key holds the form.
  const enter = (wrapper: ReturnType<typeof mountField>) => {
    const press = new KeyboardEvent("keydown", {
      key: "Enter",
      bubbles: true,
      cancelable: true,
    });
    wrapper.element.dispatchEvent(press);
    return press.defaultPrevented;
  };

  it("holds Enter on a year it does not take, and says why", async () => {
    const wrapper = mountField({ modelValue: 2026 });
    await field(wrapper).setValue("202");
    expect(enter(wrapper)).toBe(true);
    expect(reasons(wrapper)).toEqual(["Год из четырех цифр"]);
  });

  it("lets Enter send a year it takes", async () => {
    const wrapper = mountField();
    await field(wrapper).setValue("2031");
    expect(enter(wrapper)).toBe(false);
  });

  it("is emptied to null", async () => {
    const wrapper = mountField({ modelValue: 2026 });
    await field(wrapper).setValue("");
    expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([null]);
  });

  it("takes the window's measure in a window", () => {
    const inside = mount(YearInput, {
      props: { modelValue: null },
      global: { provide: { [IN_DIALOG as symbol]: true } },
    });
    expect(inside.classes()).toContain("year-input--dialog");
    expect(mountField().classes()).not.toContain("year-input--dialog");
  });
});

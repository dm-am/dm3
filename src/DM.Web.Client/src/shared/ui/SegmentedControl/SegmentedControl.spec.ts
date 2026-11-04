/**
 * @vitest-environment jsdom
 */

// The toggle of a mode or a period: one look at every width, the ordinary pill.
import { describe, it, expect, afterEach } from "vitest";
import { mount } from "@vue/test-utils";
import SegmentedControl from "./SegmentedControl.vue";

const OPTIONS = [
  { value: "month", label: "Месяц" },
  { value: "year", label: "Год" },
  { value: "all", label: "Все время" },
] as const;

afterEach(() => {
  document.body.innerHTML = "";
});

const mountControl = (props: Record<string, unknown> = {}) =>
  mount(SegmentedControl, {
    props: {
      modelValue: "month",
      options: OPTIONS,
      ariaLabel: "Период",
      ...props,
    },
    attachTo: document.body,
  });

describe("SegmentedControl", () => {
  it("is a named group of buttons, the chosen one pressed", () => {
    const wrapper = mountControl({ modelValue: "year" });
    const group = wrapper.get('[role="group"]');
    expect(group.attributes("aria-label")).toBe("Период");
    const pressed = wrapper
      .findAll("button")
      .map((b) => [b.text(), b.attributes("aria-pressed")]);
    expect(pressed).toEqual([
      ["Месяц", "false"],
      ["Год", "true"],
      ["Все время", "false"],
    ]);
  });

  it("says which segment was chosen", async () => {
    const wrapper = mountControl();
    await wrapper.findAll("button")[2].trigger("click");
    expect(wrapper.emitted("update:modelValue")).toEqual([["all"]]);
  });
});

/**
 * @vitest-environment jsdom
 */

/**
 * The toggle of a mode or a period: "Месяц | Год | Все время".
 *
 * One look at every width, the ordinary pill, as before 5 October 2026: a
 * phone shows the pill of the full version, its segments at the width of
 * their words and one inset from their hairlines.
 *
 * The looks are rules of the compiled stylesheet, read off it, since jsdom
 * applies none.
 */
import { describe, it, expect, afterEach } from "vitest";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { mount } from "@vue/test-utils";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import SegmentedControl from "./SegmentedControl.vue";

const SFC = join(
  dirname(fileURLToPath(import.meta.url)),
  "SegmentedControl.vue",
);

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

  it("is the ordinary pill, with no other look to be asked for", () => {
    const root = mountControl().get(".segmented-control");
    expect(root.classes()).toEqual(["segmented-control"]);
    expect(root.attributes("style")).toBeUndefined();
    // The pill stretched to its box was a look for a phone's row; a phone
    // shows the pill of the full version.
    expect(ruleMap(componentCss(SFC)).has(".segmented-control--stretch")).toBe(
      false,
    );
  });

  it("keeps its words one inset from the hairlines, with no closer look", () => {
    const rules = ruleMap(componentCss(SFC));
    expect(rules.get(".segment")?.padding).toBe("0 16px");
    const insets = [...rules]
      .filter(([selector]) => /\.segment\b/.test(selector))
      .filter(([, declarations]) => declarations.padding !== undefined)
      .map(([selector]) => selector);
    expect(insets).toEqual([".segment"]);
  });
});

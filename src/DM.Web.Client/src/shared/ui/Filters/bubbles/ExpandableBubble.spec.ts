/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import ExpandableBubble from "./ExpandableBubble.vue";

// Mock SvgIcon
const SvgIconStub = {
  template: '<span class="svg-icon-stub" />',
  props: ["name"],
};

describe("ExpandableBubble", () => {
  const mountComponent = (props = {}) => {
    return mount(ExpandableBubble, {
      props: {
        prefix: "Авторы:",
        values: [],
        ...props,
      },
      global: {
        stubs: {
          SvgIcon: SvgIconStub,
        },
      },
    });
  };

  // ============================================================================
  // SINGLE VALUE
  // ============================================================================

  describe("Single Value", () => {
    it("renders simple bubble for single value", () => {
      const wrapper = mountComponent({
        values: [{ id: "1", label: "User1" }],
      });
      expect(wrapper.find(".bubble-single-owner").exists()).toBe(true);
      expect(wrapper.find(".bubble-expandable").exists()).toBe(false);
    });

    it("shows prefix", () => {
      const wrapper = mountComponent({
        prefix: "Автор:",
        values: [{ id: "1", label: "User1" }],
      });
      expect(wrapper.find(".bubble-prefix").text()).toBe("Автор:");
    });

    it("shows value label", () => {
      const wrapper = mountComponent({
        values: [{ id: "1", label: "TestUser" }],
      });
      expect(wrapper.find(".bubble-value-text").text()).toBe("TestUser");
    });

    it("emits remove when remove button clicked", async () => {
      const wrapper = mountComponent({
        values: [{ id: "user-1", label: "User1" }],
      });
      await wrapper.find(".bubble-owner-remove").trigger("click");

      expect(wrapper.emitted("remove")).toBeTruthy();
      expect(wrapper.emitted("remove")![0]).toEqual(["user-1"]);
    });
  });

  // ============================================================================
  // MULTIPLE VALUES - EXPANDABLE
  // ============================================================================

  describe("Multiple Values - Expandable", () => {
    const multipleValues = [
      { id: "1", label: "User1" },
      { id: "2", label: "User2" },
      { id: "3", label: "User3" },
    ];

    it("renders expandable bubble for multiple values", () => {
      const wrapper = mountComponent({ values: multipleValues });
      expect(wrapper.find(".bubble-expandable").exists()).toBe(true);
    });

    it("shows only maxVisible values", () => {
      const wrapper = mountComponent({
        values: multipleValues,
        maxVisible: 1,
      });
      const visibleItems = wrapper.findAll(".bubble-owner-item");
      expect(visibleItems.length).toBe(1);
    });

    it("shows remaining count", () => {
      const wrapper = mountComponent({
        values: multipleValues,
        maxVisible: 1,
      });
      expect(wrapper.find(".bubble-remaining").text()).toContain("и еще 2");
    });

    it("shows expand button", () => {
      const wrapper = mountComponent({
        values: multipleValues,
        maxVisible: 1,
      });
      expect(wrapper.find(".bubble-expand-btn").exists()).toBe(true);
    });

    it("toggles dropdown on expand button click", async () => {
      const wrapper = mountComponent({
        values: multipleValues,
        maxVisible: 1,
      });

      expect(wrapper.find(".owners-dropdown").exists()).toBe(false);

      await wrapper.find(".bubble-expand-btn").trigger("click");
      expect(wrapper.find(".owners-dropdown").exists()).toBe(true);

      await wrapper.find(".bubble-expand-btn").trigger("click");
      expect(wrapper.find(".owners-dropdown").exists()).toBe(false);
    });

    it("shows hidden values in dropdown", async () => {
      const wrapper = mountComponent({
        values: multipleValues,
        maxVisible: 1,
      });

      await wrapper.find(".bubble-expand-btn").trigger("click");

      const dropdownItems = wrapper.findAll(".owners-dropdown-item");
      expect(dropdownItems.length).toBe(2); // User2 and User3
    });

    it("emits remove when dropdown item clicked", async () => {
      const wrapper = mountComponent({
        values: multipleValues,
        maxVisible: 1,
      });

      await wrapper.find(".bubble-expand-btn").trigger("click");
      await wrapper.find(".owners-dropdown-item").trigger("click");

      expect(wrapper.emitted("remove")).toBeTruthy();
    });
  });

  // ============================================================================
  // SORTING
  // ============================================================================

  describe("Sorting", () => {
    it("sorts values alphabetically", () => {
      const wrapper = mountComponent({
        values: [
          { id: "c", label: "Zack" },
          { id: "a", label: "Alice" },
          { id: "b", label: "Bob" },
        ],
        maxVisible: 3,
      });

      const items = wrapper.findAll(".bubble-value-text");
      expect(items[0].text()).toBe("Alice");
      expect(items[1].text()).toBe("Bob");
      expect(items[2].text()).toBe("Zack");
    });
  });

  // ============================================================================
  // MAX VISIBLE CUSTOMIZATION
  // ============================================================================

  describe("Max Visible Customization", () => {
    it("respects maxVisible prop", () => {
      const values = [
        { id: "1", label: "User1" },
        { id: "2", label: "User2" },
        { id: "3", label: "User3" },
        { id: "4", label: "User4" },
      ];

      const wrapper = mountComponent({
        values,
        maxVisible: 2,
      });

      const visibleItems = wrapper.findAll(".bubble-owner-item");
      expect(visibleItems.length).toBe(2);
      expect(wrapper.find(".bubble-remaining").text()).toContain("и еще 2");
    });
  });

  // ============================================================================
  // THE SPACE AFTER THE COLON
  // ============================================================================

  /**
   * "Ведущий: TestUser" with one space, on the screen and in a copy.
   *
   * The prefix ends in a space of its own, as in every bubble, and the name
   * stands in a pill that lights up under the pointer. The pill's side stood
   * beside that space and doubled it: "Ведущий:  TestUser" (7.73 after the
   * colon against a space of 3.75, measured 13 October 2026). And the pill was
   * a flex box, whose items copy each on its own line: "Ведущий: \nTestUser".
   * jsdom lays nothing out, so the rules are read as the build compiles them.
   */
  describe("the space after the colon", () => {
    const rules = ruleMap(
      componentCss(
        join(dirname(fileURLToPath(import.meta.url)), "ExpandableBubble.vue"),
      ),
    );

    // The side the pill keeps toward the colon and the step it takes back over
    // the space cancel out, so the name stands one space after the colon. The
    // side is 2 and not the 4 of the rest of the pill: with all of the 4 taken
    // back, the light of a name under the pointer in a bubble of several
    // started on the colon itself (0.27 left of its end against a space of
    // 3.75, measured 13 October 2026); with 2 it starts inside the space.
    it("is the prefix's one space, with the light of the pill short of the colon", () => {
      const pill = rules.get(".bubble-owner-item");
      expect(pill?.padding).toBe("2px 4px");
      expect(rules.get(".bubble-prefix + .bubble-owner-item")).toEqual({
        "padding-left": "2px",
        "margin-left": "-2px",
      });
    });

    it("copies on one line: the pill is not a flex box", () => {
      expect(rules.get(".bubble-owner-item")?.display).toBe("inline-block");
    });

    /**
     * The cross of a name stands as far from the name as from what follows
     * it: "и еще N" in a bubble of several, the frame in a bubble of one. A
     * step of 8 before it, the bubble's side; after it the side of the pill
     * (4) and the space "и еще N" opens with (3.74 at 14), or the bubble's
     * side where the pill has none. After it stood 14.69 against 10.95 before
     * it, measured on the stand 14 October 2026: "и еще N" kept a margin of 4
     * over its space. The cross is drawn at 16, the size of the cross of the
     * search beside the bubbles.
     */
    it("stands the same step from the name and from what follows it", () => {
      expect(rules.get(".bubble-owner-remove")).toMatchObject({
        "margin-left": "8px",
        "font-size": "16px",
      });
      expect(rules.get(".bubble-remaining")?.["margin-left"]).toBeUndefined();
      expect(
        rules.get(".bubble-single-owner .bubble-owner-item")?.["padding-right"],
      ).toBe("0");

      const wrapper = mountComponent({
        prefix: "Ведущие:",
        values: [
          { id: "1", label: "SolohinLex" },
          { id: "2", label: "TestUser" },
        ],
        maxVisible: 1,
      });
      expect(wrapper.get(".bubble-remaining").element.textContent).toMatch(
        /^ и еще 1/,
      );
      wrapper.unmount();
    });

    it("stands between the prefix and the name and nowhere else in the text", () => {
      for (const values of [
        [{ id: "1", label: "TestUser" }],
        [
          { id: "1", label: "TestUser" },
          { id: "2", label: "SolohinLex" },
        ],
      ]) {
        const wrapper = mountComponent({
          prefix: values.length > 1 ? "Ведущие:" : "Ведущий:",
          values,
        });
        const prefix = wrapper.get(".bubble-prefix").element.textContent;
        const name = wrapper.get(".bubble-owner-item").element.textContent;
        expect(prefix).toMatch(/^Ведущи[йе]: $/);
        expect(name).toMatch(values.length > 1 ? /^SolohinLex/ : /^TestUser/);
        wrapper.unmount();
      }
    });
  });
});

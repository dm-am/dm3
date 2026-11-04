/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import ExpandableBubble from "./ExpandableBubble.vue";

describe("ExpandableBubble", () => {
  const mountComponent = (props = {}) => {
    return mount(ExpandableBubble, {
      props: {
        prefix: "Авторы:",
        values: [],
        ...props,
      },
    });
  };

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

    // The list closes on any removal, even with values still hidden behind it.
    it.each([".owners-dropdown-item", ".bubble-owner-remove"])(
      "closes the list when a value is taken off by %s",
      async (cross) => {
        const wrapper = mountComponent({
          values: [
            ...multipleValues,
            { id: "user-4", label: "User4" },
            { id: "user-5", label: "User5" },
          ],
          maxVisible: 1,
        });

        await wrapper.find(".bubble-expand-btn").trigger("click");
        await wrapper.find(cross).trigger("click");

        expect(wrapper.find(".owners-dropdown").exists()).toBe(false);
      },
    );
  });

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

  /**
   * "Ведущий: TestUser" with one space, on the screen and in a copy.
   *
   * The prefix ends in a space of its own, as in every bubble, and the name
   * stands in a pill that lights up under the pointer: the space is the
   * prefix's alone.
   */
  describe("the space after the colon", () => {
    it("opens what follows the name with its own space", () => {
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

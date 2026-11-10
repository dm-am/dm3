/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import FilterDropdownItem from "./FilterDropdownItem.vue";

describe("FilterDropdownItem", () => {
  const mountComponent = (props = {}) =>
    mount(FilterDropdownItem, {
      props: {
        label: "Test Label",
        ...props,
      },
    });

  describe("Basic Rendering", () => {
    it("renders label text", () => {
      const wrapper = mountComponent({ label: "My Label" });
      expect(wrapper.find(".item-label").text()).toBe("My Label");
    });

    it("renders hint when provided", () => {
      const wrapper = mountComponent({ hint: "Helpful hint" });
      expect(wrapper.find(".item-hint").exists()).toBe(true);
      expect(wrapper.find(".item-hint").text()).toBe("Helpful hint");
    });
  });

  describe("Events", () => {
    it("emits item-select when clicked", async () => {
      const wrapper = mountComponent();
      await wrapper.find("button").trigger("click");

      expect(wrapper.emitted("item-select")).toBeTruthy();
      expect(wrapper.emitted("item-select")).toHaveLength(1);
    });

    it("emits mouseenter on mouse enter", async () => {
      const wrapper = mountComponent();
      await wrapper.find("button").trigger("mouseenter");

      expect(wrapper.emitted("mouseenter")).toBeTruthy();
      expect(wrapper.emitted("mouseenter")).toHaveLength(1);
    });
  });
});

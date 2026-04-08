/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import FilterDropdownItem from "../primitives/FilterDropdownItem.vue";

describe("FilterDropdownItem", () => {
  const mountComponent = (props = {}) => {
    return mount(FilterDropdownItem, {
      props: {
        label: "Test Label",
        ...props,
      },
    });
  };

  // ============================================================================
  // BASIC RENDERING
  // ============================================================================

  describe("Basic Rendering", () => {
    it("renders as a button", () => {
      const wrapper = mountComponent();
      expect(wrapper.find("button.dropdown-item").exists()).toBe(true);
    });

    it("renders label text", () => {
      const wrapper = mountComponent({ label: "My Label" });
      expect(wrapper.find(".item-label").text()).toBe("My Label");
    });

    it("renders hint when provided", () => {
      const wrapper = mountComponent({ hint: "Helpful hint" });
      expect(wrapper.find(".item-hint").exists()).toBe(true);
      expect(wrapper.find(".item-hint").text()).toBe("Helpful hint");
    });

    it("does not render hint when not provided", () => {
      const wrapper = mountComponent();
      expect(wrapper.find(".item-hint").exists()).toBe(false);
    });

    it("renders avatar when avatarUrl provided", () => {
      const wrapper = mountComponent({ avatarUrl: "https://example.com/avatar.png" });
      const img = wrapper.find("img.item-avatar");
      expect(img.exists()).toBe(true);
      expect(img.attributes("src")).toBe("https://example.com/avatar.png");
    });

    it("does not render avatar when not provided", () => {
      const wrapper = mountComponent();
      expect(wrapper.find("img.item-avatar").exists()).toBe(false);
    });

    it("renders arrow when hasSubOptions is true", () => {
      const wrapper = mountComponent({ hasSubOptions: true });
      expect(wrapper.find(".item-arrow").exists()).toBe(true);
    });

    it("does not render arrow when hasSubOptions is false", () => {
      const wrapper = mountComponent({ hasSubOptions: false });
      expect(wrapper.find(".item-arrow").exists()).toBe(false);
    });
  });

  // ============================================================================
  // CSS CLASSES
  // ============================================================================

  describe("CSS Classes", () => {
    it("adds highlighted class when highlighted is true", () => {
      const wrapper = mountComponent({ highlighted: true });
      expect(wrapper.find(".dropdown-item").classes()).toContain("highlighted");
    });

    it("does not add highlighted class when highlighted is false", () => {
      const wrapper = mountComponent({ highlighted: false });
      expect(wrapper.find(".dropdown-item").classes()).not.toContain("highlighted");
    });

    it("adds indented class when indent is true", () => {
      const wrapper = mountComponent({ indent: true });
      expect(wrapper.find(".dropdown-item").classes()).toContain("indented");
    });

    it("does not add indented class when indent is false", () => {
      const wrapper = mountComponent({ indent: false });
      expect(wrapper.find(".dropdown-item").classes()).not.toContain("indented");
    });
  });

  // ============================================================================
  // EVENTS
  // ============================================================================

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

  // ============================================================================
  // EDGE CASES
  // ============================================================================

  describe("Edge Cases", () => {
    it("handles empty label", () => {
      const wrapper = mountComponent({ label: "" });
      expect(wrapper.find(".item-label").text()).toBe("");
    });

    it("handles very long label", () => {
      const longLabel = "A".repeat(100);
      const wrapper = mountComponent({ label: longLabel });
      expect(wrapper.find(".item-label").text()).toBe(longLabel);
    });

    it("handles all props at once", () => {
      const wrapper = mountComponent({
        label: "Full Item",
        hint: "With hint",
        avatarUrl: "https://example.com/avatar.png",
        highlighted: true,
        indent: true,
        hasSubOptions: true,
      });

      expect(wrapper.find(".item-label").text()).toBe("Full Item");
      expect(wrapper.find(".item-hint").text()).toBe("With hint");
      expect(wrapper.find("img.item-avatar").exists()).toBe(true);
      expect(wrapper.find(".dropdown-item").classes()).toContain("highlighted");
      expect(wrapper.find(".dropdown-item").classes()).toContain("indented");
      expect(wrapper.find(".item-arrow").exists()).toBe(true);
    });
  });
});

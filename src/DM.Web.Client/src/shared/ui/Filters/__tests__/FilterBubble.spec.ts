/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import FilterBubble from "../bubbles/FilterBubble.vue";

// Mock SvgIcon
const SvgIconStub = {
  template: '<span class="svg-icon-stub" />',
  props: ["name"],
};

describe("FilterBubble", () => {
  const mountComponent = (props = {}) => {
    return mount(FilterBubble, {
      props: {
        value: "Test Value",
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
  // BASIC RENDERING
  // ============================================================================

  describe("Basic Rendering", () => {
    it("renders bubble container", () => {
      const wrapper = mountComponent();
      expect(wrapper.find(".bubble").exists()).toBe(true);
    });

    it("renders value text", () => {
      const wrapper = mountComponent({ value: "Active" });
      expect(wrapper.find(".bubble-value-text").text()).toBe("Active");
    });

    it("renders prefix when provided", () => {
      const wrapper = mountComponent({ prefix: "Status:", value: "Active" });
      expect(wrapper.find(".bubble-prefix").exists()).toBe(true);
      expect(wrapper.find(".bubble-prefix").text()).toBe("Status:");
    });

    it("does not render prefix when not provided", () => {
      const wrapper = mountComponent({ value: "Active" });
      expect(wrapper.find(".bubble-prefix").exists()).toBe(false);
    });

    it("renders remove button", () => {
      const wrapper = mountComponent();
      expect(wrapper.find(".bubble-remove-btn").exists()).toBe(true);
    });

    it("renders close symbol inside button", () => {
      const wrapper = mountComponent();
      const btn = wrapper.find(".bubble-remove-btn");
      expect(btn.text()).toContain("\u00D7");
    });
  });

  // ============================================================================
  // EVENTS
  // ============================================================================

  describe("Events", () => {
    it("emits remove when remove button is clicked", async () => {
      const wrapper = mountComponent();
      await wrapper.find(".bubble-remove-btn").trigger("click");

      expect(wrapper.emitted("remove")).toBeTruthy();
      expect(wrapper.emitted("remove")).toHaveLength(1);
    });

    it("stops propagation on remove click", async () => {
      const wrapper = mountComponent();
      const event = { stopPropagation: () => {} };

      // Click with stop modifier should prevent bubbling
      await wrapper.find(".bubble-remove-btn").trigger("click.stop");

      expect(wrapper.emitted("remove")).toBeTruthy();
    });
  });

  // ============================================================================
  // CONTENT DISPLAY
  // ============================================================================

  describe("Content Display", () => {
    it("displays prefix followed by space", () => {
      const wrapper = mountComponent({ prefix: "Tag:", value: "Fantasy" });
      const prefixEl = wrapper.find(".bubble-prefix");
      // Prefix should include trailing space for visual separation
      expect(prefixEl.text().endsWith(":")).toBe(true);
    });

    it("displays value correctly", () => {
      const wrapper = mountComponent({ value: "Some Filter Value" });
      expect(wrapper.find(".bubble-value-text").text()).toBe("Some Filter Value");
    });

    it("handles empty value", () => {
      const wrapper = mountComponent({ value: "" });
      expect(wrapper.find(".bubble-value-text").text()).toBe("");
    });

    it("handles very long value", () => {
      const longValue = "A".repeat(100);
      const wrapper = mountComponent({ value: longValue });
      expect(wrapper.find(".bubble-value-text").text()).toBe(longValue);
    });

    it("handles special characters in value", () => {
      const wrapper = mountComponent({ value: "<script>alert('xss')</script>" });
      expect(wrapper.find(".bubble-value-text").text()).toBe("<script>alert('xss')</script>");
    });
  });

  // ============================================================================
  // VARIATIONS
  // ============================================================================

  describe("Variations", () => {
    it("renders status bubble correctly", () => {
      const wrapper = mountComponent({
        prefix: "Статус:",
        value: "Активен",
      });
      expect(wrapper.find(".bubble-prefix").text()).toBe("Статус:");
      expect(wrapper.find(".bubble-value-text").text()).toBe("Активен");
    });

    it("renders date bubble correctly", () => {
      const wrapper = mountComponent({
        prefix: "Дата:",
        value: "01.01.2024 — 31.12.2024",
      });
      expect(wrapper.find(".bubble-prefix").text()).toBe("Дата:");
      expect(wrapper.find(".bubble-value-text").text()).toBe("01.01.2024 — 31.12.2024");
    });

    it("renders tag bubble correctly", () => {
      const wrapper = mountComponent({
        prefix: "Тег:",
        value: "Фэнтези",
      });
      expect(wrapper.find(".bubble-prefix").text()).toBe("Тег:");
      expect(wrapper.find(".bubble-value-text").text()).toBe("Фэнтези");
    });

    it("renders value-only bubble correctly", () => {
      const wrapper = mountComponent({
        value: "Solo Filter",
      });
      expect(wrapper.find(".bubble-prefix").exists()).toBe(false);
      expect(wrapper.find(".bubble-value-text").text()).toBe("Solo Filter");
    });
  });

  // ============================================================================
  // ACCESSIBILITY
  // ============================================================================

  describe("Accessibility", () => {
    it("remove button is a button element", () => {
      const wrapper = mountComponent();
      const btn = wrapper.find(".bubble-remove-btn");
      expect(btn.element.tagName).toBe("BUTTON");
    });

    it("remove button has type button", () => {
      const wrapper = mountComponent();
      const btn = wrapper.find(".bubble-remove-btn");
      expect(btn.attributes("type")).toBe("button");
    });
  });

  // ============================================================================
  // EDGE CASES
  // ============================================================================

  describe("Edge Cases", () => {
    it("handles cyrillic text", () => {
      const wrapper = mountComponent({
        prefix: "Статус игры:",
        value: "Идет игра",
      });
      expect(wrapper.find(".bubble-prefix").text()).toBe("Статус игры:");
      expect(wrapper.find(".bubble-value-text").text()).toBe("Идет игра");
    });

    it("handles numbers as value", () => {
      const wrapper = mountComponent({ value: "123" });
      expect(wrapper.find(".bubble-value-text").text()).toBe("123");
    });

    it("handles emoji in value", () => {
      const wrapper = mountComponent({ value: "Active 🎮" });
      expect(wrapper.find(".bubble-value-text").text()).toBe("Active 🎮");
    });
  });
});

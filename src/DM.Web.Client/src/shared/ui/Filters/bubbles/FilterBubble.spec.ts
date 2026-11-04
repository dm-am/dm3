/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import FilterBubble from "./FilterBubble.vue";

describe("FilterBubble", () => {
  const mountComponent = (props = {}) =>
    mount(FilterBubble, { props: { value: "Test Value", ...props } });

  it("renders value text", () => {
    const wrapper = mountComponent({ value: "Active" });
    expect(wrapper.find(".bubble-value-text").text()).toBe("Active");
  });

  it("renders prefix when provided", () => {
    const wrapper = mountComponent({ prefix: "Status:", value: "Active" });
    expect(wrapper.find(".bubble-prefix").text()).toBe("Status:");
  });

  it("does not render prefix when not provided", () => {
    const wrapper = mountComponent({ value: "Active" });
    expect(wrapper.find(".bubble-prefix").exists()).toBe(false);
  });

  it("renders close symbol inside button", () => {
    const wrapper = mountComponent();
    const btn = wrapper.find(".bubble-remove-btn");
    expect(btn.text()).toContain("\u00D7");
  });

  it("emits remove when remove button is clicked", async () => {
    const wrapper = mountComponent();
    await wrapper.find(".bubble-remove-btn").trigger("click");

    expect(wrapper.emitted("remove")).toHaveLength(1);
  });

  it("handles special characters in value", () => {
    const wrapper = mountComponent({
      value: "<script>alert('xss')</script>",
    });
    expect(wrapper.find(".bubble-value-text").text()).toBe(
      "<script>alert('xss')</script>",
    );
  });
});

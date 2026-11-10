/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import FilterSearchInput from "./FilterSearchInput.vue";

describe("FilterSearchInput", () => {
  const mountComponent = (props = {}) =>
    mount(FilterSearchInput, {
      props: {
        modelValue: "",
        ...props,
      },
    });

  describe("Basic Rendering", () => {
    it("renders input with placeholder", () => {
      const wrapper = mountComponent({ placeholder: "Поиск..." });
      const input = wrapper.find("input");
      expect(input.attributes("placeholder")).toBe("Поиск...");
    });

    it("uses default placeholder if not provided", () => {
      const wrapper = mountComponent();
      const input = wrapper.find("input");
      expect(input.attributes("placeholder")).toBe("Поиск");
    });

    it("shows clear button when input has value", async () => {
      const wrapper = mountComponent({ modelValue: "test" });
      expect(wrapper.find(".clear-input-btn").exists()).toBe(true);
    });

    it("hides clear button when input is empty", () => {
      const wrapper = mountComponent({ modelValue: "" });
      expect(wrapper.find(".clear-input-btn").exists()).toBe(false);
    });
  });

  describe("Input Behavior", () => {
    it("updates local value immediately on input", async () => {
      const wrapper = mountComponent();
      const input = wrapper.find("input");

      await input.setValue("test");
      expect((input.element as HTMLInputElement).value).toBe("test");
    });

    it("emits update:modelValue immediately on input", async () => {
      const wrapper = mountComponent();
      const input = wrapper.find("input");

      await input.setValue("test");

      expect(wrapper.emitted("update:modelValue")).toBeTruthy();
      expect(wrapper.emitted("update:modelValue")![0]).toEqual(["test"]);
    });

    it("emits input event on typing", async () => {
      const wrapper = mountComponent();
      const input = wrapper.find("input");

      await input.setValue("test");

      expect(wrapper.emitted("input")).toBeTruthy();
    });

    it("emits keydown events", async () => {
      const wrapper = mountComponent();
      const input = wrapper.find("input");

      await input.trigger("keydown", { key: "Escape" });

      expect(wrapper.emitted("keydown")).toBeTruthy();
    });
  });

  describe("Clear Button", () => {
    it("clears input when clear button is clicked", async () => {
      const wrapper = mountComponent({ modelValue: "test" });
      const clearBtn = wrapper.find(".clear-input-btn");

      await clearBtn.trigger("click");

      expect(wrapper.emitted("update:modelValue")).toBeTruthy();
      expect(wrapper.emitted("update:modelValue")![0]).toEqual([""]);
    });

    it("emits input event when clearing", async () => {
      const wrapper = mountComponent({ modelValue: "test" });
      const clearBtn = wrapper.find(".clear-input-btn");

      await clearBtn.trigger("click");

      expect(wrapper.emitted("input")).toBeTruthy();
    });
  });

  describe("External Value Sync", () => {
    it("updates local input when modelValue prop changes", async () => {
      const wrapper = mountComponent({ modelValue: "initial" });
      const input = wrapper.find("input");

      expect((input.element as HTMLInputElement).value).toBe("initial");

      await wrapper.setProps({ modelValue: "updated" });
      expect((input.element as HTMLInputElement).value).toBe("updated");
    });
  });
});

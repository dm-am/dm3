/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach } from "vitest";
import { mount, type VueWrapper } from "@vue/test-utils";
import SidebarBlock from "./SidebarBlock.vue";

const KEY = "__HideMenuModule_TestBlock__";

function render() {
  return mount(SidebarBlock, {
    props: { token: "TestBlock", title: "Опросы" },
    slots: { default: "<li class='test-content'>Content</li>" },
  });
}

const isOpen = (wrapper: VueWrapper) =>
  wrapper.find(".expand-fold").classes().includes("open");

describe("SidebarBlock", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it("heads its rows with the title", () => {
    const wrapper = render();

    expect(wrapper.find(".sidebar-title").text()).toBe("Опросы");
    expect(wrapper.find(".test-content").exists()).toBe(true);
  });

  it("starts open", () => {
    const wrapper = render();

    expect(isOpen(wrapper)).toBe(true);
    expect(wrapper.find(".toggle").attributes("aria-expanded")).toBe("true");
  });

  it("folds on a click and keeps the choice under the block's token", async () => {
    const wrapper = render();

    await wrapper.find(".toggle").trigger("click");

    expect(isOpen(wrapper)).toBe(false);
    expect(wrapper.find(".toggle").attributes("aria-expanded")).toBe("false");
    expect(localStorage.getItem(KEY)).toBe("false");
  });

  it("starts folded after a fold, and opens on a click", async () => {
    localStorage.setItem(KEY, "false");
    const wrapper = render();
    expect(isOpen(wrapper)).toBe(false);

    await wrapper.find(".toggle").trigger("click");

    expect(isOpen(wrapper)).toBe(true);
    expect(localStorage.getItem(KEY)).toBe("true");
  });

  // An inline opacity outranks the hover and focus rules of the stylesheet.
  it("leaves the visibility of the +/- to the stylesheet", () => {
    const icon = render().get(".icon").element as HTMLElement;

    expect(icon.style.opacity).toBe("");
  });
});

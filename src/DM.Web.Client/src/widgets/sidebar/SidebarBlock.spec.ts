/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, config } from "@vue/test-utils";
import { nextTick } from "vue";
import SidebarBlock from "./SidebarBlock.vue";

// Mock localStorage
const localStorageMock = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: vi.fn((key: string) => store[key] || null),
    setItem: vi.fn((key: string, value: string) => {
      store[key] = value;
    }),
    removeItem: vi.fn((key: string) => {
      delete store[key];
    }),
    clear: vi.fn(() => {
      store = {};
    }),
  };
})();

Object.defineProperty(window, "localStorage", { value: localStorageMock });

describe("SidebarBlock", () => {
  beforeEach(() => {
    localStorageMock.clear();
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  // ============================================================================
  // BASIC RENDERING
  // ============================================================================

  describe("Basic Rendering", () => {
    it("renders without errors", () => {
      const wrapper = mount(SidebarBlock, {
        props: { token: "TestBlock" },
        slots: { title: "Test Title", default: "Test Content" },
      });
      expect(wrapper.exists()).toBe(true);
    });

    it("renders title slot content", () => {
      const wrapper = mount(SidebarBlock, {
        props: { token: "TestBlock" },
        slots: { title: "Block Title" },
      });
      expect(wrapper.find(".sidebar-title").text()).toContain("Block Title");
    });

    it("renders default slot content", () => {
      const wrapper = mount(SidebarBlock, {
        props: { token: "TestBlock" },
        slots: { default: "<div class='test-content'>Content</div>" },
      });
      expect(wrapper.find(".test-content").exists()).toBe(true);
    });

    it("has toggle element with cursor pointer", () => {
      const wrapper = mount(SidebarBlock, {
        props: { token: "TestBlock" },
        slots: { title: "Title" },
      });
      expect(wrapper.find(".toggle").exists()).toBe(true);
    });

    it("has icon element", () => {
      const wrapper = mount(SidebarBlock, {
        props: { token: "TestBlock" },
        slots: { title: "Title" },
      });
      expect(wrapper.find(".icon").exists()).toBe(true);
    });
  });

  // ============================================================================
  // COLLAPSE/EXPAND
  // ============================================================================

  describe("Collapse/Expand", () => {
    it("starts expanded by default", () => {
      const wrapper = mount(SidebarBlock, {
        props: { token: "TestBlock" },
        slots: { default: "Content" },
      });
      const list = wrapper.find(".list");
      expect(list.classes()).not.toContain("collapsed");
    });

    it("collapses when toggle is clicked", async () => {
      const wrapper = mount(SidebarBlock, {
        props: { token: "TestBlock" },
        slots: { default: "Content" },
      });

      await wrapper.find(".toggle").trigger("click");
      await nextTick();

      const list = wrapper.find(".list");
      expect(list.classes()).toContain("collapsed");
    });

    it("expands when toggle is clicked on collapsed block", async () => {
      // Pre-set collapsed state in localStorage
      localStorageMock.getItem.mockReturnValue("false");

      const wrapper = mount(SidebarBlock, {
        props: { token: "TestBlock" },
        slots: { default: "Content" },
      });

      // Click to expand
      await wrapper.find(".toggle").trigger("click");
      await nextTick();

      const list = wrapper.find(".list");
      expect(list.classes()).not.toContain("collapsed");
    });

    it("rotates icon on collapse", async () => {
      const wrapper = mount(SidebarBlock, {
        props: { token: "TestBlock" },
        slots: { title: "Title" },
      });

      const icon = wrapper.find(".icon");
      // Initially rotated 45deg (expanded)
      expect(icon.attributes("style")).toContain("rotate(45deg)");

      await wrapper.find(".toggle").trigger("click");
      await nextTick();

      // After collapse, rotation should be 0
      expect(icon.attributes("style")).toContain("rotate(0deg)");
    });
  });

  // ============================================================================
  // LOCALSTORAGE PERSISTENCE
  // ============================================================================

  describe("LocalStorage Persistence", () => {
    it("uses correct localStorage key format", async () => {
      const wrapper = mount(SidebarBlock, {
        props: { token: "MyBlock" },
        slots: { default: "Content" },
      });

      await wrapper.find(".toggle").trigger("click");

      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        "__HideMenuModule_MyBlock__",
        expect.any(String),
      );
    });

    it("saves collapsed state to localStorage", async () => {
      const wrapper = mount(SidebarBlock, {
        props: { token: "TestBlock" },
        slots: { default: "Content" },
      });

      await wrapper.find(".toggle").trigger("click");

      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        "__HideMenuModule_TestBlock__",
        "false",
      );
    });

    it("saves expanded state to localStorage", async () => {
      localStorageMock.getItem.mockReturnValue("false");

      const wrapper = mount(SidebarBlock, {
        props: { token: "TestBlock" },
        slots: { default: "Content" },
      });

      await wrapper.find(".toggle").trigger("click");
      await nextTick();

      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        "__HideMenuModule_TestBlock__",
        "true",
      );
    });

    it("reads initial state from localStorage", () => {
      localStorageMock.getItem.mockReturnValue("false");

      const wrapper = mount(SidebarBlock, {
        props: { token: "TestBlock" },
        slots: { default: "Content" },
      });

      const list = wrapper.find(".list");
      expect(list.classes()).toContain("collapsed");
    });
  });

  // ============================================================================
  // HOVER STATE
  // ============================================================================

  describe("Hover State", () => {
    it("shows icon on hover", async () => {
      const wrapper = mount(SidebarBlock, {
        props: { token: "TestBlock" },
        slots: { title: "Title" },
      });

      const icon = wrapper.find(".icon");
      // Initially hidden (opacity 0)
      expect(icon.attributes("style")).toContain("opacity: 0");

      // Hover target is the whole title row (only the icon button toggles).
      await wrapper.find(".sidebar-title").trigger("mouseenter");
      await nextTick();

      // After hover, should be visible (opacity 1)
      expect(icon.attributes("style")).toContain("opacity: 1");
    });

    it("hides icon on mouse leave", async () => {
      const wrapper = mount(SidebarBlock, {
        props: { token: "TestBlock" },
        slots: { title: "Title" },
      });

      await wrapper.find(".sidebar-title").trigger("mouseenter");
      await nextTick();

      await wrapper.find(".sidebar-title").trigger("mouseleave");
      await nextTick();

      const icon = wrapper.find(".icon");
      expect(icon.attributes("style")).toContain("opacity: 0");
    });
  });

  // ============================================================================
  // DIFFERENT TOKENS
  // ============================================================================

  describe("Different Tokens", () => {
    it("uses unique localStorage keys for different tokens", async () => {
      const wrapper1 = mount(SidebarBlock, {
        props: { token: "Block1" },
        slots: { default: "Content" },
      });

      const wrapper2 = mount(SidebarBlock, {
        props: { token: "Block2" },
        slots: { default: "Content" },
      });

      await wrapper1.find(".toggle").trigger("click");
      await wrapper2.find(".toggle").trigger("click");

      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        "__HideMenuModule_Block1__",
        expect.any(String),
      );
      expect(localStorageMock.setItem).toHaveBeenCalledWith(
        "__HideMenuModule_Block2__",
        expect.any(String),
      );
    });
  });
});

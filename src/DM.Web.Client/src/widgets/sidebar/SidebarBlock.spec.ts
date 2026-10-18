/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount } from "@vue/test-utils";
import { nextTick } from "vue";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  componentCss,
  outranks,
  specificity,
} from "@/shared/lib/testing/styles";
import SidebarBlock from "./SidebarBlock.vue";

const HERE = dirname(fileURLToPath(import.meta.url));

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
    // Since vitest 4, restoreAllMocks touches only vi.spyOn spies, so a
    // mockReturnValue left on the vi.fn-based localStorage mock would leak
    // into the next test. resetAllMocks puts the original implementations
    // back.
    vi.resetAllMocks();
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
      const fold = wrapper.find(".expand-fold");
      expect(fold.classes()).toContain("open");
    });

    it("collapses when toggle is clicked", async () => {
      const wrapper = mount(SidebarBlock, {
        props: { token: "TestBlock" },
        slots: { default: "Content" },
      });

      await wrapper.find(".toggle").trigger("click");
      await nextTick();

      const fold = wrapper.find(".expand-fold");
      expect(fold.classes()).not.toContain("open");
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

      const fold = wrapper.find(".expand-fold");
      expect(fold.classes()).toContain("open");
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

      const fold = wrapper.find(".expand-fold");
      expect(fold.classes()).not.toContain("open");
    });
  });

  // ============================================================================
  // THE +/- SIGN
  // ============================================================================

  /**
   * When the sign shows, on a screen with a mouse and on a touch screen.
   *
   * It was an inline opacity bound to the hover of the title row, and an
   * inline value outranks every rule of a stylesheet: the rule that keeps the
   * sign faintly visible on a touch screen never applied, and a phone showed
   * no sign at all. So the answer is read the way the browser reaches it: the
   * inline style of the mounted sign first, then the compiled stylesheet,
   * the rules of the media the screen matches, by weight and then by order.
   */
  describe("The +/- sign", () => {
    const MOUSE = new Set(["(hover: hover)", "(pointer: fine)"]);
    const TOUCH = new Set(["(hover: none)", "(pointer: coarse)"]);

    /** The selectors that reach the sign in each state of the title row. */
    const reaching = (state: { hovered?: boolean; focused?: boolean }) =>
      new Set([
        ".icon",
        ".toggle .icon",
        ...(state.hovered
          ? [".sidebar-title:hover .icon", ".toggle:hover .icon"]
          : []),
        ...(state.focused ? [".toggle:focus-visible .icon"] : []),
      ]);

    interface Rule {
      media: string | null;
      selectors: string[];
      opacity?: string;
    }

    /** The flat compiled sheet, rule by rule, each with the @media over it. */
    function sheet(): Rule[] {
      const css = componentCss(join(HERE, "SidebarBlock.vue"));
      const rules: Rule[] = [];
      let media: string | null = null;
      let at = 0;
      while (at < css.length) {
        const open = css.indexOf("{", at);
        const shut = css.indexOf("}", at);
        if (shut < 0) break;
        // The brace that closes an @media block.
        if (open < 0 || shut < open) {
          media = null;
          at = shut + 1;
          continue;
        }
        const head = css.slice(at, open).trim();
        if (head.startsWith("@media")) {
          media = head.slice("@media".length).trim();
          at = open + 1;
          continue;
        }
        const body = css.slice(open + 1, shut);
        const opacity = /(?:^|;)\s*opacity:\s*([^;]+)/.exec(body)?.[1].trim();
        rules.push({
          media,
          selectors: head.split(",").map((one) => one.trim()),
          opacity,
        });
        at = shut + 1;
      }
      return rules;
    }

    /**
     * The opacity the browser gives the sign: an !important rule first, then
     * the inline style, then the rule of most weight, the later of two equal.
     */
    function opacityOf(
      icon: HTMLElement,
      screen: Set<string>,
      state: { hovered?: boolean; focused?: boolean } = {},
    ): number {
      const reach = reaching(state);
      type Best = { weight: [number, number, number]; value: string } | null;
      let normal: Best = null;
      let important: Best = null;
      for (const rule of sheet()) {
        if (rule.opacity === undefined) continue;
        if (rule.media !== null && !screen.has(rule.media)) continue;
        const isImportant = rule.opacity.includes("!important");
        const value = rule.opacity.replace("!important", "").trim();
        for (const selector of rule.selectors) {
          if (!reach.has(selector)) continue;
          const weight = specificity(selector);
          const best = isImportant ? important : normal;
          if (best && outranks(best.weight, weight)) continue;
          if (isImportant) important = { weight, value };
          else normal = { weight, value };
        }
      }
      if (important) return Number(important.value);
      if (icon.style.opacity !== "") return Number(icon.style.opacity);
      return Number(normal?.value ?? 1);
    }

    function mountSign() {
      const wrapper = mount(SidebarBlock, {
        props: { token: "TestBlock" },
        slots: { title: "Title" },
      });
      return { wrapper, icon: wrapper.get(".icon").element as HTMLElement };
    }

    it("stays visible on a touch screen, which has no hover", () => {
      const { icon } = mountSign();
      expect(opacityOf(icon, TOUCH)).toBeGreaterThan(0);
    });

    it("stays visible on a touch screen after a tap on the row", async () => {
      // A browser of a phone fires mouseenter on a tap; the sign must not go.
      const { wrapper, icon } = mountSign();
      await wrapper.find(".sidebar-title").trigger("mouseenter");
      await wrapper.find(".sidebar-title").trigger("mouseleave");
      await nextTick();
      expect(opacityOf(icon, TOUCH)).toBeGreaterThan(0);
    });

    it("is hidden under a mouse until the pointer is over the title row", async () => {
      const { wrapper, icon } = mountSign();
      expect(opacityOf(icon, MOUSE)).toBe(0);

      await wrapper.find(".sidebar-title").trigger("mouseenter");
      await nextTick();
      expect(opacityOf(icon, MOUSE, { hovered: true })).toBe(1);
    });

    it("shows on keyboard focus of the toggle, as under the pointer", () => {
      const { icon } = mountSign();
      expect(opacityOf(icon, MOUSE, { focused: true })).toBe(1);
      expect(opacityOf(icon, TOUCH, { focused: true })).toBe(1);
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

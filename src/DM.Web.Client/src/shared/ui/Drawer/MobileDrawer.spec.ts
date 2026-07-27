/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, config } from "@vue/test-utils";
import { nextTick } from "vue";
import MobileDrawer from "./MobileDrawer.vue";

// Stub Teleport to render content in place (not to body) — same convention
// as InputDialog.spec.ts.
config.global.stubs = {
  Teleport: true,
};

describe("MobileDrawer", () => {
  // The component locks scroll on `.main` (the app's real scroll container,
  // see App.vue) — provide one in the document for every test.
  let mainEl: HTMLDivElement;

  beforeEach(() => {
    mainEl = document.createElement("div");
    mainEl.className = "main";
    mainEl.style.overflow = "scroll";
    document.body.appendChild(mainEl);
  });

  afterEach(() => {
    document.body.removeChild(mainEl);
    vi.restoreAllMocks();
  });

  // ============================================================================
  // BASIC RENDERING
  // ============================================================================

  describe("Basic Rendering", () => {
    it("renders when modelValue is true", () => {
      const wrapper = mount(MobileDrawer, {
        props: { modelValue: true },
      });
      expect(wrapper.find(".drawer-scrim").exists()).toBe(true);
      expect(wrapper.find(".drawer-panel").exists()).toBe(true);
    });

    it("does not render when modelValue is false", () => {
      const wrapper = mount(MobileDrawer, {
        props: { modelValue: false },
      });
      expect(wrapper.find(".drawer-scrim").exists()).toBe(false);
    });

    it("renders default slot content", () => {
      const wrapper = mount(MobileDrawer, {
        props: { modelValue: true },
        slots: { default: "<div class='drawer-test-content'>Nav</div>" },
      });
      expect(wrapper.find(".drawer-test-content").exists()).toBe(true);
    });
  });

  // ============================================================================
  // ACCESSIBILITY
  // ============================================================================

  describe("Accessibility", () => {
    it('panel has role="dialog" and aria-modal="true"', () => {
      const wrapper = mount(MobileDrawer, {
        props: { modelValue: true },
      });
      const panel = wrapper.find(".drawer-panel");
      expect(panel.attributes("role")).toBe("dialog");
      expect(panel.attributes("aria-modal")).toBe("true");
    });

    it("uses the default aria-label", () => {
      const wrapper = mount(MobileDrawer, {
        props: { modelValue: true },
      });
      expect(wrapper.find(".drawer-panel").attributes("aria-label")).toBe(
        "Меню навигации",
      );
    });

    it("accepts a custom aria-label", () => {
      const wrapper = mount(MobileDrawer, {
        props: { modelValue: true, ariaLabel: "Custom label" },
      });
      expect(wrapper.find(".drawer-panel").attributes("aria-label")).toBe(
        "Custom label",
      );
    });

    it("close button has aria-label", () => {
      const wrapper = mount(MobileDrawer, {
        props: { modelValue: true },
      });
      expect(wrapper.find(".drawer-close").attributes("aria-label")).toBe(
        "Закрыть меню",
      );
    });
  });

  // ============================================================================
  // CLOSE INTERACTIONS
  // ============================================================================

  describe("Close interactions", () => {
    it("emits update:modelValue(false) when the close button is clicked", async () => {
      const wrapper = mount(MobileDrawer, {
        props: { modelValue: true },
      });

      await wrapper.find(".drawer-close").trigger("click");

      expect(wrapper.emitted("update:modelValue")).toBeTruthy();
      expect(wrapper.emitted("update:modelValue")![0]).toEqual([false]);
    });

    it("emits update:modelValue(false) on scrim click", async () => {
      const wrapper = mount(MobileDrawer, {
        props: { modelValue: true },
      });

      await wrapper.find(".drawer-scrim").trigger("click");

      expect(wrapper.emitted("update:modelValue")).toBeTruthy();
      expect(wrapper.emitted("update:modelValue")![0]).toEqual([false]);
    });

    it("does not close when clicking inside the panel", async () => {
      const wrapper = mount(MobileDrawer, {
        props: { modelValue: true },
      });

      await wrapper.find(".drawer-panel").trigger("click");

      expect(wrapper.emitted("update:modelValue")).toBeFalsy();
    });

    it("emits update:modelValue(false) on Escape key", async () => {
      const wrapper = mount(MobileDrawer, {
        props: { modelValue: true },
      });

      await wrapper.find(".drawer-scrim").trigger("keydown", {
        key: "Escape",
      });

      expect(wrapper.emitted("update:modelValue")).toBeTruthy();
      expect(wrapper.emitted("update:modelValue")![0]).toEqual([false]);
    });
  });

  // ============================================================================
  // BODY / MAIN SCROLL LOCK
  // ============================================================================

  describe("Scroll lock", () => {
    it("locks .main scroll while open", async () => {
      mount(MobileDrawer, {
        props: { modelValue: true },
      });
      await nextTick();

      expect(mainEl.style.overflow).toBe("hidden");
    });

    it("restores the previous .main overflow when closed", async () => {
      const wrapper = mount(MobileDrawer, {
        props: { modelValue: true },
      });
      await nextTick();
      expect(mainEl.style.overflow).toBe("hidden");

      await wrapper.setProps({ modelValue: false });
      await nextTick();

      expect(mainEl.style.overflow).toBe("scroll");
    });

    it("does not lock scroll while closed from the start", () => {
      mount(MobileDrawer, {
        props: { modelValue: false },
      });

      expect(mainEl.style.overflow).toBe("scroll");
    });
  });
});

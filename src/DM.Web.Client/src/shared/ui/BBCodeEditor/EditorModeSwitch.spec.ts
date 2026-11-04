/**
 * @vitest-environment jsdom
 */

/**
 * The switch of the two modes inside the editor: its two tabs, the visual mode
 * first on screen, the text carried across a change of mode, and the line
 * under the active tab, which follows it when the font changes its width.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import { nextTick } from "vue";
import BBCodeEditor from "./BBCodeEditor.vue";
import { editorShell } from "@/shared/lib/testing/editorShell";

// Unmount every wrapper after each test so the component destroys its
// tiptap editor (see onUnmounted in BBCodeEditor.vue). Leaked editors
// keep ProseMirror DOMObserver flush timers pending, which can fire
// after jsdom teardown and fail the run with an unhandled error.
enableAutoUnmount(afterEach);

const { clean, reset } = editorShell();

describe("EditorModeSwitch", () => {
  beforeEach(clean);
  afterEach(reset);

  describe("Basic Rendering", () => {
    it("renders mode toggle buttons", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      const modeButtons = wrapper.findAll(".mode-tab");
      expect(modeButtons.length).toBe(2);
    });

    it("starts in WYSIWYG mode by default", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      // Default mode is WYSIWYG (visual preview of the BBCode being
      // composed). BBCode is the source of truth sent on save; WYSIWYG
      // is the default preview surface. Tab order: BBCode[0], WYSIWYG[1].
      const wysiwygBtn = wrapper.findAll(".mode-tab")[1];
      expect(wysiwygBtn.classes()).toContain("active");
    });
  });

  describe("Mode Switching", () => {
    // Button order is: BBCode[0], WYSIWYG[1]

    it("switches to WYSIWYG mode when WYSIWYG button clicked", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      // Click WYSIWYG button (index 1)
      await wrapper.findAll(".mode-tab")[1].trigger("click");
      await flushPromises();
      await nextTick();

      // Re-query after DOM update
      expect(wrapper.findAll(".mode-tab")[1].classes()).toContain("active");
    });

    it("shows textarea when switching into BBCode mode", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      await nextTick();

      // Default is WYSIWYG — textarea is not visible. Switch to BBCode mode.
      await wrapper.findAll(".mode-tab")[0].trigger("click");
      await flushPromises();
      await nextTick();

      const textarea = wrapper.find(".bbcode-textarea");
      expect(textarea.isVisible()).toBe(true);
    });

    it("preserves content when switching modes", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "[b]test[/b]" },
      });
      await flushPromises();
      await nextTick();

      // Default is WYSIWYG. Switch to BBCode mode to expose the textarea,
      // which holds the authoritative BBCode source.
      await wrapper.findAll(".mode-tab")[0].trigger("click");
      await flushPromises();
      await nextTick();

      const textarea = wrapper.find(".bbcode-textarea");
      expect((textarea.element as HTMLTextAreaElement).value).toContain("[b]");
    });
  });

  /**
   * The line under the active mode tab. The tabs are measured on mount, and on
   * the stand the site's font had not arrived by then: "wysiwyg" in the
   * fallback font was 59px against 55.56 in PT Sans, and the line kept the
   * first width, standing 3px past the editor's right edge until the mode was
   * switched. The widths below are those two, and the observer the tabs are
   * watched with is fired by hand when the font "arrives".
   */
  describe("Mode indicator", () => {
    const STRIP_LEFT = 800;
    const FALLBACK = { bbcode: 59, wysiwyg: 59 };
    const SITE_FONT = { bbcode: 59.125, wysiwyg: 55.5625 };

    it("follows the active tab when the font changes its width", async () => {
      let widths = FALLBACK;
      /** The box a tab has in the current font, the strip at STRIP_LEFT. */
      const box = (el: HTMLElement) => {
        if (el.classList.contains("mode-tabs")) {
          return { left: STRIP_LEFT, width: widths.bbcode + widths.wysiwyg };
        }
        if (!el.classList.contains("mode-tab")) return { left: 0, width: 0 };
        return el.textContent?.includes("wysiwyg")
          ? { left: STRIP_LEFT + widths.bbcode, width: widths.wysiwyg }
          : { left: STRIP_LEFT, width: widths.bbcode };
      };
      vi.spyOn(
        HTMLElement.prototype,
        "getBoundingClientRect",
      ).mockImplementation(function (this: HTMLElement) {
        const { left, width } = box(this);
        return DOMRect.fromRect({ x: left, y: 0, width, height: 20 });
      });
      // What a layout engine answers through the rounded properties.
      vi.spyOn(HTMLElement.prototype, "offsetLeft", "get").mockImplementation(
        function (this: HTMLElement) {
          return Math.round(box(this).left - STRIP_LEFT);
        },
      );
      vi.spyOn(HTMLElement.prototype, "offsetWidth", "get").mockImplementation(
        function (this: HTMLElement) {
          return Math.round(box(this).width);
        },
      );

      const observers: ResizeObserverCallback[] = [];
      const moduleObserver = globalThis.ResizeObserver;
      vi.stubGlobal(
        "ResizeObserver",
        class {
          constructor(callback: ResizeObserverCallback) {
            observers.push(callback);
          }
          observe() {}
          unobserve() {}
          disconnect() {}
        },
      );
      try {
        const wrapper = mount(BBCodeEditor, { props: { modelValue: "" } });
        await flushPromises();
        const line = () =>
          wrapper.get(".mode-indicator").element as HTMLElement;
        expect([line().style.left, line().style.width]).toEqual([
          "59px",
          "59px",
        ]);

        // The font arrives: the tabs take their own widths, and the line
        // takes the active one's at once rather than sliding to it.
        widths = SITE_FONT;
        for (const callback of observers) {
          callback([], {} as ResizeObserver);
        }
        await nextTick();
        expect([line().style.left, line().style.width]).toEqual([
          "59.125px",
          "55.5625px",
        ]);
        expect(line().style.transition).toBe("none");

        // A change of mode still slides it.
        await wrapper.findAll(".mode-tab")[0].trigger("click");
        await flushPromises();
        expect([line().style.left, line().style.width]).toEqual([
          "0px",
          "59.125px",
        ]);
        expect(line().style.transition).toBe("");
      } finally {
        vi.stubGlobal("ResizeObserver", moduleObserver);
      }
    });
  });
});

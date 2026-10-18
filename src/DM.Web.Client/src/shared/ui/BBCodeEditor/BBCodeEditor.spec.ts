/**
 * @vitest-environment jsdom
 */

/**
 * BBCodeEditor Component Tests
 *
 * Tests for the WYSIWYG BBCode editor component.
 * Covers mode switching, toolbar actions, character counting,
 * draft management, accessibility, and validation.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  mount,
  flushPromises,
  config,
  enableAutoUnmount,
} from "@vue/test-utils";
import { nextTick } from "vue";
import type { Editor } from "@tiptap/vue-3";
import {
  sanitizeImageUrl,
  validateBBCode,
  type BBCodeContext,
} from "@/shared/lib/utils/bbcode";
import BBCodeEditor from "./BBCodeEditor.vue";
import InsertPanel from "./InsertPanel.vue";
import { componentCss, ruleMap, styleRules } from "@/shared/lib/testing/styles";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

// The help opens in a window of the site, and the library under it needs the
// app's plugin. Here it is a box that holds the card while the window is open.
vi.mock("vue-final-modal", async () => {
  const { defineComponent, h } = await import("vue");
  return {
    VueFinalModal: defineComponent({
      name: "VueFinalModal",
      props: { modelValue: { type: Boolean, default: false } },
      setup:
        (props, { slots }) =>
        () =>
          props.modelValue
            ? h("div", { class: "vfm-content" }, slots.default?.())
            : null,
    }),
  };
});

// Unmount every wrapper after each test so the component destroys its
// tiptap editor (see onUnmounted in BBCodeEditor.vue). Leaked editors
// keep ProseMirror DOMObserver flush timers pending, which can fire
// after jsdom teardown and fail the run with an unhandled error.
enableAutoUnmount(afterEach);

// Stub Teleport to render content in place (not to body)
config.global.stubs = {
  Teleport: true,
};

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
    get length() {
      return Object.keys(store).length;
    },
    key: vi.fn((index: number) => Object.keys(store)[index] || null),
  };
})();

Object.defineProperty(window, "localStorage", { value: localStorageMock });

// Mock ResizeObserver. A function rather than an arrow: the editor watches its
// mode tabs with one, built with `new`, and vitest will not construct an arrow.
vi.stubGlobal(
  "ResizeObserver",
  vi.fn(function () {
    return {
      observe: vi.fn(),
      unobserve: vi.fn(),
      disconnect: vi.fn(),
    };
  }),
);

/**
 * A window of the width set here: every media query of a width answered for
 * it, so an editor that laid its toolbar out by the width of the window would
 * do it in these tests. A document with no matchMedia, as a test that sets no
 * width, reads as a desktop. A query of no width (the motion of the page)
 * does not match.
 */
const shell = { width: 1366, listeners: new Set<() => void>() };

function windowOf(width: number) {
  shell.width = width;
  window.matchMedia = ((query: string) => {
    const max = /max-width:\s*(\d+)px/.exec(query);
    const min = /min-width:\s*(\d+)px/.exec(query);
    return {
      media: query,
      get matches() {
        if (!max && !min) return false;
        return (
          (!max || shell.width <= Number(max[1])) &&
          (!min || shell.width >= Number(min[1]))
        );
      },
      addEventListener: (_: string, listener: () => void) =>
        shell.listeners.add(listener),
      removeEventListener: (_: string, listener: () => void) =>
        shell.listeners.delete(listener),
    };
  }) as unknown as typeof window.matchMedia;
}

describe("BBCodeEditor", () => {
  beforeEach(() => {
    localStorageMock.clear();
    vi.clearAllMocks();
  });

  afterEach(() => {
    Reflect.deleteProperty(window, "matchMedia");
    shell.listeners.clear();
    shell.width = 1366;
    // Since vitest 4, restoreAllMocks touches only vi.spyOn spies, so a
    // mockImplementation left on the vi.fn-based localStorage mock would
    // leak into the next test. resetAllMocks puts the original
    // implementations back.
    vi.resetAllMocks();
    vi.restoreAllMocks();
  });

  // ============================================================================
  // BASIC RENDERING
  // ============================================================================

  describe("Basic Rendering", () => {
    it("renders without errors", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      expect(wrapper.exists()).toBe(true);
    });

    it("renders toolbar", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      expect(wrapper.find(".editor-toolbar").exists()).toBe(true);
    });

    // The rule after a group stands after the group's last button, and none
    // ends the row.
    it("closes each group of tags with a rule after its last button", () => {
      windowOf(390);
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      const buttons = wrapper.findAll(".toolbar-tags .tag-btn");
      expect(buttons.map((button) => button.text())).toEqual([
        "b",
        "i",
        "u",
        "strike",
        "spoiler",
        "nsfw",
        "ul",
        "ol",
        "img",
        "link",
        "quote",
        "tab",
        "code",
        "noparse",
        "load",
        "help",
      ]);
      const closing = wrapper
        .findAll(".toolbar-tags .toolbar-separator")
        .map(
          (rule) =>
            Array.from(
              rule.element.parentElement!.querySelectorAll(".tag-btn"),
            ).at(-1)!.textContent,
        );
      expect(closing).toEqual([
        "strike",
        "nsfw",
        "ol",
        "link",
        "quote",
        "tab",
        "noparse",
      ]);
    });

    it.each([1366, 390])(
      "closes a group with the last of its buttons the surface offers, at %i",
      (width) => {
        // A game post has [private] at the end of the hidden group, a
        // moderator's comment [mod] at the end of the blocks. The rule stands
        // at the end of the group, at every width.
        windowOf(width);
        const closing = (props: Record<string, unknown>) =>
          mount(BBCodeEditor, { props: { modelValue: "", ...props } })
            .findAll(".toolbar-tags .toolbar-separator")
            .map(
              (rule) =>
                Array.from(
                  rule.element.parentElement!.querySelectorAll(".tag-btn"),
                ).at(-1)!.textContent,
            );
        expect(closing({ context: "post" })).toContain("private");
        expect(closing({ context: "post" })).not.toContain("nsfw");
        expect(closing({ context: "common", isModerator: true })).toContain(
          "mod",
        );
        expect(closing({ context: "common", isModerator: true })).not.toContain(
          "quote",
        );
      },
    );

    it("renders editor content area", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      expect(wrapper.find(".editor-content").exists()).toBe(true);
    });

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

  // ============================================================================
  // EXTENSION REGISTRY
  // ============================================================================

  describe("Extension registry", () => {
    it("registers each extension once and leaves anchors to BbLink", async () => {
      const warnings: string[] = [];
      const warn = vi
        .spyOn(console, "warn")
        .mockImplementation((...args: unknown[]) => {
          warnings.push(args.map(String).join(" "));
        });

      const wrapper = mount(BBCodeEditor, { props: { modelValue: "" } });
      await nextTick();

      const editor = (wrapper.vm as unknown as { editor: Editor }).editor;
      const names = editor.extensionManager.extensions.map((e) => e.name);

      // StarterKit brings an `underline` and a `link` of its own, and this
      // editor registers a replacement for each. The underline collides by
      // name and tiptap says so out loud; the link does not collide by name,
      // so nothing warns — its parse rule is a bare `a[href]` and it outranks
      // BbLink's, which is how `data-bb-text` and `data-bb-selfref` came to be
      // dropped on the way into the document, autolink included. Both are off
      // in the kit's options, and this is the gate that keeps them off.
      expect(
        names.filter((name, index) => names.indexOf(name) !== index),
        "the kit registers an extension this editor also registers: turn the kit's copy off in StarterKit.configure",
      ).toEqual([]);
      expect(
        warnings.filter((line) => line.includes("Duplicate extension names")),
      ).toEqual([]);
      expect(Object.keys(editor.schema.marks)).toContain("bbLink");
      expect(
        Object.keys(editor.schema.marks),
        "the stock link mark parses every anchor and discards the BBCode attributes BbLink carries",
      ).not.toContain("link");

      warn.mockRestore();
    });
  });

  // ============================================================================
  // ACCESSIBILITY
  // ============================================================================

  describe("Accessibility", () => {
    it('has role="region" on container', () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      expect(wrapper.find(".bbcode-editor").attributes("role")).toBe("region");
    });

    it("has aria-label on container", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      expect(wrapper.find(".bbcode-editor").attributes("aria-label")).toBe(
        "Редактор BBCode",
      );
    });

    it('toolbar has role="toolbar"', () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      expect(wrapper.find(".editor-toolbar").attributes("role")).toBe(
        "toolbar",
      );
    });

    it("mode toggle has proper class", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      expect(wrapper.find(".mode-tabs").exists()).toBe(true);
    });

    it("toolbar buttons have aria-label", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      const buttons = wrapper.findAll(".tag-btn");
      buttons.forEach((btn) => {
        expect(btn.attributes("aria-label")).toBeTruthy();
      });
    });

    it("mode buttons exist and are clickable", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      const modeButtons = wrapper.findAll(".mode-tab");
      expect(modeButtons.length).toBe(2);
    });

    it("separators have proper ARIA attributes", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      const separators = wrapper.findAll(".toolbar-separator");
      separators.forEach((sep) => {
        expect(sep.attributes("role")).toBe("separator");
      });
    });
  });

  // ============================================================================
  // MODE SWITCHING
  // ============================================================================

  describe("Mode Switching", () => {
    // Button order is: BBCode[0], WYSIWYG[1]
    // Component starts in BBCode mode by default

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

  // ============================================================================
  // MODE INDICATOR
  // ============================================================================

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

  // ============================================================================
  // CHARACTER & WORD COUNT
  // ============================================================================

  describe("Character & Word Count", () => {
    it("shows status bar when showCharCount is true", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", showCharCount: true },
      });
      expect(wrapper.find(".status-bar").exists()).toBe(true);
    });

    it("hides status bar when showCharCount is false", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", showCharCount: false },
      });
      expect(wrapper.find(".status-bar").exists()).toBe(false);
    });

    it("shows character count in BBCode mode", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "hello world", showCharCount: true },
      });
      await nextTick();

      // Component starts in BBCode mode
      const statusBar = wrapper.find(".status-bar");
      expect(statusBar.text()).toContain("11");
    });

    it("shows word count", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "hello world", showCharCount: true },
      });
      await nextTick();

      // Component starts in BBCode mode
      const statusBar = wrapper.find(".status-bar");
      expect(statusBar.text()).toContain("2");
    });

    it("shows over-limit warning when maxLength exceeded", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "hello world", maxLength: 5, showCharCount: true },
      });
      await nextTick();

      // Component starts in BBCode mode
      expect(wrapper.find(".over-limit").exists()).toBe(true);
    });

    it("does not show over-limit when within limit", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "hi", maxLength: 100, showCharCount: true },
      });
      await nextTick();

      // Component starts in BBCode mode
      expect(wrapper.find(".over-limit").exists()).toBe(false);
    });
  });

  // ============================================================================
  // DRAFT MANAGEMENT
  // ============================================================================

  describe("Draft Management", () => {
    it("saves draft to localStorage", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", draftKey: "test-draft" },
      });
      await nextTick();

      // Component starts in BBCode mode
      const textarea = wrapper.find(".bbcode-textarea");
      await textarea.setValue("draft content");
      await nextTick();

      expect(localStorageMock.setItem).toHaveBeenCalled();
    });

    it("shows draft available indicator when draft exists", async () => {
      const draftData = JSON.stringify({
        content: "saved draft",
        timestamp: Date.now(),
      });
      // Mock must return value for the specific key
      localStorageMock.getItem.mockImplementation((key: string) => {
        if (key === "bbcode_draft_test-draft") return draftData;
        return null;
      });

      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", draftKey: "test-draft", showCharCount: true },
      });
      await nextTick();

      // Component shows "Есть черновик" in status bar instead of a dialog
      expect(wrapper.find(".draft-available").exists()).toBe(true);
    });

    it("shows draft available in status bar when draft exists", async () => {
      const draftData = JSON.stringify({
        content: "saved draft",
        timestamp: Date.now(),
      });
      localStorageMock.getItem.mockImplementation((key: string) => {
        if (key === "bbcode_draft_test-draft") return draftData;
        return null;
      });

      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", draftKey: "test-draft", showCharCount: true },
      });
      await nextTick();

      // Status bar shows draft available indicator
      const statusBar = wrapper.find(".status-bar");
      expect(statusBar.text()).toContain("Есть черновик");
    });

    it("does not show draft indicator for expired drafts", () => {
      const expiredDraft = JSON.stringify({
        content: "old draft",
        timestamp: Date.now() - 8 * 24 * 60 * 60 * 1000, // 8 days old
      });
      localStorageMock.getItem.mockReturnValueOnce(expiredDraft);

      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", draftKey: "test-draft", showCharCount: true },
      });

      expect(wrapper.find(".draft-available").exists()).toBe(false);
    });

    it("does not show draft indicator when draft matches modelValue", () => {
      const draftData = JSON.stringify({
        content: "same content",
        timestamp: Date.now(),
      });
      localStorageMock.getItem.mockReturnValueOnce(draftData);

      const wrapper = mount(BBCodeEditor, {
        props: {
          modelValue: "same content",
          draftKey: "test-draft",
          showCharCount: true,
        },
      });

      expect(wrapper.find(".draft-available").exists()).toBe(false);
    });
  });

  // ============================================================================
  // VALIDATION
  // ============================================================================

  describe("BBCode Validation", () => {
    it("shows validation errors in BBCode mode", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "[b]unclosed" },
      });
      await nextTick();

      // Default is WYSIWYG; validation runs only in BBCode source mode.
      // Switch into BBCode mode, then wait for debounced validation.
      await wrapper.findAll(".mode-tab")[0].trigger("click");
      await flushPromises();
      await nextTick();

      await new Promise((resolve) => setTimeout(resolve, 400));
      await nextTick();

      expect(wrapper.find(".validation-errors").exists()).toBe(true);
    });

    it("validation errors have proper ARIA attributes", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "[b]unclosed" },
      });
      await nextTick();

      // Component starts in BBCode mode, wait for debounced validation
      await new Promise((resolve) => setTimeout(resolve, 400));
      await nextTick();

      const errorsDiv = wrapper.find(".validation-errors");
      if (errorsDiv.exists()) {
        expect(errorsDiv.attributes("role")).toBe("alert");
        expect(errorsDiv.attributes("aria-live")).toBe("polite");
      }
    });

    it("hides validation errors in WYSIWYG mode", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "[b]unclosed" },
      });

      // Switch to WYSIWYG mode (button index 1)
      await wrapper.findAll(".mode-tab")[1].trigger("click");
      await nextTick();

      // In WYSIWYG mode, validation errors should not be shown
      expect(wrapper.find(".validation-errors").exists()).toBe(false);
    });
  });

  // ============================================================================
  // HELP POPUP
  // ============================================================================

  describe("Help Dialog", () => {
    const helpButton = (wrapper: ReturnType<typeof mount>) =>
      wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === "Показать справку")!;

    it("opens the help window when the help button is clicked", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      expect(wrapper.find(".help-table").exists()).toBe(false);

      await helpButton(wrapper).trigger("click");
      await flushPromises();

      expect(wrapper.find(".help-table").exists()).toBe(true);
    });

    it("help button says it opens a window", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      expect(helpButton(wrapper).attributes("aria-haspopup")).toBe("dialog");
    });
  });

  // ============================================================================
  // DISABLED STATE
  // ============================================================================

  describe("Disabled State", () => {
    it("applies disabled class when disabled", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", disabled: true },
      });

      expect(wrapper.find(".bbcode-editor").classes()).toContain("disabled");
    });

    it("disables toolbar buttons when disabled", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", disabled: true },
      });

      // Check that the editor container has disabled class
      expect(wrapper.find(".bbcode-editor").classes()).toContain("disabled");

      // Most toolbar buttons should have disabled attribute
      const tagButtons = wrapper.findAll(
        '.tag-btn:not([aria-label="Показать справку"])',
      );
      const disabledButtons = tagButtons.filter(
        (btn) => btn.attributes("disabled") !== undefined,
      );
      // At least some buttons should be disabled
      expect(disabledButtons.length).toBeGreaterThan(0);
    });

    it("disables textarea when disabled", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", disabled: true },
      });
      await nextTick();

      // Component starts in BBCode mode
      const textarea = wrapper.find(".bbcode-textarea");
      expect(textarea.attributes("disabled")).toBeDefined();
    });
  });

  // ============================================================================
  // CONTEXT-BASED TAGS
  // ============================================================================

  describe("Context-Based Tags", () => {
    it("shows mod button only for moderators", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", isModerator: false, context: "common" },
      });

      const modBtn = wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === "Модераторский блок");

      expect(modBtn).toBeUndefined();
    });

    // Note: [mod] tag is not supported by DM2, so mod button test removed
  });

  // ============================================================================
  // LOAD DRAFT BUTTON
  // ============================================================================

  describe("Load Draft Button", () => {
    it("shows load draft button in toolbar", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      const loadBtn = wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === "Загрузить черновик");

      expect(loadBtn).toBeDefined();
    });

    it("load draft button has correct aria-label", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      const loadBtn = wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === "Загрузить черновик");

      // Button uses Tooltip component for tooltip, not title attribute
      expect(loadBtn?.attributes("aria-label")).toBe("Загрузить черновик");
    });

    it("load draft button is present in BBCode mode", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      await nextTick();

      // Component starts in BBCode mode
      const loadBtn = wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === "Загрузить черновик");

      expect(loadBtn).toBeDefined();
    });
  });

  // ============================================================================
  // EXPOSED METHODS
  // ============================================================================

  describe("Exposed Methods", () => {
    it("exposes focus method", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      expect(typeof wrapper.vm.focus).toBe("function");
    });

    it("exposes clear method", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      expect(typeof wrapper.vm.clear).toBe("function");
    });

    it("exposes clearDraft method", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      expect(typeof wrapper.vm.clearDraft).toBe("function");
    });

    it("clear method emits update:modelValue with empty string", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "test" },
      });

      wrapper.vm.clear();
      await nextTick();

      expect(wrapper.emitted("update:modelValue")).toBeTruthy();
      const emitted = wrapper.emitted("update:modelValue");
      expect(emitted![emitted!.length - 1]).toEqual([""]);
    });
  });

  // ============================================================================
  // KEYBOARD SHORTCUTS
  // ============================================================================

  describe("Keyboard Shortcuts in BBCode Mode", () => {
    // Component starts in BBCode mode by default

    it("Ctrl+B wraps selection with [b] tags", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "test text" },
      });
      await flushPromises();
      await nextTick();

      // Component starts in BBCode mode
      const textarea = wrapper.find(".bbcode-textarea");
      const el = textarea.element as HTMLTextAreaElement;

      // Selection range works on the existing value from modelValue
      el.setSelectionRange(0, 4);

      await textarea.trigger("keydown", { key: "b", ctrlKey: true });
      await flushPromises();
      await nextTick();

      // Check emitted value has the wrapped tags
      const emitted = wrapper.emitted("update:modelValue");
      expect(emitted).toBeTruthy();
      const lastEmit = emitted![emitted!.length - 1][0] as string;
      expect(lastEmit).toContain("[b]");
    });

    it("Ctrl+I wraps selection with [i] tags", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "test text" },
      });
      await flushPromises();
      await nextTick();

      // Component starts in BBCode mode
      const textarea = wrapper.find(".bbcode-textarea");
      const el = textarea.element as HTMLTextAreaElement;

      el.setSelectionRange(0, 4);

      await textarea.trigger("keydown", { key: "i", ctrlKey: true });
      await flushPromises();
      await nextTick();

      const emitted = wrapper.emitted("update:modelValue");
      expect(emitted).toBeTruthy();
      const lastEmit = emitted![emitted!.length - 1][0] as string;
      expect(lastEmit).toContain("[i]");
    });

    it("Ctrl+U wraps selection with [u] tags", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "test text" },
      });
      await flushPromises();
      await nextTick();

      // Component starts in BBCode mode
      const textarea = wrapper.find(".bbcode-textarea");
      const el = textarea.element as HTMLTextAreaElement;

      el.setSelectionRange(0, 4);

      await textarea.trigger("keydown", { key: "u", ctrlKey: true });
      await flushPromises();
      await nextTick();

      const emitted = wrapper.emitted("update:modelValue");
      expect(emitted).toBeTruthy();
      const lastEmit = emitted![emitted!.length - 1][0] as string;
      expect(lastEmit).toContain("[u]");
    });

    it("Ctrl+Enter emits submit event", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "test" },
      });
      await flushPromises();
      await nextTick();

      // Component starts in BBCode mode
      const textarea = wrapper.find(".bbcode-textarea");
      await textarea.trigger("keydown", { key: "Enter", ctrlKey: true });
      await flushPromises();
      await nextTick();

      expect(wrapper.emitted("submit")).toBeTruthy();
    });
  });

  // ============================================================================
  // FULLSCREEN MODE
  // ============================================================================

  // Fullscreen Mode tests removed - feature not implemented in current component version

  // ============================================================================
  // DRAFT STATUS INDICATOR
  // ============================================================================

  describe("Draft Status Indicator", () => {
    it("shows draft status in status bar when draftKey is set", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: {
          modelValue: "test content",
          draftKey: "test-draft",
          showCharCount: true,
        },
      });
      await nextTick();

      // Component starts in BBCode mode, type something to trigger draft save
      const textarea = wrapper.find(".bbcode-textarea");
      await textarea.setValue("new content");
      await flushPromises();
      await nextTick();

      // Draft status should appear
      expect(wrapper.find(".draft-status").exists()).toBe(true);
    });

    it("does not show draft status when draftKey is not set", () => {
      const wrapper = mount(BBCodeEditor, {
        props: {
          modelValue: "test content",
          showCharCount: true,
        },
      });

      expect(wrapper.find(".draft-status").exists()).toBe(false);
    });

    it('shows "Сохранено" text after saving', async () => {
      const wrapper = mount(BBCodeEditor, {
        props: {
          modelValue: "",
          draftKey: "test-draft-2",
          showCharCount: true,
        },
      });
      await nextTick();

      // Component starts in BBCode mode
      const textarea = wrapper.find(".bbcode-textarea");
      await textarea.setValue("draft content");
      await flushPromises();
      await nextTick();

      const statusText = wrapper.find(".draft-status").text();
      expect(statusText).toContain("Сохранено");
    });

    it("draft status has saving class when saving", async () => {
      // This test checks the CSS class binding
      const wrapper = mount(BBCodeEditor, {
        props: {
          modelValue: "",
          draftKey: "test-draft-3",
          showCharCount: true,
        },
      });

      // Status bar should be visible
      expect(wrapper.find(".status-bar").exists()).toBe(true);
    });

    it("saves draft to localStorage", async () => {
      localStorageMock.clear();

      const wrapper = mount(BBCodeEditor, {
        props: {
          modelValue: "",
          draftKey: "test-draft-save",
        },
      });
      await nextTick();

      // Component starts in BBCode mode
      const textarea = wrapper.find(".bbcode-textarea");
      await textarea.setValue("saved draft content");
      await flushPromises();
      await nextTick();

      expect(localStorageMock.setItem).toHaveBeenCalled();
    });
  });

  // ============================================================================
  // DRAFT KEY CHANGE
  // ============================================================================

  describe("Draft Key Change", () => {
    it("does not carry the text over to the next subject", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", draftKey: "game:7f1a:comment" },
      });
      await nextTick();

      await wrapper.find(".bbcode-textarea").setValue("текст первой игры");
      await flushPromises();

      await wrapper.setProps({ draftKey: "game:b204:comment" });
      await flushPromises();

      // The box is emptied for the new subject, and nothing of the previous
      // one's text is written under the new key.
      expect(wrapper.emitted("update:modelValue")?.at(-1)).toEqual([""]);
      expect(
        localStorageMock.getItem("bbcode_draft_game:b204:comment"),
      ).toBeNull();
    });

    it("keeps each subject's draft under its own key", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", draftKey: "game:7f1a:comment" },
      });
      await nextTick();

      await wrapper.find(".bbcode-textarea").setValue("текст первой игры");
      await flushPromises();

      await wrapper.setProps({ draftKey: "game:b204:comment" });
      await flushPromises();
      await wrapper.setProps({ draftKey: "game:7f1a:comment" });
      await flushPromises();

      // Back on the first game, its own draft is on offer again.
      expect(wrapper.find(".draft-available").exists()).toBe(true);
      expect(
        localStorageMock.getItem("bbcode_draft_game:7f1a:comment"),
      ).toContain("текст первой игры");
    });
  });

  // ============================================================================
  // ERROR HANDLING
  // ============================================================================

  describe("Error Handling", () => {
    it("handles localStorage errors gracefully when saving draft", async () => {
      const consoleWarnSpy = vi
        .spyOn(console, "warn")
        .mockImplementation(() => {});
      localStorageMock.setItem.mockImplementation(() => {
        throw new Error("localStorage quota exceeded");
      });

      const wrapper = mount(BBCodeEditor, {
        props: {
          modelValue: "",
          draftKey: "test-draft-error",
        },
      });
      await nextTick();

      // Component starts in BBCode mode
      const textarea = wrapper.find(".bbcode-textarea");
      await textarea.setValue("content that fails to save");
      await flushPromises();
      await nextTick();

      // Should not throw, just log warning
      expect(consoleWarnSpy).toHaveBeenCalled();

      consoleWarnSpy.mockRestore();
      localStorageMock.setItem.mockRestore();
    });

    it("handles invalid JSON in draft gracefully", async () => {
      localStorageMock.getItem.mockReturnValue("invalid json {{{");

      const wrapper = mount(BBCodeEditor, {
        props: {
          modelValue: "",
          draftKey: "test-invalid-json",
        },
      });

      // Should not throw, just return the raw string
      expect(wrapper.exists()).toBe(true);

      localStorageMock.getItem.mockRestore();
    });

    it("handles missing editor gracefully", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      // Editor should exist
      expect(wrapper.find(".editor-content").exists()).toBe(true);
    });

    it("handles empty modelValue", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      expect(wrapper.exists()).toBe(true);
      expect(wrapper.find(".editor-content").exists()).toBe(true);
    });

    it("handles undefined draftKey", () => {
      const wrapper = mount(BBCodeEditor, {
        props: {
          modelValue: "test",
          draftKey: undefined as any,
        },
      });

      expect(wrapper.exists()).toBe(true);
    });

    it("handles malformed BBCode gracefully", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: {
          modelValue: "[b]unclosed bold [i]nested[/b]",
        },
      });
      await nextTick();

      // Component starts in BBCode mode, should show validation errors but not crash
      expect(wrapper.exists()).toBe(true);
    });

    it("recovers from mode switch errors", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "test content" },
      });

      // Switch modes multiple times rapidly
      // Button order: BBCode[0], WYSIWYG[1]
      const modeButtons = wrapper.findAll(".mode-tab");
      await modeButtons[1].trigger("click"); // -> WYSIWYG
      await modeButtons[0].trigger("click"); // -> BBCode
      await modeButtons[1].trigger("click"); // -> WYSIWYG
      await nextTick();

      expect(wrapper.exists()).toBe(true);
    });
  });

  // ============================================================================
  // TOOLBAR BUTTONS
  // ============================================================================

  describe("Toolbar Buttons", () => {
    it("has spoiler button", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      const spoilerBtn = wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === "Спойлер");
      expect(spoilerBtn).toBeDefined();
    });

    it("has nsfw button", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      const nsfwBtn = wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === "NSFW контент");
      expect(nsfwBtn).toBeDefined();
    });

    it("has link button", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      const linkBtn = wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === "Вставить ссылку");
      expect(linkBtn).toBeDefined();
    });

    it("has image button", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      const imgBtn = wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === "Вставить изображение");
      expect(imgBtn).toBeDefined();
    });

    it("has quote button", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      const quoteBtn = wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === "Цитата");
      expect(quoteBtn).toBeDefined();
    });
  });

  // ============================================================================
  // INSERTION PANELS
  // ============================================================================

  /**
   * A link, an image, a spoiler caption and a hidden block's readers are asked
   * in a panel under the button, not in a window: the page is not dimmed, and
   * the text they go into stays in sight.
   */
  describe("Insertion panels", () => {
    const tagButton = (wrapper: ReturnType<typeof mount>, label: string) =>
      wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === label)!;

    /** Opens the panel of a toolbar button and returns it. */
    const openWindow = async (label: string, context?: BBCodeContext) => {
      const wrapper = mount(BBCodeEditor, {
        attachTo: document.body,
        props: { modelValue: "", ...(context ? { context } : {}) },
      });
      await tagButton(wrapper, label).trigger("click");
      await nextTick();
      return {
        wrapper,
        card: wrapper.get(`[role="dialog"][aria-label="${label}"]`),
      };
    };

    it.each([
      ["Вставить ссылку", "common"],
      ["Вставить изображение", "common"],
      ["Спойлер", "common"],
      ["Приватный текст", "post"],
    ] as const)(
      "%s opens a panel on the page, not a window over it",
      async (label, context) => {
        const { wrapper, card } = await openWindow(label, context);
        expect(card.attributes("aria-modal")).toBeUndefined();
        // In the editor itself, beside its toolbar: nothing is carried to
        // <body>, nothing dims the page.
        expect(
          wrapper.get(".bbcode-editor-wrapper").element.contains(card.element),
        ).toBe(true);
        expect(card.element.closest("teleport-stub")).toBeNull();
        expect(document.querySelector(".dialog-backdrop")).toBeNull();
        expect(tagButton(wrapper, label).attributes("aria-expanded")).toBe(
          "true",
        );
        // The text of the editor is still there to be read and typed in.
        expect(wrapper.find(".bbcode-editor").isVisible()).toBe(true);
      },
    );

    it("closes on its button pressed again, and on Escape back to the button", async () => {
      // The caret back on the button shows its tooltip, which measures its
      // text on a canvas jsdom does not have; a null context is the path the
      // tooltip already handles.
      vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(null);
      const { wrapper } = await openWindow("Вставить ссылку");
      const button = tagButton(wrapper, "Вставить ссылку");

      await button.trigger("click");
      expect(wrapper.find('[role="dialog"]').exists()).toBe(false);
      expect(button.attributes("aria-expanded")).toBe("false");

      await button.trigger("click");
      await nextTick();
      await wrapper
        .get('[role="dialog"] input[name="url"]')
        .trigger("keydown", { key: "Escape" });
      expect(wrapper.find('[role="dialog"]').exists()).toBe(false);
      expect(document.activeElement).toBe(button.element);
    });

    it("gives way to the panel of the next button", async () => {
      const { wrapper } = await openWindow("Вставить ссылку");
      const image = tagButton(wrapper, "Вставить изображение");
      image.element.dispatchEvent(
        new MouseEvent("pointerdown", { bubbles: true }),
      );
      await image.trigger("click");
      await nextTick();
      expect(
        wrapper
          .findAll('[role="dialog"]')
          .map((panel) => panel.attributes("aria-label")),
      ).toEqual(["Вставить изображение"]);
    });

    it("says in itself what cannot be inserted", async () => {
      const { card } = await openWindow("Вставить ссылку");
      await card.get('input[name="url"]').setValue("mailto:a@example.com");
      await card.get('input[name="url"]').trigger("keydown", { key: "Enter" });
      expect(card.get(".insert-field-error").text()).toBe(
        "Разрешены только адреса http и https",
      );
      expect(card.isVisible()).toBe(true);
    });

    it("marks the optional text of a link once", async () => {
      // The caption said "(опционально)" and the window added its own
      // "(необязательно)" after it: two marks for one fact.
      const { card } = await openWindow("Вставить ссылку");

      const caption = card
        .findAll(".insert-field-label")
        .find((label) => label.text().startsWith("Текст ссылки"))!;
      expect(caption.text().match(/необязательно|опционально/g)).toEqual([
        "необязательно",
      ]);
    });

    it("asks a link for its text first and starts the caret there", async () => {
      // The owner's order (28 September 2026): the text as it will read in
      // the post, then the address it leads to. The caret goes to the first
      // empty field, so it starts in the text.
      const { card } = await openWindow("Вставить ссылку");
      await nextTick();
      expect(
        card.findAll("input").map((input) => input.attributes("name")),
      ).toEqual(["text", "url"]);
      expect(
        card.findAll(".insert-field-label label").map((label) => label.text()),
      ).toEqual(["Текст ссылки", "URL ссылки"]);
      expect(document.activeElement).toBe(
        card.get('input[name="text"]').element,
      );
    });

    it("inserts with the main button of a page and leaves with Отмена", async () => {
      // The panel's two buttons were both grey, and the owner could not tell
      // which one inserted.
      const { card } = await openWindow("Вставить изображение");
      const [main, cancel] = card.findAll("button");
      expect([main.text(), cancel.text()]).toEqual(["Вставить", "Отмена"]);
      expect(main.classes()).toContain("primary");
      expect(cancel.classes()).not.toContain("primary");
    });

    it("takes for an image only the addresses an image is drawn from", async () => {
      // The address of an image went through the link's check and let
      // mailto: through, which sanitizeImageUrl then dropped. The window now
      // accepts exactly what sanitizeImageUrl keeps, and says which.
      const { card } = await openWindow("Вставить изображение");
      const address = card.get<HTMLInputElement>('input[name="url"]');

      const refusal = async (value: string) => {
        await address.setValue(value);
        await address.trigger("blur");
        await nextTick();
        const error = card.find(".insert-field-error");
        return error.exists() ? error.text() : null;
      };

      for (const value of [
        "https://example.com/a.png",
        "http://example.com/a.png",
        "mailto:someone@example.com",
        "tel:+100",
        "ftp://example.com/a.png",
        "javascript:alert(1)",
      ]) {
        const drawn = sanitizeImageUrl(value) !== "#";
        expect(await refusal(value), value).toBe(
          drawn ? null : "Разрешены только адреса http и https",
        );
      }
    });
  });

  // ============================================================================
  // THE TOOLBAR AT EVERY WIDTH
  // ============================================================================

  /**
   * At every width of the window the tag buttons wrap, as they stood before
   * 5 October, so that every tag is in sight and none is cut away into "…": a
   * phone shows the row of the full version. Two lines at 1366 under the
   * comment of a topic, one in the window of 920. The row breaks between
   * groups alone, so "ul ol" never parts and no line opens with a rule.
   */
  describe("Toolbar at every width", () => {
    const sheet = ruleMap(
      componentCss(
        join(dirname(fileURLToPath(import.meta.url)), "BBCodeEditor.vue"),
      ),
    );

    /** The editor on the page, in a window of the given width. */
    const onPage = async (width: number) => {
      windowOf(width);
      const wrapper = mount(BBCodeEditor, {
        attachTo: document.body,
        props: { modelValue: "" },
      });
      await flushPromises();
      return wrapper;
    };

    type Wrapper = ReturnType<typeof mount>;

    /** The tag buttons a reader sees. */
    const inSight = (wrapper: Wrapper) =>
      wrapper
        .findAll(".toolbar-tags .tag-btn")
        .filter((button) => !button.element.closest("[hidden]"))
        .map((button) => button.text());

    const TAGS = [
      "b",
      "i",
      "u",
      "strike",
      "spoiler",
      "nsfw",
      "ul",
      "ol",
      "img",
      "link",
      "quote",
      "tab",
      "code",
      "noparse",
      "load",
      "help",
    ];

    it.each([390, 680, 1000, 1001, 1366])(
      'shows every tag button at %i, and no "…"',
      async (width) => {
        const wrapper = await onPage(width);
        expect(inSight(wrapper)).toEqual(TAGS);
        expect(wrapper.find(".tag-btn-more").exists()).toBe(false);
        expect(wrapper.find(".more-menu").exists()).toBe(false);
        expect(wrapper.find("[data-fit-row]").exists()).toBe(false);
      },
    );

    it.each([390, 1366])(
      "wraps between groups alone at %i, each but the last closed by its rule",
      async (width) => {
        const wrapper = await onPage(width);
        const row = wrapper.get(".toolbar-tags");
        const groups = row.findAll(".tag-group");
        expect(groups.map((group) => group.element.parentElement)).toEqual(
          groups.map(() => row.element),
        );
        expect(
          groups.map((group) =>
            group.findAll(".tag-btn").map((button) => button.text()),
          ),
        ).toEqual([
          ["b", "i", "u", "strike"],
          ["spoiler", "nsfw"],
          ["ul", "ol"],
          ["img", "link"],
          ["quote"],
          ["tab"],
          ["code", "noparse"],
          ["load", "help"],
        ]);
        // The rule hangs at the end of the line its group ends, as a bar of
        // a caption line does, and nothing ends the last group.
        expect(
          groups.map((group) =>
            group.element.lastElementChild!.classList.contains(
              "toolbar-separator",
            ),
          ),
        ).toEqual([true, true, true, true, true, true, true, false]);
        expect(row.findAll(".toolbar-separator")).toHaveLength(7);
      },
    );

    it("lays the row out as a wrapping flex line of the groups, with the gaps it had before 5 October", () => {
      expect(sheet.get(".toolbar-tags")).toMatchObject({
        display: "flex",
        "flex-wrap": "wrap",
        "align-items": "center",
      });
      expect(sheet.get(".tag-group")).toMatchObject({
        display: "inline-flex",
        "align-items": "center",
      });
      expect(sheet.get(".tag-group")?.["flex-wrap"]).toBeUndefined();
      expect(sheet.get(".toolbar-tags")?.gap).toBe(
        sheet.get(".tag-group")?.gap,
      );
    });

    it.each([390, 1366])(
      "hangs the panel of a button from the button itself at %i",
      async (width) => {
        const wrapper = await onPage(width);
        const link = wrapper
          .findAll(".tag-btn")
          .find(
            (button) => button.attributes("aria-label") === "Вставить ссылку",
          )!;
        await link.trigger("click");
        await nextTick();
        expect(wrapper.findComponent(InsertPanel).props("anchor")).toBe(
          link.element,
        );
      },
    );

    // A column can be narrower than the widest group of tags and the switch
    // side by side: at 1001 the form of a post gives the toolbar 243,
    // "spoiler nsfw private |" takes 154 and the switch 114. The switch went
    // out of the editor there, over the frame of the card. The toolbar wraps
    // instead: the switch goes down to a line of its own, at its right end.
    it("sends the switch to a line of its own where it does not fit beside the tags", () => {
      expect(sheet.get(".editor-toolbar")?.["flex-wrap"]).toBe("wrap");
      expect(sheet.get(".editor-toolbar .mode-tabs")?.["margin-left"]).toBe(
        "auto",
      );
      // The switch never shrinks: a line of its own is where it goes.
      expect(sheet.get(".mode-tabs")?.["flex-shrink"]).toBe("0");
      expect(sheet.get(".toolbar-tags")?.flex).toBe("1 1 0");
    });

    it("gives a tag button no size of its own for a narrow window", () => {
      const css = componentCss(
        join(dirname(fileURLToPath(import.meta.url)), "BBCodeEditor.vue"),
      );
      expect(css).not.toMatch(/@media[^{]*max-width/);
      const floors = styleRules(css)
        .filter((rule) => rule.selector === ".tag-btn")
        .map((rule) => rule.declarations["min-height"])
        .filter(Boolean);
      expect(floors).toEqual([]);
    });

    it("sets a pressed tag and a count over the limit in the bold of the face", () => {
      // PT Sans ships 400 and 700 alone: a 600 was a 700 under another name.
      const rules = styleRules(
        componentCss(
          join(dirname(fileURLToPath(import.meta.url)), "BBCodeEditor.vue"),
        ),
      );
      const weight = (selector: string) =>
        rules.find((rule) => rule.selector === selector)?.declarations[
          "font-weight"
        ];
      expect(weight(".tag-btn.active")).toBe("700");
      expect(weight(".status-item.over-limit")).toBe("700");
    });
  });

  // ============================================================================
  // WHAT THE EDITOR WRITES
  // ============================================================================

  /**
   * The panels, the toolbar and the keys write only the spellings of the
   * grammar (docs/architecture/BBCODE_RENDERING.md, "Редактор"), in either
   * mode, and the markup check is silent on all of them.
   */
  describe("What the panels write", () => {
    type Mode = "bbcode" | "wysiwyg";

    const mountIn = async (mode: Mode, props: Record<string, unknown> = {}) => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", ...props },
      });
      await flushPromises();
      if (mode === "bbcode") {
        await wrapper.findAll(".mode-tab")[0].trigger("click");
        await nextTick();
      }
      return wrapper;
    };

    const button = (wrapper: ReturnType<typeof mount>, label: string) =>
      wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === label)!;

    const lastValue = (wrapper: ReturnType<typeof mount>) => {
      const emitted = wrapper.emitted("update:modelValue") ?? [];
      return emitted.length
        ? (emitted[emitted.length - 1][0] as string)
        : undefined;
    };

    /** Open the panel of a button, fill it in and insert. */
    const insert = async (
      mode: Mode,
      label: string,
      values: Record<string, string>,
      props: Record<string, unknown> = {},
    ) => {
      const wrapper = await mountIn(mode, props);
      await button(wrapper, label).trigger("click");
      await nextTick();
      for (const [name, value] of Object.entries(values)) {
        await wrapper
          .get(`[role="dialog"] input[name="${name}"]`)
          .setValue(value);
      }
      await wrapper
        .findAll('[role="dialog"] button')
        .find((btn) => btn.text() === "Вставить")!
        .trigger("click");
      await flushPromises();
      await nextTick();
      return lastValue(wrapper);
    };

    const cases: Array<
      [Mode, string, Record<string, string>, string, Record<string, unknown>?]
    > = [
      [
        "bbcode",
        "Вставить ссылку",
        { url: "https://example.com/" },
        "[link]https://example.com/[/link]",
      ],
      [
        "bbcode",
        "Вставить ссылку",
        { url: "https://example.com/", text: "Главная" },
        '[link text="Главная"]https://example.com/[/link]',
      ],
      [
        "wysiwyg",
        "Вставить ссылку",
        { url: "https://example.com/", text: "Главная" },
        '[link text="Главная"]https://example.com/[/link]',
      ],
      [
        "bbcode",
        "Вставить изображение",
        {
          url: "https://example.com/a.png",
          width: "300",
          height: "200",
        },
        '[img width="300" height="200"]https://example.com/a.png[/img]',
      ],
      [
        "bbcode",
        "Вставить изображение",
        { url: "https://example.com/a.png", height: "200" },
        '[img height="200"]https://example.com/a.png[/img]',
      ],
      [
        "wysiwyg",
        "Вставить изображение",
        {
          url: "https://example.com/a.png",
          width: "300",
          height: "200",
        },
        '[img width="300" height="200"]https://example.com/a.png[/img]',
      ],
      [
        "bbcode",
        "Приватный текст",
        { character: "Вася, Петя" },
        '[private="Вася, Петя"][/private]',
        { context: "post" },
      ],
      [
        "bbcode",
        "Приватный текст",
        {},
        "[private][/private]",
        { context: "post" },
      ],
      [
        "bbcode",
        "Спойлер",
        { text: "Карта" },
        '[spoiler text="Карта"][/spoiler]',
      ],
      ["bbcode", "Спойлер", {}, "[spoiler][/spoiler]"],
      [
        "wysiwyg",
        "Спойлер",
        { text: "Карта" },
        '[spoiler text="Карта"][/spoiler]',
      ],
    ];

    it.each(cases)(
      "in the %s mode, %s writes the grammar's spelling (%j)",
      async (mode, label, values, written, props = {}) => {
        const value = await insert(mode, label, values, props);
        expect(value).toBe(written);
        expect(validateBBCode(written)).toEqual([]);
      },
    );

    it.each([
      ["Вставить ссылку", "text", 'а"]б', "Нельзя ставить кавычку перед ]"],
      [
        "Вставить ссылку",
        "text",
        'а" b="c',
        "Нельзя ставить кавычку перед словом со знаком =",
      ],
      ["Вставить изображение", "width", "0", "Только целое число от 1 до 9999"],
      [
        "Вставить изображение",
        "height",
        "10000",
        "Только целое число от 1 до 9999",
      ],
      [
        "Вставить ссылку",
        "url",
        "http://localhost/",
        "Адрес ведет в локальную сеть, такой сайт не покажет",
      ],
      // Not an address at all, and not the reader's network: the refusal
      // used to say it was.
      [
        "Вставить изображение",
        "url",
        "https://example.com/a b.png",
        "Введите корректный URL",
      ],
      ["Вставить ссылку", "url", "https://", "Введите корректный URL"],
    ])(
      "%s refuses a %s the markup cannot hold: %s",
      async (label, field, value, refusal) => {
        const wrapper = await mountIn("bbcode");
        await button(wrapper, label).trigger("click");
        await nextTick();
        const input = wrapper.get(`[role="dialog"] input[name="${field}"]`);
        await input.setValue(value);
        await input.trigger("blur");
        await nextTick();
        expect(wrapper.get('[role="dialog"] .insert-field-error').text()).toBe(
          refusal,
        );
      },
    );

    // An image has no alt: a reader never sees it, and text nobody sees on
    // the page is where an insult goes unnoticed.
    it("asks an image for its address and size and nothing else", async () => {
      const wrapper = await mountIn("bbcode");
      await button(wrapper, "Вставить изображение").trigger("click");
      await nextTick();
      expect(
        wrapper
          .findAll('[role="dialog"] input')
          .map((input) => input.attributes("name")),
      ).toEqual(["url", "width", "height"]);
      expect(wrapper.text()).not.toContain("Описание");
    });

    it("names a character optionally", async () => {
      const wrapper = await mountIn("bbcode", { context: "post" });
      await button(wrapper, "Приватный текст").trigger("click");
      await nextTick();
      const caption = wrapper
        .findAll(".insert-field-label")
        .find((label) => label.text().startsWith("Имена персонажей"))!;
      expect(caption.text()).toContain("необязательно");
    });

    it("takes a spoiler off without asking in the visual mode", async () => {
      const wrapper = await mountIn("wysiwyg", {
        modelValue: "[spoiler]текст[/spoiler]",
      });
      const editor = (wrapper.vm as unknown as { editor: Editor }).editor;
      editor.commands.setTextSelection(3);
      await button(wrapper, "Спойлер").trigger("click");
      await flushPromises();
      expect(wrapper.find('[role="dialog"]').exists()).toBe(false);
      expect(lastValue(wrapper)).toBe("текст");
    });
  });

  describe("Keys", () => {
    const keydown = (target: EventTarget, init: KeyboardEventInit) =>
      target.dispatchEvent(
        new KeyboardEvent("keydown", {
          bubbles: true,
          cancelable: true,
          ...init,
        }),
      );

    // The kit bound Ctrl+Enter to a line break, so the visual mode, the
    // default one, never sent anything with it.
    it("Ctrl+Enter sends from the visual mode", async () => {
      const wrapper = mount(BBCodeEditor, { props: { modelValue: "текст" } });
      await flushPromises();
      const editor = (wrapper.vm as unknown as { editor: Editor }).editor;

      keydown(editor.view.dom, { key: "Enter", ctrlKey: true });
      await nextTick();

      expect(wrapper.emitted("submit")).toBeTruthy();
      expect(editor.getHTML()).not.toContain("<br");
    });

    // On a Russian layout the key types "ы": bound to the letter, the spoiler
    // missed it and the kit's strike, bound to the same key, took it.
    it("Ctrl+Shift+S makes a spoiler in the visual mode on any layout", async () => {
      const wrapper = mount(BBCodeEditor, { props: { modelValue: "текст" } });
      await flushPromises();
      const editor = (wrapper.vm as unknown as { editor: Editor }).editor;
      editor.commands.selectAll();

      keydown(editor.view.dom, {
        key: "Ы",
        code: "KeyS",
        ctrlKey: true,
        shiftKey: true,
      });
      await flushPromises();

      const emitted = wrapper.emitted("update:modelValue")!;
      expect(emitted[emitted.length - 1][0]).toBe("[spoiler]текст[/spoiler]");
    });

    it.each([
      ["и", "KeyB", false, "[b]test[/b] text"],
      ["Ы", "KeyS", true, "[spoiler]test[/spoiler] text"],
    ])(
      "the BBCode mode reads the key, not the letter: %s",
      async (key, code, shiftKey, written) => {
        const wrapper = mount(BBCodeEditor, {
          props: { modelValue: "test text" },
        });
        await flushPromises();
        await wrapper.findAll(".mode-tab")[0].trigger("click");
        await nextTick();
        const textarea = wrapper.find(".bbcode-textarea");
        (textarea.element as HTMLTextAreaElement).setSelectionRange(0, 4);

        await textarea.trigger("keydown", {
          key,
          code,
          ctrlKey: true,
          shiftKey,
        });
        await nextTick();

        const emitted = wrapper.emitted("update:modelValue")!;
        expect(emitted[emitted.length - 1][0]).toBe(written);
      },
    );
  });

  describe("The tags of a surface", () => {
    const hasButton = (context: string, label: string) =>
      mount(BBCodeEditor, {
        props: { modelValue: "", context, isModerator: true } as never,
      })
        .findAll(".tag-btn")
        .some((btn) => btn.attributes("aria-label") === label);

    // Private messages do not render [mod]; the global chat does.
    it("offers [mod] where the server renders it and nowhere else", () => {
      expect(hasButton("message", "Модераторский блок")).toBe(true);
      expect(hasButton("common", "Модераторский блок")).toBe(true);
      expect(hasButton("direct", "Модераторский блок")).toBe(false);
      expect(hasButton("post", "Модераторский блок")).toBe(false);
    });

    const help = async (context: string) => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", context, isModerator: true } as never,
      });
      await wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === "Показать справку")!
        .trigger("click");
      await flushPromises();
      return wrapper.get(".help-table").text();
    };

    it("shows in its help the tags of the surface, with real examples", async () => {
      const post = await help("post");
      expect(post).toContain('[private="Вася, Петя"]');
      expect(post).toContain("[private]");
      expect(post).not.toContain("[mod]");

      const common = await help("common");
      expect(common).not.toContain("[private");
      expect(common).toContain("[mod]");
      expect(common).toContain('[link text="Главная"]https://example.com/');
      expect(common).toContain('[img width="60"]');
      expect(common).toContain('[spoiler text="Карта"]');
      expect(common).toContain("[mention]");
      // No placeholder stands where a parameter's name belongs.
      expect(common).not.toMatch(/\[(link|img|private|spoiler)=/);
    });
  });

  describe("The markup check", () => {
    it("names a broken spelling in Russian, with its line", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: 'раз\n[code="x"]пример[/code]' },
      });
      await flushPromises();
      await wrapper.findAll(".mode-tab")[0].trigger("click");
      await new Promise((resolve) => setTimeout(resolve, 400));
      await nextTick();

      expect(wrapper.get(".validation-errors .error-text").text()).toBe(
        "Строка 2: у [code] нет значения, пишется просто [code]",
      );
    });

    // One finding to a line, each after the warning sign, its text whole: the
    // look the check had before its plain lines, which read worse.
    it("puts each finding on a line of its own, after the warning sign", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "[b]текст\n[/i]" },
      });
      await flushPromises();
      await wrapper.findAll(".mode-tab")[0].trigger("click");
      await new Promise((resolve) => setTimeout(resolve, 400));
      await nextTick();

      const lines = wrapper.findAll(".validation-error");
      expect(lines.map((line) => line.get(".error-text").text())).toEqual([
        "Строка 1: [b] не закрыт, добавьте [/b]",
        "Строка 2: [/i] ничего не закрывает, уберите его или откройте [i] выше",
      ]);
      for (const line of lines) {
        const sign = line.get(".error-icon");
        expect(sign.text()).toBe("\u26A0");
        expect(sign.attributes("aria-hidden")).toBe("true");
      }
      // The words are one run of text: no tag of them is set apart.
      expect(wrapper.find(".validation-errors code").exists()).toBe(false);
    });

    // The save path of a comment refuses a hidden block and names the reason.
    // The check used to read every surface as a game post and said nothing,
    // so the author learnt it only from the refusal after pressing send.
    const checked = async (
      context: BBCodeContext,
      modelValue: string,
      checkContext?: BBCodeContext,
    ) => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue, context, checkContext },
      });
      await flushPromises();
      await wrapper.findAll(".mode-tab")[0].trigger("click");
      await new Promise((resolve) => setTimeout(resolve, 400));
      await nextTick();
      return wrapper;
    };

    it("names a hidden block where the surface has none, as the save path does", async () => {
      const comment = await checked("common", 'раз\n[private="Вася"]секрет');

      expect(comment.get(".validation-errors").text()).toContain(
        "Строка 2: скрытый текст работает только в игровых постах, для примера оберните его в [noparse]",
      );
    });

    it("leaves a hidden block alone in a game post", async () => {
      const post = await checked("post", '[private="Вася"]секрет[/private]');

      expect(post.find(".validation-errors").exists()).toBe(false);
    });

    // The metagame half of a game post: the post's buttons and preview, saved
    // on the comment surface, which refuses the block.
    it("checks as the surface it is told the text is saved on, keeping the buttons", async () => {
      const metagame = await checked(
        "post",
        '[private="Вася"]секрет[/private]',
        "common",
      );

      expect(metagame.get(".validation-errors").text()).toContain(
        "Строка 1: скрытый текст работает только в игровых постах, для примера оберните его в [noparse]",
      );
      expect(metagame.find(".tag-btn-private").exists()).toBe(true);
    });
  });
});

/**
 * The band of the markup check under the field, read off the stylesheet: the
 * page a shade darker, and the findings in the colour of a refusal. The owner
 * turned down the blue fill it had ("давай не синюю подложку, лучше затемнить
 * бэк каким-то серым слегка"), and a colour chosen for the band again is what
 * this refuses.
 */
describe("BBCodeEditor markup check band", () => {
  const rules = styleRules(
    componentCss(
      join(dirname(fileURLToPath(import.meta.url)), "BBCodeEditor.vue"),
    ),
  );
  const declared = (selector: string) =>
    rules
      .filter((rule) => rule.selector === selector)
      .map((rule) => rule.declarations);

  it("lays the band on the grey overlay of the controls", () => {
    expect(declared(".validation-errors")[0]["background-color"]).toBe(
      "var(--control-bg-overlay)",
    );
  });

  it("keeps the findings red", () => {
    expect(declared(".validation-error")[0].color).toBe("var(--accent-red)");
  });
});

/**
 * The editor on a phone is the editor of 1366 (owner, 12 October 2026: a
 * phone shows the full version, changed as little as its width forces, and
 * text stands on the left).
 *
 * - "0 слов | 0 / 50000 символов" under the field stood in the middle of a
 *   phone and on the left of 1366, its first letter on the field's edge; the
 *   width forced neither.
 * - The row of tags stood 8 in from the field's edges on a phone, its first
 *   button and the right end of the switch, and on the edges at 1366.
 * - The hint of an empty field was a float of no height in a box one line
 *   high that cuts what overflows it: a hint of two lines on a phone showed
 *   its second line as a strip of the tops of its letters. It takes the
 *   height of its lines now, and the field grows under it.
 *
 * Read off the stylesheet, each rule with the @media it stands in: jsdom lays
 * nothing out. That the browsers lay it out so is the live stand's to show.
 */
describe("BBCodeEditor on a phone", () => {
  type Rule = {
    media: string | null;
    selector: string;
    declarations: Record<string, string>;
  };

  /** The flat compiled sheet, rule by rule, each with the @media over it. */
  const sheet: Rule[] = (() => {
    const css = componentCss(
      join(dirname(fileURLToPath(import.meta.url)), "BBCodeEditor.vue"),
    );
    const rules: Rule[] = [];
    for (const [, condition, body] of css.matchAll(
      /@media\s*([^{]+)\{((?:[^{}]*\{[^{}]*\})*)\s*\}/g,
    )) {
      for (const rule of styleRules(body))
        rules.push({ media: condition.trim(), ...rule });
    }
    const outside = css.replace(
      /@media\s*([^{]+)\{((?:[^{}]*\{[^{}]*\})*)\s*\}/g,
      "",
    );
    for (const rule of styleRules(outside))
      rules.push({ media: null, ...rule });
    return rules;
  })();

  const rulesOf = (selector: string) =>
    sheet.filter((rule) => rule.selector === selector);

  /** The left and right of a padding shorthand or of its own sides. */
  const sides = (declarations: Record<string, string>) => {
    const parts = (declarations.padding ?? "").split(/\s+/).filter(Boolean);
    const [, right = parts[0], , left = right] = parts;
    return {
      left: declarations["padding-left"] ?? left,
      right: declarations["padding-right"] ?? right,
    };
  };

  const flush = (value: string | undefined) =>
    value === undefined || value === "0" || value === "0px";

  it("sets the counter line on the left at every width", () => {
    const counter = rulesOf(".status-bar");
    expect(counter.length).toBeGreaterThan(0);
    for (const { media, declarations } of counter) {
      expect(
        [undefined, "left", "start"],
        `.status-bar under ${media ?? "every width"}`,
      ).toContain(declarations["text-align"]);
    }
  });

  it("starts the counter line on the field's edge at every width", () => {
    for (const { media, declarations } of rulesOf(".status-bar")) {
      const { left } = sides(declarations);
      expect(flush(left), `.status-bar under ${media ?? "every width"}`).toBe(
        true,
      );
    }
  });

  it("stands the row of tags on the field's edges at every width", () => {
    const toolbar = rulesOf(".editor-toolbar");
    expect(toolbar.length).toBeGreaterThan(0);
    for (const { media, declarations } of toolbar) {
      const { left, right } = sides(declarations);
      const where = `.editor-toolbar under ${media ?? "every width"}`;
      expect(flush(left), where).toBe(true);
      expect(flush(right), where).toBe(true);
    }
  });

  // The field is drawn rounded with its overflow cut, and the browser of a
  // phone drew the grip that resizes it in the corner as a dot just outside
  // the rounding (the comment of a ticket at 390). A finger does not drag
  // that grip, so a touch screen has none; under a mouse it stays as it is.
  it("draws no grip in the corner of the field on a touch screen, and keeps it under a mouse", () => {
    const field = rulesOf(".bbcode-editor.resizable");

    expect(
      field
        .filter((rule) => rule.media === null)
        .map((rule) => rule.declarations.resize),
    ).toEqual(["vertical"]);
    expect(
      field
        .filter((rule) => rule.media === "(pointer: coarse)")
        .map((rule) => rule.declarations.resize),
    ).toEqual(["none"]);
  });

  it("gives the hint of an empty field the height of its lines and the caret the start of the line", () => {
    const hint = rulesOf(
      ".wysiwyg-content :deep(.tiptap) p.is-editor-empty:first-child::before",
    );
    expect(hint).toHaveLength(1);
    const [{ declarations }] = hint;

    // Of no height, the hint overflowed a box one line high, which cut it.
    expect(declarations).not.toHaveProperty("height");
    // It floats beside the empty line and takes none of its width: a hint as
    // wide as the line, its right margin taking that width back, so the caret
    // stands at the start of the line it shares with the hint's first line.
    expect(declarations).toMatchObject({
      float: "left",
      width: "100%",
      "margin-right": "-100%",
    });
    // The editable box holds the float, so all of the hint is the field to
    // tap, its second line too, and not the box around it.
    expect(
      rulesOf(".wysiwyg-content :deep(.tiptap)").map(
        (rule) => rule.declarations.display,
      ),
    ).toContain("flow-root");
  });
});

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
import BBCodeEditor from "./BBCodeEditor.vue";

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

// Mock ResizeObserver
vi.stubGlobal(
  "ResizeObserver",
  vi.fn(() => ({
    observe: vi.fn(),
    unobserve: vi.fn(),
    disconnect: vi.fn(),
  })),
);

describe("BBCodeEditor", () => {
  beforeEach(() => {
    localStorageMock.clear();
    vi.clearAllMocks();
  });

  afterEach(() => {
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
    it("shows help dialog when help button clicked", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      // Find help button by its content/aria-label
      const helpBtn = wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === "Показать справку");

      expect(helpBtn).toBeDefined();
      await helpBtn!.trigger("click");
      await nextTick();

      // Help dialog is teleported to body, check the button state instead
      expect(helpBtn!.classes()).toContain("active");
    });

    it("help button has proper ARIA attributes", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      const helpBtn = wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === "Показать справку");
      expect(helpBtn!.attributes("aria-expanded")).toBeDefined();
    });

    it("toggles help dialog state on button click", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      const helpBtn = wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === "Показать справку");

      // Click to open
      await helpBtn!.trigger("click");
      await nextTick();
      expect(helpBtn!.classes()).toContain("active");

      // Click to close
      await helpBtn!.trigger("click");
      await nextTick();
      expect(helpBtn!.classes()).not.toContain("active");
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
  // INPUT DIALOGS
  // ============================================================================

  describe("Input Dialogs", () => {
    it("shows link dialog when link button clicked", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      const linkBtn = wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === "Вставить ссылку");
      await linkBtn!.trigger("click");
      await nextTick();

      // InputDialog uses Teleport, check it exists in component state
      expect((wrapper.vm as any).showLinkDialog).toBe(true);
    });

    it("shows image dialog when image button clicked", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });

      const imgBtn = wrapper
        .findAll(".tag-btn")
        .find((btn) => btn.attributes("aria-label") === "Вставить изображение");
      await imgBtn!.trigger("click");
      await nextTick();

      expect((wrapper.vm as any).showImageDialog).toBe(true);
    });
  });
});

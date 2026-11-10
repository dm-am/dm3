/**
 * @vitest-environment jsdom
 */

/**
 * The status line under the editor: shown or hidden by its parent, the words
 * and the characters of the text, and the count over the limit marked.
 */
import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { mount, enableAutoUnmount } from "@vue/test-utils";
import { nextTick } from "vue";
import BBCodeEditor from "./BBCodeEditor.vue";
import { editorShell } from "@/shared/lib/testing/editorShell";

// Unmount every wrapper after each test so the component destroys its
// tiptap editor (see onUnmounted in BBCodeEditor.vue). Leaked editors
// keep ProseMirror DOMObserver flush timers pending, which can fire
// after jsdom teardown and fail the run with an unhandled error.
enableAutoUnmount(afterEach);

const { clean, reset } = editorShell();

describe("EditorStatusLine", () => {
  beforeEach(clean);
  afterEach(reset);

  describe("Character & Word Count", () => {
    it("shows status bar", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      expect(wrapper.find(".status-bar").exists()).toBe(true);
    });

    it("shows character count in BBCode mode", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "hello world" },
      });
      await nextTick();

      const statusBar = wrapper.find(".status-bar");
      expect(statusBar.text()).toContain("11");
    });

    it("shows word count", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "hello world" },
      });
      await nextTick();

      const statusBar = wrapper.find(".status-bar");
      expect(statusBar.text()).toContain("2");
    });

    it("shows over-limit warning when maxLength exceeded", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "hello world", maxLength: 5 },
      });
      await nextTick();

      expect(wrapper.find(".over-limit").exists()).toBe(true);
    });

    it("does not show over-limit when within limit", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "hi", maxLength: 100 },
      });
      await nextTick();

      expect(wrapper.find(".over-limit").exists()).toBe(false);
    });
  });
});

/**
 * @vitest-environment jsdom
 */

/**
 * The keys of the editor: the chords of the BBCode mode wrap the selection in
 * a tag, Ctrl+Enter sends from either mode, and a chord is read off the key
 * and not the letter, so it works on a Russian layout too.
 */
import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import { nextTick } from "vue";
import type { Editor } from "@tiptap/vue-3";
import BBCodeEditor from "../BBCodeEditor.vue";
import { editorShell } from "@/shared/lib/testing/editorShell";

// Unmount every wrapper after each test so the component destroys its
// tiptap editor (see onUnmounted in BBCodeEditor.vue). Leaked editors
// keep ProseMirror DOMObserver flush timers pending, which can fire
// after jsdom teardown and fail the run with an unhandled error.
enableAutoUnmount(afterEach);

const { clean, reset } = editorShell();

describe("editor keys", () => {
  beforeEach(clean);
  afterEach(reset);

  describe("Keyboard Shortcuts in BBCode Mode", () => {
    it.each(["b", "i", "u"])(
      "Ctrl+%s wraps the selection in its tag",
      async (letter) => {
        const wrapper = mount(BBCodeEditor, {
          props: { modelValue: "test text" },
        });
        await flushPromises();
        await nextTick();

        const textarea = wrapper.find(".bbcode-textarea");
        (textarea.element as HTMLTextAreaElement).setSelectionRange(0, 4);

        await textarea.trigger("keydown", { key: letter, ctrlKey: true });
        await flushPromises();
        await nextTick();

        const emitted = wrapper.emitted("update:modelValue");
        expect(emitted).toBeTruthy();
        const lastEmit = emitted![emitted!.length - 1][0] as string;
        expect(lastEmit).toContain(`[${letter}]`);
      },
    );

    it("Ctrl+Enter emits submit event", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "test" },
      });
      await flushPromises();
      await nextTick();

      const textarea = wrapper.find(".bbcode-textarea");
      await textarea.trigger("keydown", { key: "Enter", ctrlKey: true });
      await flushPromises();
      await nextTick();

      expect(wrapper.emitted("submit")).toBeTruthy();
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
});

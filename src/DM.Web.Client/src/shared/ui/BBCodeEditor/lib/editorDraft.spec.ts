/**
 * @vitest-environment jsdom
 */

/**
 * The draft of the editor: kept in localStorage under the key of its subject,
 * offered back in the status line and by the button of the toolbar, past its
 * week forgotten, emptied on a change of subject, and no failure of the store
 * breaks the editor.
 */
import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import { nextTick } from "vue";
import BBCodeEditor from "../BBCodeEditor.vue";
import { editorShell } from "@/shared/lib/testing/editorShell";

// Unmount every wrapper after each test so the component destroys its
// tiptap editor (see onUnmounted in BBCodeEditor.vue). Leaked editors
// keep ProseMirror DOMObserver flush timers pending, which can fire
// after jsdom teardown and fail the run with an unhandled error.
enableAutoUnmount(afterEach);

const { localStorageMock, clean, reset } = editorShell();

describe("editor draft", () => {
  beforeEach(clean);
  afterEach(reset);

  describe("Draft Management", () => {
    it("saves draft to localStorage", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", draftKey: "test-draft" },
      });
      await nextTick();

      const textarea = wrapper.find(".bbcode-textarea");
      await textarea.setValue("draft content");
      await nextTick();

      expect(localStorageMock.getItem("bbcode_draft_test-draft")).toContain(
        "draft content",
      );
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
        props: { modelValue: "", draftKey: "test-draft" },
      });
      await nextTick();

      // Component shows "Есть черновик" in status bar instead of a dialog
      expect(wrapper.find(".draft-available").exists()).toBe(true);
    });

    it("does not show draft indicator for expired drafts", async () => {
      const expiredDraft = JSON.stringify({
        content: "old draft",
        timestamp: Date.now() - 8 * 24 * 60 * 60 * 1000, // 8 days old
      });
      localStorageMock.getItem.mockReturnValueOnce(expiredDraft);

      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", draftKey: "test-draft" },
      });

      await nextTick();
      expect(wrapper.find(".draft-available").exists()).toBe(false);
    });

    it("forgets on mount the drafts of other subjects past their week", () => {
      const kept = (days: number) =>
        JSON.stringify({
          content: "черновик",
          timestamp: Date.now() - days * 24 * 60 * 60 * 1000,
        });
      localStorageMock.setItem("bbcode_draft_other", kept(8));
      localStorageMock.setItem("bbcode_draft_fresh", kept(1));
      localStorageMock.setItem("settings", kept(8));

      mount(BBCodeEditor, { props: { modelValue: "", draftKey: "mine" } });

      expect(localStorageMock.getItem("bbcode_draft_other")).toBeNull();
      expect(localStorageMock.getItem("bbcode_draft_fresh")).not.toBeNull();
      expect(localStorageMock.getItem("settings")).not.toBeNull();
    });

    it("does not show draft indicator when draft matches modelValue", async () => {
      const draftData = JSON.stringify({
        content: "same content",
        timestamp: Date.now(),
      });
      localStorageMock.getItem.mockReturnValueOnce(draftData);

      const wrapper = mount(BBCodeEditor, {
        props: {
          modelValue: "same content",
          draftKey: "test-draft",
        },
      });

      await nextTick();
      expect(wrapper.find(".draft-available").exists()).toBe(false);
    });
  });

  describe("Draft Status Indicator", () => {
    it("shows draft status in status bar when draftKey is set", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: {
          modelValue: "test content",
          draftKey: "test-draft",
        },
      });
      await nextTick();

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
        },
      });

      expect(wrapper.find(".draft-status").exists()).toBe(false);
    });

    it('shows "Сохранено" text after saving', async () => {
      const wrapper = mount(BBCodeEditor, {
        props: {
          modelValue: "",
          draftKey: "test-draft-2",
        },
      });
      await nextTick();

      const textarea = wrapper.find(".bbcode-textarea");
      await textarea.setValue("draft content");
      await flushPromises();
      await nextTick();

      const statusText = wrapper.find(".draft-status").text();
      expect(statusText).toContain("Сохранено");
    });
  });

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

  describe("Error Handling", () => {
    it("handles localStorage errors gracefully when saving draft", async () => {
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

      const textarea = wrapper.find(".bbcode-textarea");
      await textarea.setValue("content that fails to save");
      await flushPromises();
      await nextTick();

      expect(wrapper.find(".draft-status").exists()).toBe(false);

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

      await nextTick();
      expect(wrapper.find(".draft-available").exists()).toBe(false);

      localStorageMock.getItem.mockRestore();
    });
  });
});

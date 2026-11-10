/**
 * @vitest-environment jsdom
 */

/**
 * The markup check under the field of the BBCode mode: its findings in
 * Russian, one to a line after the warning sign, read on the surface the text
 * is saved on, and the band they stand in.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import { nextTick } from "vue";
import type { BBCodeContext } from "@/shared/lib/bbcode/bbcodeTagSet";
import BBCodeEditor from "./BBCodeEditor.vue";
import { styleRules } from "@/shared/lib/testing/styles";
import { editorCss, editorShell } from "@/shared/lib/testing/editorShell";

// Unmount every wrapper after each test so the component destroys its
// tiptap editor (see onUnmounted in BBCodeEditor.vue). Leaked editors
// keep ProseMirror DOMObserver flush timers pending, which can fire
// after jsdom teardown and fail the run with an unhandled error.
enableAutoUnmount(afterEach);

const { clean, reset } = editorShell();

describe("MarkupCheck", () => {
  beforeEach(() => {
    clean();
    vi.useFakeTimers({ toFake: ["setTimeout", "clearTimeout"] });
  });
  afterEach(() => {
    vi.useRealTimers();
    reset();
  });

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

      await vi.advanceTimersByTimeAsync(300);
      await nextTick();

      expect(wrapper.find(".validation-errors").exists()).toBe(true);
    });

    it("validation errors have proper ARIA attributes", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "[b]unclosed" },
      });
      await wrapper.findAll(".mode-tab")[0].trigger("click");
      await vi.advanceTimersByTimeAsync(300);
      await nextTick();

      const errorsDiv = wrapper.get(".validation-errors");
      expect(errorsDiv.attributes("role")).toBe("alert");
      expect(errorsDiv.attributes("aria-live")).toBe("polite");
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

  describe("The markup check", () => {
    it("names a broken spelling in Russian, with its line", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: 'раз\n[code="x"]пример[/code]' },
      });
      await flushPromises();
      await wrapper.findAll(".mode-tab")[0].trigger("click");
      await vi.advanceTimersByTimeAsync(300);
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
      await vi.advanceTimersByTimeAsync(300);
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
      await vi.advanceTimersByTimeAsync(300);
      await nextTick();
      return wrapper;
    };

    it("names a hidden block where the surface has none, as the save path does", async () => {
      const comment = await checked("common", 'раз\n[private="Вася"]секрет');

      expect(comment.get(".validation-errors").text()).toContain(
        "Строка 2: скрытый блок работает только в игровых постах, для примера оберните его в [noparse]",
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
        "Строка 1: скрытый блок работает только в игровых постах, для примера оберните его в [noparse]",
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
  const rules = styleRules(editorCss());
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

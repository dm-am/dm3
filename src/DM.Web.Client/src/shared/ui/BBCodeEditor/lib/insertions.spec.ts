/**
 * @vitest-environment jsdom
 */

/**
 * The insertions of the editor as the author meets them: the panel each asks
 * in, under its button and not in a window, what the panel refuses and why,
 * and what it writes into the text in either mode.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import { nextTick } from "vue";
import type { Editor } from "@tiptap/vue-3";
import { validateBBCode } from "@/shared/lib/bbcode/bbcodeWarnings";
import { type BBCodeContext } from "@/shared/lib/bbcode/bbcodeTagSet";
import BBCodeEditor from "../BBCodeEditor.vue";
import { editorShell } from "@/shared/lib/testing/editorShell";

// Unmount every wrapper after each test so the component destroys its
// tiptap editor (see onUnmounted in BBCodeEditor.vue). Leaked editors
// keep ProseMirror DOMObserver flush timers pending, which can fire
// after jsdom teardown and fail the run with an unhandled error.
enableAutoUnmount(afterEach);

const { clean, reset } = editorShell();

describe("insertions", () => {
  beforeEach(clean);
  afterEach(reset);

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
      ["Скрытый блок", "post"],
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

    it("asks a link for its text first and starts the caret there", async () => {
      // The text as it will read in the post, then the address it leads to.
      // The caret goes to the first empty field, so it starts in the text.
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
  });

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
        "Скрытый блок",
        { character: "Вася, Петя" },
        '[private="Вася, Петя"][/private]',
        { context: "post" },
      ],
      [
        "bbcode",
        "Скрытый блок",
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
      // Not an address at all, and not the reader's network
      [
        "Вставить изображение",
        "url",
        "https://example.com/a b.png",
        "Адрес записан с ошибкой",
      ],
      ["Вставить ссылку", "url", "https://", "Адрес записан с ошибкой"],
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
      await button(wrapper, "Скрытый блок").trigger("click");
      await nextTick();
      const caption = wrapper
        .findAll(".insert-field-label")
        .find((label) => label.text().startsWith("Имена персонажей"))!;
      expect(caption.text()).toContain("необязательно");
    });

    it("refuses a name the markup cannot hold", async () => {
      const wrapper = await mountIn("bbcode", { context: "post" });
      await button(wrapper, "Скрытый блок").trigger("click");
      await nextTick();
      const input = wrapper.get('[role="dialog"] input[name="character"]');
      const refusal = async (value: string) => {
        await input.setValue(value);
        await input.trigger("blur");
        await nextTick();
        return wrapper.get('[role="dialog"] .insert-field-error').text();
      };
      expect(await refusal("Вася[")).toBe("Нельзя использовать символ [");
      expect(await refusal('Вася"]')).toBe("Нельзя ставить кавычку перед ]");
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
});

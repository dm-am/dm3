/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import { nextTick } from "vue";
import type { Editor } from "@tiptap/vue-3";
import type { BBCodeContext } from "@/shared/lib/bbcode/bbcodeTagSet";
import { Tooltip } from "@/shared/ui/Tooltip";
import BBCodeEditor from "./BBCodeEditor.vue";
import InsertPanel from "./InsertPanel.vue";
import { editorShell } from "@/shared/lib/testing/editorShell";

/**
 * The row of buttons of the editor on every surface, for a moderator and for
 * anyone else, as the reader meets it: which buttons stand in which group and
 * in what order, and for each its caption, its name for a screen reader, its
 * tooltip, its colour and the window it opens. Taken as it is, so a change of
 * where the row comes from shows as a change of the row.
 */

// The editor destroys its tiptap instance on unmount; a leaked one fires
// ProseMirror timers after jsdom is gone.
enableAutoUnmount(afterEach);

const { clean, reset } = editorShell();

function rowOf(context: BBCodeContext, isModerator: boolean, disabled = false) {
  const wrapper = mount(BBCodeEditor, {
    props: { modelValue: "", context, isModerator, disabled },
  });
  const groups = wrapper.findAll(".toolbar-tags > .tag-group").map((group) => {
    const names = group.findAll(".tag-btn").map((button) => button.text());
    const rule = group.find(".toolbar-separator").exists() ? " |" : "";
    return names.join(" ") + rule;
  });
  const buttons = wrapper
    .findAllComponents(Tooltip)
    .filter((tooltip) => tooltip.find(".tag-btn").exists())
    .map((tooltip) => {
      const button = tooltip.get(".tag-btn");
      const look = button.element.firstElementChild?.tagName.toLowerCase();
      const tone = button.classes().find((c) => c.startsWith("tag-btn-"));
      return {
        name: button.text(),
        label: button.attributes("aria-label"),
        hint: tooltip.props("text"),
        look: look ?? null,
        tone: tone ?? null,
        opens: button.attributes("aria-haspopup") ?? null,
        disabled: button.attributes("disabled") !== undefined,
      };
    });
  return { row: groups.join(" "), buttons };
}

const EVERY_BUTTON = {
  b: ["Жирный", "Жирный (Ctrl+B)", "b", null, null],
  i: ["Курсив", "Курсив (Ctrl+I)", "i", null, null],
  u: ["Подчеркнутый", "Подчеркнутый (Ctrl+U)", "u", null, null],
  strike: ["Зачеркнутый", "Зачеркнутый", "s", null, null],
  spoiler: ["Спойлер", "Спойлер", null, null, "dialog"],
  nsfw: ["NSFW контент", "18+ контент", null, "tag-btn-nsfw", null],
  private: [
    "Скрытый блок",
    "Скрытый блок (видно только указанному персонажу)",
    null,
    "tag-btn-private",
    "dialog",
  ],
  ul: ["Маркированный список", "Маркированный список", null, null, null],
  ol: ["Нумерованный список", "Нумерованный список", null, null, null],
  img: ["Вставить изображение", "Вставить изображение", null, null, "dialog"],
  link: ["Вставить ссылку", "Вставить ссылку", null, null, "dialog"],
  quote: ["Цитата", "Цитата", null, null, null],
  mod: ["Модераторский блок", "Модераторский блок", null, "tag-btn-mod", null],
  tab: ["Вставить отступ", "Отступ", null, null, null],
  code: ["Код", "Моноширинный код", null, null, null],
  noparse: ["Без форматирования", "Без обработки BBCode", null, null, null],
  load: ["Загрузить черновик", "Загрузить черновик", null, null, null],
  help: ["Показать справку", "Справка", null, null, "dialog"],
} as const;

const WITH_MOD =
  "b i u strike | spoiler nsfw | ul ol | img link | quote mod | tab | code noparse | load help";
const WITHOUT_MOD =
  "b i u strike | spoiler nsfw | ul ol | img link | quote | tab | code noparse | load help";
const WITH_PRIVATE =
  "b i u strike | spoiler nsfw private | ul ol | img link | quote | tab | code noparse | load help";

describe("BBCodeEditor: the row of buttons", () => {
  it.each<[BBCodeContext, boolean, string]>([
    ["common", true, WITH_MOD],
    ["common", false, WITHOUT_MOD],
    ["message", true, WITH_MOD],
    ["message", false, WITHOUT_MOD],
    ["post", true, WITH_PRIVATE],
    ["post", false, WITH_PRIVATE],
    ["info", true, WITHOUT_MOD],
    ["info", false, WITHOUT_MOD],
    ["direct", true, WITHOUT_MOD],
    ["direct", false, WITHOUT_MOD],
    ["hint", true, "noparse | load help"],
    ["hint", false, "noparse | load help"],
  ])("on %s, moderator %s, reads %s", (context, isModerator, row) => {
    expect(rowOf(context, isModerator).row).toBe(row);
  });

  it.each<[BBCodeContext, boolean]>([
    ["common", true],
    ["post", false],
    ["hint", false],
  ])(
    "names, hints, colours and opens each button the same on %s, moderator %s",
    (context, isModerator) => {
      for (const button of rowOf(context, isModerator).buttons) {
        const [label, hint, look, tone, opens] =
          EVERY_BUTTON[button.name as keyof typeof EVERY_BUTTON];
        expect(button).toEqual({
          name: button.name,
          label,
          hint,
          look,
          tone,
          opens,
          disabled: false,
        });
      }
    },
  );

  it("leaves the help alone pressable in a shut editor", () => {
    const shut = rowOf("common", true, true).buttons;

    expect(shut.filter((b) => !b.disabled).map((b) => b.name)).toEqual([
      "help",
    ]);
  });
});

describe("the toolbar in the editor", () => {
  beforeEach(clean);
  afterEach(reset);

  describe("Disabled State", () => {
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
  });

  // The tag buttons wrap between groups: every tag stays in sight, and no line opens with a rule.
  describe("Toolbar at every width", () => {
    /** The editor on the page. */
    const onPage = async () => {
      const wrapper = mount(BBCodeEditor, {
        attachTo: document.body,
        props: { modelValue: "" },
      });
      await flushPromises();
      return wrapper;
    };

    it("shows every tag button", async () => {
      const wrapper = await onPage();
      expect(
        wrapper
          .findAll(".toolbar-tags .tag-btn")
          .map((button) => button.text()),
      ).toEqual([
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
    });

    it("wraps between groups alone, each but the last closed by its rule", async () => {
      const wrapper = await onPage();
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
    });

    it("hangs the panel of a button from the button itself", async () => {
      const wrapper = await onPage();
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
    });
  });

  describe("What a tag button writes", () => {
    // Each button that asks nothing: what it writes around the selection in
    // the BBCode mode, and what its command leaves in the visual document.
    const WRAPPING: [string, string, string][] = [
      ["b", "[b]text[/b]", "<strong>text</strong>"],
      ["i", "[i]text[/i]", "<em>text</em>"],
      ["u", "[u]text[/u]", "<u>text</u>"],
      ["strike", "[strike]text[/strike]", "<s>text</s>"],
      ["nsfw", "[nsfw]text[/nsfw]", 'data-bb-tag="nsfw"'],
      ["ul", "[ul]\n[li]text[/li]\n[/ul]", "<ul><li><p>text</p></li></ul>"],
      ["ol", "[ol]\n[li]text[/li]\n[/ol]", "<ol><li><p>text</p></li></ol>"],
      ["quote", "[quote]text[/quote]", 'data-bb-tag="quote"'],
      ["mod", "[mod]text[/mod]", 'data-bb-tag="mod"'],
      ["code", "[code]text[/code]", "<code>text</code>"],
      ["noparse", "[noparse]text[/noparse]", 'data-bb-tag="noparse"'],
    ];

    type Wrapper = ReturnType<typeof mount>;
    const editorOf = (wrapper: Wrapper) =>
      (wrapper.vm as unknown as { editor: Editor }).editor;

    /** The button pressed on "text" selected, in the mode select() leaves. */
    const press = async (
      name: string,
      select: (wrapper: Wrapper) => Promise<unknown>,
    ) => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "text", context: "common", isModerator: true },
      });
      await flushPromises();
      await select(wrapper);
      await wrapper
        .findAll(".tag-btn")
        .find((button) => button.text() === name)!
        .trigger("click");
      await flushPromises();
      return wrapper;
    };

    it.each(WRAPPING)(
      "%s wraps the selection of the BBCode mode",
      async (name, written) => {
        const wrapper = await press(name, async (shown) => {
          await shown.findAll(".mode-tab")[0].trigger("click");
          await nextTick();
          const field = shown.get(".bbcode-textarea").element;
          (field as HTMLTextAreaElement).setSelectionRange(0, 4);
        });

        expect(wrapper.emitted("update:modelValue")!.at(-1)).toEqual([written]);
      },
    );

    it.each(WRAPPING)(
      "%s runs its command on the selection of the visual mode",
      async (name, _written, mark) => {
        const wrapper = await press(name, async (shown) =>
          editorOf(shown).commands.selectAll(),
        );

        expect(editorOf(wrapper).getHTML()).toContain(mark);
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
  });
});

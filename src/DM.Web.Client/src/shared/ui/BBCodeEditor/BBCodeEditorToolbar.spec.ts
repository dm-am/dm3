/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, afterEach } from "vitest";
import { mount, enableAutoUnmount } from "@vue/test-utils";
import type { BBCodeContext } from "@/shared/lib/utils/bbcode";
import { Tooltip } from "@/shared/ui/Tooltip";
import BBCodeEditor from "./BBCodeEditor.vue";

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
    "Приватный текст",
    "Приватный текст (видно только указанному персонажу)",
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

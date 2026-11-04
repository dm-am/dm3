/**
 * @vitest-environment node
 */

/**
 * A single sentence carries no period at its end in a list item and in
 * anything that pops up over the page: a window, a confirmation, a plate, a
 * hint that opens on hover, a panel that drops down. A full stop closes a
 * thought that has a neighbour, and a bullet or a line in a window has none. A
 * paragraph of a page, a grey line under a heading, a hint under a field and an
 * empty state are the writer's call, and the documents (pages/legal,
 * pages/rules) are left out whole.
 *
 * Two sentences in one block keep theirs. An ellipsis, an abbreviation and a
 * value a template drops into the middle of a line are left alone. What is read
 * is told in shared/lib/testing/listAndPopupCopy.ts. The check reads the
 * templates: a paragraph split across lines by the formatter is one sentence.
 */
import { describe, expect, it } from "vitest";
import {
  disk as sources,
  treeOf,
  type Tree,
} from "@/shared/lib/testing/toastScan";
import { copyOf } from "@/shared/lib/testing/listAndPopupCopy";

/**
 * A word whose own period is not the end of anything, matched whole:
 * "пропадет." ends in the letters of "т." and is a sentence.
 */
const ABBREVIATION =
  /(?:^|[^а-яА-Яa-zA-Z])(сек|мин|ч|г|гг|т|д|п|др|см|стр|р|мод)\.$/i;

/** A second sentence: a stop, a question or a bang with a space after it. */
const SENTENCE_BREAK = /[.!?]\s/g;

/**
 * The pages that are documents rather than screens: the user agreement, the
 * privacy policy and the rules of the site. Only these two folders: the page
 * about the project, the testimonials and the page about helping it are
 * screens, and their lists are read like any other.
 */
const DOCUMENTS = ["pages/legal", "pages/rules"];

/** The client's sources, the documents and the type declarations left out. */
const disk = (): Tree => {
  const all = sources();
  return {
    files: all.files.filter(
      (file) =>
        !file.endsWith(".d.ts") &&
        !DOCUMENTS.some((dir) => file.startsWith(`${dir}/`)),
    ),
    read: all.read,
  };
};

/** What is wrong with a line, or an empty string when nothing is. */
const offends = (text: string): string => {
  if (!text) return "";
  if (text.endsWith("...")) return "";
  if (!text.endsWith(".") || ABBREVIATION.test(text)) return "";
  const body = text.slice(0, -1);
  // Two sentences keep the period of the last one, or the block reads as an
  // unfinished second thought.
  if ([...body.matchAll(SENTENCE_BREAK)].length) return "";
  return "a period at the end";
};

/** Every offending line of a tree, as "file: problem - line". */
const offenders = (tree: Tree): string[] =>
  [...copyOf(tree)].flatMap(([file, lines]) =>
    [...new Set(lines)]
      .filter((line) => offends(line))
      .map((line) => `${file}: ${offends(line)} - ${line}`),
  );

describe("a single sentence in a list or in a popup", () => {
  it("ends without a period", () => {
    expect(offenders(disk())).toEqual([]);
  });

  it("knows what is not a sentence ending", () => {
    // The canary and the three exemptions, so a green run means the check ran.
    expect(offends("Ваша почта успешно обновлена.")).toBe(
      "a period at the end",
    );
    expect(offends("Нужна помощь? Обратитесь в поддержку")).toBe("");
    expect(offends("Проверяем ссылку...")).toBe("");
    expect(offends("Повторите через 30 сек.")).toBe("");
    expect(offends("Ссылка действительна 48 часов")).toBe("");
    expect(offends("1 500 р.")).toBe("");
    expect(offends("Мод.")).toBe("");
  });

  it("reads a word ending in the letters of an abbreviation as a word", () => {
    // "пропадет." ends in "т." and "код." in "д.": taken for the tail of an
    // abbreviation, every such line walked past the check.
    expect(offends("Набранный текст пропадет.")).toBe("a period at the end");
    expect(offends("Введите пароль и код.")).toBe("a period at the end");
    expect(offends("Игра уйдет наставнику на проверку.")).toBe(
      "a period at the end",
    );
  });

  it("reads every component and module of the client, documents aside", () => {
    const read = disk().files;

    expect(read).toContain("shared/ui/Layout/Dialog.vue");
    expect(read).toContain("widgets/sidebar/GamePanel.vue");
    expect(read).toContain("pages/donate/DonatePage.vue");
    expect(read).not.toContain("sentencePeriods.spec.ts");
    expect(read.some((file) => file.startsWith("pages/legal/"))).toBe(false);
    expect(read.some((file) => file.startsWith("pages/rules/"))).toBe(false);
  });

  /**
   * Each place is asserted on files written out here rather than on real
   * components: a component that gets its copy fixed would take the proof of
   * the fix down with it.
   */
  describe("reads where the rule speaks and nowhere else", () => {
    /** A single-file component, written out as the two blocks it is. */
    const component = (markup: string, body = "") =>
      [
        `<script setup lang="ts">`,
        body,
        `</script>`,
        `<template>`,
        markup,
        `</template>`,
      ].join("\n");

    /** Every offending line of one component, or of a few side by side. */
    const caught = (files: Record<string, string>) =>
      offenders(treeOf(files)).map((line) => line.split(" - ").pop());

    const one = (markup: string, body = "") =>
      caught({ "pages/Page.vue": component(markup, body) });

    it("leaves a paragraph of a page alone", () => {
      expect(
        one(
          `<p>Если вам что-то непонятно в правилах, обратитесь к администрации.</p>` +
            `<secondary-text>Удаление игры необратимо.</secondary-text>` +
            `<input placeholder="Введите имя." />`,
        ),
      ).toEqual([]);
    });

    it("reads a list item wherever it stands", () => {
      expect(
        one(
          `<ul><li>Участвуйте в обсуждениях.</li>` +
            `<li>Первое. Второе.</li>` +
            `<li>Смотрите <router-link to="/x">правила</router-link> сайта.</li></ul>`,
        ),
      ).toEqual(["Участвуйте в обсуждениях.", "Смотрите правила сайта."]);
    });

    it("reads everything inside a window", () => {
      expect(
        one(
          `<Dialog><p>Письмо отправлено.</p>` +
            `<secondary-text>Удаление необратимо.</secondary-text>` +
            `<p>{{ error || "Ссылка недействительна." }}</p>` +
            `<input placeholder="Введите имя." /></Dialog>` +
            `<ConfirmDialog title="Удалить игру?" message="Игра будет удалена." />` +
            `<div role="dialog"><span>Сохранено.</span></div>`,
        ),
      ).toEqual([
        "Письмо отправлено.",
        "Удаление необратимо.",
        "Ссылка недействительна.",
        "Введите имя.",
        "Игра будет удалена.",
        "Сохранено.",
      ]);
    });

    it("follows what a window is handed into the script", () => {
      expect(
        one(
          `<p>Страница</p><ConfirmDialog :show="open" :title="pending?.title ?? ''" :message="note" />`,
          [
            `const note = computed(() => "Статус сменится сразу.");`,
            `const ACTIONS = { kill: { title: "Убить", message: "Персонаж погибнет." } };`,
            `const unrelated = "Не про окно.";`,
          ].join("\n"),
        ).sort(),
      ).toEqual(["Статус сменится сразу."]);
      expect(
        one(
          `<p>Страница</p><ConfirmDialog :message="pending?.message ?? ''" />`,
          `const ACTIONS = { kill: { message: "Персонаж погибнет." } };`,
        ),
      ).toEqual(["Персонаж погибнет."]);
    });

    it("reads the text of a hint that pops up, and not the control it hangs on", () => {
      expect(
        one(
          `<Tooltip text="Скопировать ссылку."><p>Ссылка на пост.</p></Tooltip>` +
            `<Tooltip><button>x</button><template #content>Открыть в новой вкладке.</template></Tooltip>`,
        ),
      ).toEqual(["Скопировать ссылку.", "Открыть в новой вкладке."]);
    });

    it("reads a component drawn inside a window, script and all", () => {
      expect(
        caught({
          "pages/Page.vue": component(`<Dialog><InnerForm /></Dialog>`),
          "features/InnerForm.vue": component(
            `<p>Пароль спрашивается еще раз.</p>`,
            `const failed = "Не удалось сохранить.";`,
          ),
          "pages/Other.vue": component(`<p>Абзац страницы.</p>`),
        }).sort(),
      ).toEqual(["Не удалось сохранить.", "Пароль спрашивается еще раз."]);
    });

    it("reads a component that is a window whole, script and all", () => {
      expect(
        one(
          `<Dialog title="Бан"><p>Причина видна нарушителю</p></Dialog>`,
          `const REFUSED = "Не удалось выдать бан.";`,
        ),
      ).toEqual(["Не удалось выдать бан."]);
    });

    it("reads the lines of a toast", () => {
      expect(
        one(
          `<p>Готово</p>`,
          [
            `const toast = useToast();`,
            `toast.success("Письмо отправлено.");`,
            `notifyFailure(error, "Не удалось сохранить.");`,
          ].join("\n"),
        ).sort(),
      ).toEqual(["Не удалось сохранить.", "Письмо отправлено."]);
    });

    /** A comment is not copy, whichever block it stands in. */
    it("does not read the comments", () => {
      expect(
        one(
          `<Dialog><!-- <p>Письмо отправлено.</p> --><p>Готово</p></Dialog>`,
          `// Письмо отправлено.\n/* Письмо отправлено. */`,
        ),
      ).toEqual([]);
    });
  });
});

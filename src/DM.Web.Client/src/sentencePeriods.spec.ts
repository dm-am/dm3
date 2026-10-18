/**
 * @vitest-environment node
 */

/**
 * A single sentence carries no period at its end in a list item and in
 * anything that pops up over the page: a window, a confirmation, a plate, a
 * hint that opens on hover, a panel that drops down. Everywhere else the
 * period is the writer's to keep.
 *
 * The rule as the owner narrowed it on 26 September 2026. A full stop closes a
 * thought that has a neighbour. A bullet and a line in a window have none: the
 * period there is a keystroke the reader pays no attention to. A paragraph of
 * a page that reads among other paragraphs does have neighbours, and without
 * its period it reads as cut off next to them - "Если вам что-то непонятно в
 * правилах..." is the example he gave. The first version of this gate took the
 * period off everything, the pages about the project and about helping it
 * among them, and those paragraphs have it back.
 *
 * Two sentences in one block keep theirs, including the last one: take it away
 * and the block reads as an unfinished second thought. Three things are not a
 * sentence ending and are left alone: an ellipsis, which says the screen is
 * waiting for an answer; an abbreviation, whose period belongs to the word;
 * and a value a template drops into the middle of a line, where the sentence
 * count is not knowable from the source.
 *
 * WHAT IS READ.
 *
 *   - every <li> of every template;
 *   - everything inside a popup element: the windows (POPUP_TAGS), anything
 *     marked with a popup role (POPUP_ROLES), the text of a Tooltip and its
 *     content slot - but not its default slot, which is the control it hangs
 *     on. Inside a popup a line is read wherever it stands: a paragraph, a
 *     grey line, a branch of a v-if, an attribute, a literal inside `{{ }}`;
 *   - a component drawn inside a popup of another file, whole, and so is a
 *     component whose template is nothing but a popup: its script is the copy
 *     of that window, its fallbacks and its refusals;
 *   - a bound attribute of a popup, followed into the script the way the toast
 *     gates follow a call: a constant, a computed, and the field an object
 *     writes it into (`:message="pending?.message"` reads every `message:` of
 *     the file);
 *   - every line a toast can put on screen, by the scan the toast gates use.
 *
 * WHAT IS NOT. A paragraph of a page, a grey line under a heading, a hint under
 * a field, an empty state: those are the writer's call. The documents (pages
 * of the user agreement, the privacy policy and the rules) are left out
 * whole, their lists included: they are texts of record, written as clauses.
 *
 * The check reads the templates rather than a mounted screen: it is the source
 * a person edits, and a paragraph split across three lines by the formatter is
 * still one sentence.
 */
import { describe, expect, it } from "vitest";
import { readdirSync, readFileSync } from "node:fs";
import { dirname, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { parse as parseSfc } from "vue/compiler-sfc";
import {
  scanOf,
  sites,
  treeOf,
  type Tree,
} from "@/shared/lib/testing/toastScan";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

/**
 * A word whose own period is not the end of anything. The whole word, not the
 * tail of one: "пропадет." ends in the letters of "т." and is a sentence, and
 * matching the tail let every line whose last word ended in one of the
 * one-letter abbreviations ("т", "д", "п", "г", "ч") walk past.
 */
const ABBREVIATION =
  /(?:^|[^а-яА-Яa-zA-Z])(сек|мин|ч|г|гг|т|д|п|др|см|стр|р|мод)\.$/i;

/** A second sentence: a stop, a question or a bang with a space after it. */
const SENTENCE_BREAK = /[.!?]\s/g;

/** Copy, as opposed to a path, a class name or a version. */
const CYRILLIC = /[а-яА-Я]/;

/** A value dropped into a line: `{{ … }}` in a template, `${…}` in a literal. */
const INTERPOLATION = /\{\{[\s\S]*?\}\}|\$\{[^}]*\}/g;

/**
 * A quoted string as the source spells it out. Anything with a backslash in it
 * is left out rather than unescaped: an escape is not copy this project
 * writes, and a half-parsed string would be read as ending where it does not.
 */
const QUOTED = new RegExp(
  ['"([^"\\\\\\n]*)"', "'([^'\\\\\\n]*)'", "`([^`\\\\]*)`"].join("|"),
  "g",
);

/** The windows of the site, by the tag a template draws them with. */
const POPUP_TAGS = new Set(["Dialog", "ConfirmDialog", "AuthDialog"]);

/**
 * A home-made popup says what it is by its role: the pinned-topics window, the
 * subscription panel, the drawer, a drop-down menu or list, a tooltip.
 */
const POPUP_ROLES = new Set([
  "dialog",
  "alertdialog",
  "menu",
  "listbox",
  "tooltip",
]);

/** The hint that opens on hover: its text is a popup, its default slot is not. */
const TOOLTIP = "Tooltip";

/**
 * Bindings that carry no words: whether the window is open, what its button
 * runs, how it is laid out. Everything else a popup is handed is followed.
 */
const SILENT_BINDINGS = new Set([
  "show",
  "model-value",
  "modelValue",
  "action",
  "loading",
  "disabled",
  "key",
  "class",
  "style",
  "editor",
  "compact",
  "inline",
  "dirty",
  "auto",
]);

/** A file whose copy a reader meets: a component, or a module of the app. */
const isSource = (name: string): boolean =>
  name.endsWith(".vue") ||
  (name.endsWith(".ts") &&
    !name.endsWith(".spec.ts") &&
    !name.endsWith(".d.ts"));

/**
 * The pages that are documents rather than screens: the user agreement, the
 * privacy policy and the rules of the site. Only these two folders: the page
 * about the project, the testimonials and the page about helping it are
 * screens, and their lists are read like any other.
 */
const DOCUMENTS = ["pages/legal", "pages/rules"];

/** The client's sources, by paths from src with "/" between the parts. */
const disk = (): Tree => {
  const found: string[] = [];
  const walk = (dir: string) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const full = resolve(dir, entry.name);
      const path = relative(CLIENT_SRC, full).split("\\").join("/");
      if (entry.isDirectory()) {
        if (
          entry.name !== "node_modules" &&
          !DOCUMENTS.includes(path) &&
          path !== "shared/lib/testing"
        )
          walk(full);
      } else if (isSource(entry.name)) found.push(path);
    }
  };
  walk(CLIENT_SRC);
  return {
    files: found,
    read: (file) => readFileSync(resolve(CLIENT_SRC, file), "utf8"),
  };
};

/** One line of copy, with the whitespace of the formatter squeezed out. */
const flatten = (text: string): string => text.replace(/\s+/g, " ").trim();

/** Every Russian string a fragment of source spells out. */
const spelled = (source: string): string[] =>
  [...source.matchAll(QUOTED)]
    .map(([, double, single, tick]) =>
      flatten((double ?? single ?? tick ?? "").replace(INTERPOLATION, " ")),
    )
    .filter((text) => CYRILLIC.test(text));

/** Code with its comments taken out. */
const uncommented = (code: string): string =>
  code.replace(/\/\*[\s\S]*?\*\//g, "").replace(/^\s*\/\/.*$/gm, "");

/** The `<script>` of a single-file component, with its comments taken out. */
const script = (source: string): string => {
  const opens = source.indexOf("<script");
  const closes = source.indexOf("</script>");
  if (opens < 0 || closes < 0) return "";
  return uncommented(source.slice(source.indexOf(">", opens) + 1, closes));
};

/** @vue/compiler-core NodeTypes - the members of a parsed template read here. */
const ELEMENT = 1;
const TEXT = 2;
const INTERPOLATED = 5;
const ATTRIBUTE = 6;
const DIRECTIVE = 7;

/** The slice of the template AST this walk reads; the rest is ignored. */
interface TemplateNode {
  type: number;
  /** ELEMENT only. */
  tag?: string;
  /** TEXT only. */
  content?: string;
  /** INTERPOLATED only: the expression as written, braces included. */
  loc?: { source: string };
  props?: {
    type: number;
    name: string;
    value?: { content: string };
    arg?: { content: string };
    exp?: { content: string };
  }[];
  children?: TemplateNode[];
}

/**
 * Tags that run inside a line rather than make one: the line they stand in
 * is read whole, with their words in it.
 */
const INLINE = new Set([
  "a",
  "b",
  "br",
  "code",
  "em",
  "i",
  "router-link",
  "RouterLink",
  "s",
  "small",
  "span",
  "strong",
  "u",
]);

/** The words of an element, a nested list aside: its items are lines of their own. */
const wordsOf = (node: TemplateNode): string => {
  if (node.type === TEXT) return node.content ?? "";
  if (node.type === INTERPOLATED) return " ";
  if (node.type === ELEMENT && node.tag !== "ul" && node.tag !== "ol")
    return (node.children ?? []).map(wordsOf).join("");
  return "";
};

/** PascalCase for a tag written either way: confirm-dialog is ConfirmDialog. */
const pascal = (tag: string): string =>
  tag.replace(/(^|-)([a-z])/g, (_, __, c: string) => c.toUpperCase());

const attribute = (node: TemplateNode, name: string): string | undefined =>
  node.props?.find((prop) => prop.type === ATTRIBUTE && prop.name === name)
    ?.value?.content;

const isPopup = (node: TemplateNode): boolean =>
  node.type === ELEMENT &&
  (POPUP_TAGS.has(pascal(node.tag ?? "")) ||
    POPUP_ROLES.has(attribute(node, "role") ?? ""));

/** The slot a `<template #name>` fills, or undefined for any other node. */
const slotOf = (node: TemplateNode): string | undefined =>
  node.tag === "template"
    ? node.props?.find(
        (prop) => prop.type === DIRECTIVE && prop.name === "slot",
      )?.arg?.content
    : undefined;

/** What one template says in the places the rule speaks about. */
interface Read {
  /** Lines of copy, as the reader meets them. */
  lines: string[];
  /** Expressions a popup is handed, to be followed into the script. */
  bound: string[];
  /** Components drawn inside a popup, by the tag they are drawn with. */
  drawn: string[];
  /** Whether the template is nothing but a popup. */
  whole: boolean;
}

/**
 * Walks a template and keeps what stands in a list item or in a popup.
 *
 * `inside` says the whole template is popup content already: a component the
 * site draws only inside a window.
 */
const readTemplate = (source: string, inside = false): Read => {
  const read: Read = { lines: [], bound: [], drawn: [], whole: false };
  const { descriptor } = parseSfc(source, { filename: "component.vue" });
  const root = descriptor.template?.ast as unknown as TemplateNode | undefined;
  if (!root) return read;

  const elements = (root.children ?? []).filter(
    (node) => node.type === ELEMENT,
  );
  read.whole = elements.length > 0 && elements.every(isPopup);

  /** The copy of a node's own attributes: a plain one is the line. */
  const attributes = (node: TemplateNode, popupRoot: boolean) => {
    for (const prop of node.props ?? []) {
      if (prop.type === ATTRIBUTE) {
        const value = prop.value?.content ?? "";
        if (CYRILLIC.test(value) && prop.name !== "role")
          read.lines.push(flatten(value));
      } else if (prop.type === DIRECTIVE && prop.name === "bind" && prop.exp) {
        read.lines.push(...spelled(prop.exp.content));
        const name = prop.arg?.content ?? "";
        if (popupRoot && !SILENT_BINDINGS.has(name))
          read.bound.push(prop.exp.content);
      }
    }
  };

  /**
   * A node's line, when it holds one of its own: a paragraph, a list item, or
   * any element with words of its own and nothing but inline tags beside
   * them. An inline tag holds a line only where no element around it did -
   * a <span> of words straight inside a popup is that popup's line.
   */
  const lineOf = (node: TemplateNode, covered: boolean): string | undefined => {
    const children = node.children ?? [];
    const inline = INLINE.has(node.tag ?? "");
    const holds =
      node.tag === "p" ||
      node.tag === "li" ||
      ((!inline || !covered) &&
        children.some(
          (c) => c.type === TEXT && CYRILLIC.test(c.content ?? ""),
        ) &&
        children.every((c) => c.type !== ELEMENT || INLINE.has(c.tag ?? "")));
    return holds ? flatten(wordsOf(node)) : undefined;
  };

  const visit = (node: TemplateNode, popup: boolean, covered = false): void => {
    if (node.type === INTERPOLATED) {
      if (popup) read.lines.push(...spelled(node.loc?.source ?? ""));
      return;
    }
    if (node.type !== ELEMENT) {
      (node.children ?? []).forEach((child) => visit(child, popup, covered));
      return;
    }

    const opens = !popup && isPopup(node);
    const here = popup || opens;
    const tag = node.tag ?? "";
    let line: string | undefined;

    if (here) {
      attributes(node, opens);
      line = lineOf(node, covered);
      if (line) read.lines.push(line);
      if (/^[A-Z]/.test(tag) || tag.includes("-")) read.drawn.push(pascal(tag));
    } else if (tag === "li") {
      line = lineOf(node, covered);
      if (line) read.lines.push(line);
      for (const held of (node.children ?? []).filter(
        (c) => c.type === INTERPOLATED,
      ))
        read.lines.push(...spelled(held.loc?.source ?? ""));
    }
    const under = covered || line !== undefined;

    if (!here && pascal(tag) === TOOLTIP) {
      // The text is what pops up; the default slot is the control it hangs
      // on, and stays on the page.
      const text = attribute(node, "text");
      if (text && CYRILLIC.test(text)) read.lines.push(flatten(text));
      for (const prop of node.props ?? [])
        if (prop.type === DIRECTIVE && prop.arg?.content === "text" && prop.exp)
          read.bound.push(prop.exp.content);
      for (const child of node.children ?? [])
        visit(child, slotOf(child) === "content");
      return;
    }

    (node.children ?? []).forEach((child) => visit(child, here, under));
  };

  visit(root, inside);
  return read;
};

/**
 * The last name of each member chain in an expression: `message` of
 * `pending?.confirm?.message ?? ''`, the field whose values the popup reads.
 */
const members = (expression: string): string[] =>
  [
    ...expression
      .replace(QUOTED, '""')
      .matchAll(/(?:\?\.|\.)\s*([A-Za-z_$][\w$]*)(?![\w$]|\s*(?:\.|\?\.|\())/g),
  ]
    .map(([, name]) => name)
    .filter((name) => name !== "value" && name !== "length");

/** Every line of a tree the rule has something to say about, by file. */
const copyOf = (tree: Tree): Map<string, string[]> => {
  const scan = scanOf(tree);
  const out = new Map<string, string[]>();
  const add = (file: string, lines: string[]) =>
    out.set(file, [...(out.get(file) ?? []), ...lines]);

  // The components a tag can stand for, by the name of their file.
  const components = new Map<string, string[]>();
  for (const file of tree.files.filter((f) => f.endsWith(".vue"))) {
    const name = file.slice(file.lastIndexOf("/") + 1, -".vue".length);
    components.set(name, [...(components.get(name) ?? []), file]);
  }

  // Popup content, found template by template until nothing new turns up:
  // a component drawn inside a window is part of that window, and so is
  // whatever it draws in turn.
  const inside = new Set<string>();
  const reads = new Map<string, Read>();
  let grew = true;
  while (grew) {
    grew = false;
    for (const file of tree.files.filter((f) => f.endsWith(".vue"))) {
      const read = readTemplate(scan.read(file), inside.has(file));
      reads.set(file, read);
      const whole = inside.has(file) || read.whole;
      if (read.whole && !inside.has(file)) {
        inside.add(file);
        grew = true;
      }
      for (const tag of read.drawn)
        for (const drawn of components.get(tag) ?? [])
          if (!inside.has(drawn) && (whole || tag !== "")) {
            inside.add(drawn);
            grew = true;
          }
    }
  }

  for (const [file, read] of reads) {
    add(file, read.lines);
    // A popup's copy that its template only names.
    for (const expression of read.bound) {
      add(file, scan.valuesOf(expression, file));
      for (const field of members(expression))
        add(file, scan.fieldValues(file, field));
    }
    // A component that is a window whole: its script is that window's copy.
    if (inside.has(file)) add(file, spelled(script(scan.read(file))));
  }

  // The plates. The same scan the toast gates measure them with, so a line
  // is read here wherever its words are written.
  for (const file of tree.files)
    for (const site of sites(scan.read(file)))
      add(file, scan.valuesOf(site.argument, file));

  return out;
};

/** What is wrong with a line, or an empty string when nothing is. */
const offends = (text: string): string => {
  if (!text) return "";
  if (text.endsWith("...")) return "";
  if (!text.endsWith(".") || ABBREVIATION.test(text)) return "";
  const body = text.slice(0, -1);
  // Two sentences keep the period of the last one, or the block reads as an
  // unfinished second thought. The rule asks the writer to avoid getting
  // there - two thoughts go in two paragraphs, the second of them grey - but
  // a line that pairs a question with its answer is one line on screen and
  // needs no period, so this check speaks only about the period itself.
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

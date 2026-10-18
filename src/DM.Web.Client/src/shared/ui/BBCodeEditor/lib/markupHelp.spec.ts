/**
 * @vitest-environment jsdom
 */

/**
 * The help of the markup editor says what the grammar says, or it teaches the
 * author to type what the page will not show. It had: its example of an image
 * carried alt="кот на окне", a parameter the grammar has no more, so the one
 * line meant to show the right form printed as text on the page.
 *
 * So each half of every row is held to the grammar here. The typed half is
 * markup the check passes in silence and the preview reads as its tag. The
 * shown half is written by hand, as the server's reading markup, and it has to
 * say what the preview of the typed half says: the same words, the caption the
 * spoiler was given, the names the hidden block was written for, the size the
 * image was held to. And a chord the help names is one the editor binds in
 * both of its modes: named from the visual mode alone, it did nothing in the
 * BBCode one.
 */
import { afterEach, describe, expect, it } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { nextTick } from "vue";
import { existsSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import type { Editor } from "@tiptap/vue-3";
import {
  bbcodeToHtml,
  CONTEXT_TAGS,
  validateBBCode,
  type BBCodeContext,
} from "@/shared/lib/utils/bbcode";
import { compileSass, styleRules } from "@/shared/lib/testing/styles";
import BBCodeEditor from "../BBCodeEditor.vue";
import { HELP_ENTRIES, chordLabel, typedPieces } from "./markupHelp";

enableAutoUnmount(afterEach);

/** The folder the site serves at its root. */
const PUBLIC = join(
  dirname(fileURLToPath(import.meta.url)),
  "../../../../../public",
);

// The contexts of the editor. The hint, the description of a game tag, has no
// help of its own: its one tag is written by the buttons of the tag's window.
const CONTEXTS = (Object.keys(CONTEXT_TAGS) as BBCodeContext[]).filter(
  (context) => context !== "hint",
);

/** The surfaces whose editor offers the tag, and so shows its row. */
const surfacesOf = (tag: string): BBCodeContext[] =>
  CONTEXTS.filter((context) => CONTEXT_TAGS[context].includes(tag));

const examples = HELP_ENTRIES.flatMap((entry) =>
  entry.examples.map((example) => ({ tag: entry.tag, ...example })),
);

const fragment = (html: string): HTMLElement => {
  const root = document.createElement("div");
  root.innerHTML = html;
  return root;
};

const NO_BREAK_SPACE = String.fromCharCode(0xa0);

/**
 * The words of a fragment, as a reader reads them: a block ends a word, as the
 * item of a list does, whether a line break stands after it or not.
 */
const words = (root: HTMLElement): string => {
  root.querySelectorAll("li, p, div").forEach((block) => block.append(" "));
  return (root.textContent ?? "")
    .replaceAll(NO_BREAK_SPACE, " ")
    .replace(/\s+/g, " ")
    .trim();
};

/** The parts of the page's markup that exist for a reader and not in the text. */
const READER_PARTS = ".spoiler-head, .nsfw-head";

describe("the rows of the help", () => {
  it("name every tag the grammar reads, the item of a list inside its list", () => {
    const named = new Set(HELP_ENTRIES.map((entry) => entry.tag));
    for (const context of CONTEXTS) {
      const missing = CONTEXT_TAGS[context].filter(
        (tag) => tag !== "li" && !named.has(tag),
      );
      expect(missing, context).toEqual([]);
    }
    for (const tag of ["ul", "ol"]) {
      const entry = HELP_ENTRIES.find((one) => one.tag === tag)!;
      expect(entry.examples[0].typed).toContain("[li]");
    }
  });

  it("stand in the order of the toolbar", () => {
    expect(HELP_ENTRIES.map((entry) => entry.tag)).toEqual([
      "b",
      "i",
      "u",
      "strike",
      "spoiler",
      "nsfw",
      "private",
      "ul",
      "ol",
      "img",
      "link",
      "mention",
      "quote",
      "mod",
      "tab",
      "code",
      "noparse",
    ]);
  });

  // The shown half may carry alt="": it is the server's image, and an empty
  // alt tells a screen reader the picture is decoration.
  it("carry nothing the grammar has dropped: no alt, no line of underscores", () => {
    for (const entry of HELP_ENTRIES) {
      const typed = entry.examples.map((example) => example.typed);
      const shown = entry.examples.map((example) =>
        example.shown.replaceAll('alt=""', ""),
      );
      const all = [entry.note ?? "", ...typed, ...shown].join("\n");
      expect(all, entry.tag).not.toMatch(/\balt\b/i);
      expect(all, entry.tag).not.toContain("___");
    }
  });
});

describe("what the help says to type", () => {
  it.each(examples)(
    "$typed passes the check in silence wherever $tag is offered",
    ({ tag, typed }) => {
      for (const context of surfacesOf(tag)) {
        expect(validateBBCode(typed, context), context).toEqual([]);
      }
    },
  );

  it.each(examples)(
    "$typed is read as [$tag], not as text",
    ({ tag, typed }) => {
      for (const context of surfacesOf(tag)) {
        const preview = fragment(bbcodeToHtml(typed, { context }));
        expect(
          preview.querySelector(`[data-bb-tag="${tag}"]`),
          `${context}: ${preview.innerHTML}`,
        ).not.toBeNull();
      }
    },
  );

  it("wraps only between two tags, and copies as it was typed", () => {
    for (const { typed } of examples) {
      const pieces = typedPieces(typed);
      expect(pieces.join("")).toBe(typed);
      for (const piece of pieces.slice(0, -1)) {
        expect(piece.endsWith("]")).toBe(true);
      }
    }
  });
});

describe("what the help says a reader gets", () => {
  it.each(examples)(
    "$typed shows the words its preview shows",
    ({ tag, typed, shown }) => {
      const [context] = surfacesOf(tag);
      const preview = fragment(bbcodeToHtml(typed, { context }));
      const page = fragment(shown);
      page.querySelectorAll(READER_PARTS).forEach((part) => part.remove());
      expect(words(page)).toBe(words(preview));
    },
  );

  it.each(examples.filter((one) => one.tag === "spoiler"))(
    "$typed puts its caption on the head",
    ({ typed, shown }) => {
      const block = fragment(bbcodeToHtml(typed)).querySelector(
        '[data-bb-tag="spoiler"]',
      )!;
      const head = fragment(shown).querySelector(".spoiler-head")!;
      expect(head.textContent).toBe(
        block.getAttribute("data-bb-text") ?? "Показать содержимое",
      );
    },
  );

  // The leads read every hidden block of the game, so its recipients name them
  // first, and the names the block was written for after them. They ride on
  // the block for the tooltip and are no text of the line.
  it.each(examples.filter((one) => one.tag === "private"))(
    "$typed carries its readers on the block, the leads first",
    ({ typed, shown }) => {
      const block = fragment(
        bbcodeToHtml(typed, { context: "post" }),
      ).querySelector('[data-bb-tag="private"]')!;
      const names = (block.getAttribute("data-bb-character") ?? "")
        .split(",")
        .map((name) => name.trim())
        .filter(Boolean);
      const piece = fragment(shown).querySelector(".private-message")!;
      expect(piece.getAttribute("data-recipients")).toBe(
        ["DungeonMaster", "Assistant", ...names].join(", "),
      );
      expect(piece.textContent).toBe("тайна");
      expect(fragment(shown).textContent).toBe("тайна");
    },
  );

  // The first picture was the favicon, and a page that names /favicon.ico as
  // its icon did not load it as a picture: the cell stood empty. Each result
  // is a picture the site serves from its own public folder.
  it.each(examples.filter((one) => one.tag === "img"))(
    "$typed shows a picture the site serves",
    ({ shown }) => {
      const src = fragment(shown).querySelector("img")!.getAttribute("src")!;
      expect(src).not.toContain("favicon");
      expect(existsSync(join(PUBLIC, src))).toBe(true);
    },
  );

  it.each(examples.filter((one) => one.tag === "link"))(
    "$typed shows its own text, or the word the server puts there",
    ({ typed, shown }) => {
      const preview = fragment(bbcodeToHtml(typed)).querySelector("a")!;
      const page = fragment(shown).querySelector("a")!;
      expect(page.textContent).toBe(
        preview.getAttribute("data-bb-text") ?? "ссылка",
      );
      expect(page.getAttribute("href")).toBe(preview.getAttribute("href"));
    },
  );

  it.each(examples.filter((one) => one.tag === "img"))(
    "$typed holds the picture to the size it names",
    ({ typed, shown }) => {
      const preview = fragment(bbcodeToHtml(typed)).querySelector(
        ".bb-image-frame",
      );
      const page = fragment(shown).querySelector(".bb-image-frame");
      expect(page?.getAttribute("style") ?? null).toBe(
        preview?.getAttribute("style") ?? null,
      );
    },
  );

  // The page holds a picture of the text to a height, DM2's, and opens a
  // taller one whole on a click. The help says both, with the number the
  // stylesheet draws to, so the two cannot drift apart.
  it("says how tall the page draws a picture, and that a click opens it whole", () => {
    const sheet = compileSass(
      [
        '@use "@/assets/styles/BbcodeContent" as *',
        ".bbcode-content",
        "  +bbcode-content",
      ].join("\n"),
    );
    const cap = styleRules(sheet).find((rule) =>
      rule.selector
        .split(",")
        .map((one) => one.trim())
        .includes(".bbcode-content img"),
    )?.declarations["max-height"];
    const pixels = /^var\(--bb-image-max-height, (\d+)px\)$/.exec(
      cap ?? "",
    )?.[1];
    expect(pixels, cap).toBeDefined();

    const note = HELP_ENTRIES.find((entry) => entry.tag === "img")!.note!;
    expect(note).toContain(`не выше ${pixels} пикселей`);
    expect(note).toContain("щелчок по ней открывает ее целиком");
  });
});

describe("the keys the help names", () => {
  const chorded = HELP_ENTRIES.filter((entry) => entry.keys).map((entry) => ({
    tag: entry.tag,
    label: chordLabel(entry.keys!),
    letter: entry.keys!.letter,
    shift: entry.keys!.shift ?? false,
  }));

  it("are the four the editor has in both modes", () => {
    expect(chorded.map((one) => one.label)).toEqual([
      "Ctrl+B",
      "Ctrl+I",
      "Ctrl+U",
      "Ctrl+Shift+S",
    ]);
  });

  const keydown = (letter: string, shift: boolean) =>
    new KeyboardEvent("keydown", {
      key: shift ? letter.toUpperCase() : letter,
      code: `Key${letter.toUpperCase()}`,
      ctrlKey: true,
      shiftKey: shift,
      bubbles: true,
      cancelable: true,
    });

  it.each(chorded)(
    "$label writes [$tag] in the BBCode mode",
    async ({ tag, letter, shift }) => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "test text" },
      });
      await flushPromises();
      await wrapper.findAll(".mode-tab")[0].trigger("click");
      await nextTick();
      const textarea = wrapper.get(".bbcode-textarea");
      (textarea.element as HTMLTextAreaElement).setSelectionRange(0, 4);

      textarea.element.dispatchEvent(keydown(letter, shift));
      await nextTick();

      const emitted = wrapper.emitted("update:modelValue")!;
      expect(emitted[emitted.length - 1][0]).toBe(`[${tag}]test[/${tag}] text`);
    },
  );

  it.each(chorded)(
    "$label writes [$tag] in the visual mode",
    async ({ tag, letter, shift }) => {
      const wrapper = mount(BBCodeEditor, { props: { modelValue: "текст" } });
      await flushPromises();
      const editor = (wrapper.vm as unknown as { editor: Editor }).editor;
      editor.commands.selectAll();

      editor.view.dom.dispatchEvent(keydown(letter, shift));
      await flushPromises();

      const emitted = wrapper.emitted("update:modelValue")!;
      expect(emitted[emitted.length - 1][0]).toBe(`[${tag}]текст[/${tag}]`);
    },
  );
});

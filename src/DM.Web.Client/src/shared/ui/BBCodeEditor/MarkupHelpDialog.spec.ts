/**
 * @vitest-environment jsdom
 */

/**
 * The help of the markup editor as the author meets it: the "help" button of
 * the toolbar opens it in a window of the site, and the window shows a row to
 * each tag the surface reads, the typed markup beside what a reader gets.
 *
 * It used to be a panel of its own over the page, neither a window nor part of
 * the page: no focus of its own, no Escape, and styles written for the whole
 * document, so the look of one line of it lay on every line.
 *
 * The library under the window is a box that holds the card while the window
 * is open, as it does; how the window closes, holds focus and dims the page is
 * Dialog.spec.ts's and dialogSurface.spec.ts's to say.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h, nextTick } from "vue";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import {
  clearRegistry,
  hasAny,
} from "@/shared/lib/composables/useExpandableRegistry";
import BBCodeEditor from "./BBCodeEditor.vue";
import { HELP_ENTRIES } from "./lib/markupHelp";

vi.mock("vue-final-modal", () => ({
  VueFinalModal: defineComponent({
    name: "VueFinalModal",
    props: {
      contentClass: { type: String, default: "" },
      modelValue: { type: Boolean, default: false },
    },
    setup:
      (props, { slots }) =>
      () =>
        props.modelValue
          ? h(
              "div",
              { class: ["vfm-content", props.contentClass] },
              slots.default?.(),
            )
          : null,
  }),
}));

enableAutoUnmount(afterEach);

beforeEach(() => clearRegistry());

const HERE = dirname(fileURLToPath(import.meta.url));

type Props = Record<string, unknown>;

const editor = (props: Props = {}) =>
  mount(BBCodeEditor, {
    attachTo: document.body,
    props: { modelValue: "", ...props },
  });

type Wrapper = ReturnType<typeof editor>;

const helpButton = (w: Wrapper) =>
  w.get('button[aria-label="Показать справку"]');

async function openHelp(w: Wrapper) {
  await helpButton(w).trigger("click");
  await flushPromises();
  await nextTick();
}

const rowsOf = (w: Wrapper) =>
  w.findAll("tbody.help-entry").map((row) => row.attributes("data-tag"));

describe("the help window", () => {
  it("is not built until the author asks for it", () => {
    const w = editor();
    expect(w.findComponent({ name: "VueFinalModal" }).exists()).toBe(false);
    expect(helpButton(w).attributes("aria-haspopup")).toBe("dialog");
  });

  it("opens as a window of the site at the editor's measure, with its heading", async () => {
    const w = editor();
    await openHelp(w);

    const card = w.get(".dialog");
    expect(card.classes()).toContain("dialog--editor");
    expect(card.get("h2").text()).toBe("Справка по тегам");
    expect(card.find("table.help-table").exists()).toBe(true);
    expect(card.findAll("thead th").map((th) => th.text())).toEqual([
      "Тег",
      "Как написать",
      "Что получится",
      "Клавиши",
    ]);
  });

  it("closes by its cross and by Escape, and opens again", async () => {
    const w = editor();
    await openHelp(w);
    await w.get(".dialog > .dialog-close").trigger("click");
    await nextTick();
    expect(w.find(".help-table").exists()).toBe(false);

    await openHelp(w);
    await w.get(".vfm-content").trigger("keydown", { key: "Escape" });
    await nextTick();
    expect(w.find(".help-table").exists()).toBe(false);

    await openHelp(w);
    expect(w.find(".help-table").exists()).toBe(true);
  });

  it("leaves nothing of the panel it replaced", () => {
    const source = readFileSync(join(HERE, "BBCodeEditor.vue"), "utf8");
    expect(source).not.toMatch(/help-(dialog|grid|item|section|footer)/);
    expect(source).not.toContain(":global(");
  });
});

describe("the rows of the help", () => {
  it("are the tags of a game post, the hidden block among them", async () => {
    const w = editor({ context: "post", isModerator: true });
    await openHelp(w);
    const rows = rowsOf(w);
    expect(rows).toContain("private");
    expect(rows).not.toContain("mod");
  });

  it("offer [mod] to a moderator where the surface reads it, and to no one else", async () => {
    const moderator = editor({ context: "common", isModerator: true });
    await openHelp(moderator);
    expect(rowsOf(moderator)).toContain("mod");
    expect(rowsOf(moderator)).not.toContain("private");

    const reader = editor({ context: "common" });
    await openHelp(reader);
    expect(rowsOf(reader)).not.toContain("mod");

    const direct = editor({ context: "direct", isModerator: true });
    await openHelp(direct);
    expect(rowsOf(direct)).not.toContain("mod");
    expect(rowsOf(direct)).not.toContain("private");
  });

  it("name the draft button only where there is a draft to load", async () => {
    const plain = editor();
    await openHelp(plain);
    expect(rowsOf(plain)).not.toContain("load");

    const drafted = editor({ draftKey: "topic-1" });
    await openHelp(drafted);
    expect(rowsOf(drafted)).toContain("load");
  });

  it("promise Ctrl+Enter only where the form sends on it", async () => {
    const silent = editor();
    await openHelp(silent);
    expect(silent.find(".help-foot").exists()).toBe(false);

    const sending = editor({ onSubmit: () => {} });
    await openHelp(sending);
    expect(sending.get(".help-foot").text()).toBe(
      "Ctrl+Enter отправляет написанное",
    );
  });

  // A note laid across the typed and the shown columns ended the rule between
  // them halfway, and its text ran over the line of the cells above it. Every
  // row covers the four columns, each cell one column wide, and a note stands
  // in the cell of the last example of its tag.
  it("keep the four cells of the table in every row, a note in its example's cell", async () => {
    const w = editor({ context: "post", draftKey: "topic-1" });
    await openHelp(w);

    const table = w.get(".help-table").element as HTMLTableElement;
    for (const section of Array.from(table.tBodies)) {
      const carried: number[] = [];
      for (const row of Array.from(section.rows)) {
        let covered = carried.filter((left) => left > 0).length;
        for (let at = 0; at < carried.length; at++) carried[at]--;
        for (const cell of Array.from(row.cells)) {
          expect(cell.colSpan, cell.textContent ?? "").toBe(1);
          covered += 1;
          if (cell.rowSpan > 1) carried.push(cell.rowSpan - 1);
        }
        expect(covered, section.dataset.tag).toBe(4);
      }
    }

    for (const entry of HELP_ENTRIES.filter(
      (one) => one.note && one.tag !== "mod",
    )) {
      const rows = w.findAll(`tbody[data-tag="${entry.tag}"] tr`);
      expect(rows).toHaveLength(entry.examples.length);
      const notes = w.findAll(`tbody[data-tag="${entry.tag}"] .help-note`);
      expect(notes.map((note) => note.text())).toEqual([entry.note]);
      expect(
        rows[rows.length - 1].find(".help-example .help-note").exists(),
      ).toBe(true);
    }
  });

  it("show the typed markup as it is typed, and the keys beside it", async () => {
    const w = editor({ context: "post" });
    await openHelp(w);

    const typed = w
      .findAll(".help-typed")
      .map((cell) => cell.element.textContent);
    const expected = HELP_ENTRIES.filter(
      (entry) => entry.tag !== "mod",
    ).flatMap((entry) => entry.examples.map((example) => example.typed));
    expect(typed).toEqual(expected);

    const bold = w.get('tbody[data-tag="b"]');
    expect(bold.get(".help-tag").text()).toBe("b");
    expect(bold.get(".help-keys").text()).toBe("Ctrl+B");
    expect(w.get('tbody[data-tag="spoiler"] .help-keys').text()).toBe(
      "Ctrl+Shift+S",
    );
  });
});

describe("what a reader gets, in the help", () => {
  it("opens a spoiler as the page does", async () => {
    const w = editor();
    await openHelp(w);

    const head = w.get('tbody[data-tag="spoiler"] .spoiler-head');
    expect(head.attributes("role")).toBe("button");
    expect(head.text()).toBe("Показать содержимое");
    await head.trigger("click");
    expect(head.text()).toBe("Скрыть содержимое");
    expect(w.get('tbody[data-tag="spoiler"] .bb-collapse').classes()).toContain(
      "open",
    );
  });

  it("goes nowhere from the links of its examples", async () => {
    const w = editor();
    await openHelp(w);

    for (const tag of ["link", "mention"]) {
      const anchor = w.get(`tbody[data-tag="${tag}"] .help-shown a`);
      expect(anchor.attributes("tabindex")).toBe("-1");
      const click = new MouseEvent("click", {
        bubbles: true,
        cancelable: true,
      });
      anchor.element.dispatchEvent(click);
      expect(click.defaultPrevented, tag).toBe(true);
    }
  });

  it("lets go of the page's 'expand all' when it closes", async () => {
    const w = editor();
    expect(hasAny.value).toBe(false);
    await openHelp(w);
    expect(hasAny.value).toBe(true);

    await w.get(".dialog > .dialog-close").trigger("click");
    await flushPromises();
    expect(hasAny.value).toBe(false);
  });
});

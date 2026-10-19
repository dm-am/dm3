/**
 * @vitest-environment jsdom
 */

/**
 * The markup editor as a whole: it renders, registers each extension of the
 * visual mode once, hands its parent its methods, takes what it is given
 * without breaking, and stands the same at every width.
 *
 * Its parts are held in specs of their own beside it: the toolbar
 * (BBCodeEditorToolbar.spec.ts), the mode switch, the status line, the markup
 * check and the help, and in lib/ the insertions, the draft and the keys.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, enableAutoUnmount } from "@vue/test-utils";
import { nextTick } from "vue";
import type { Editor } from "@tiptap/vue-3";
import BBCodeEditor from "./BBCodeEditor.vue";
import { styleRules } from "@/shared/lib/testing/styles";
import { editorCss, editorShell } from "@/shared/lib/testing/editorShell";

// Unmount every wrapper after each test so the component destroys its
// tiptap editor (see onUnmounted in BBCodeEditor.vue). Leaked editors
// keep ProseMirror DOMObserver flush timers pending, which can fire
// after jsdom teardown and fail the run with an unhandled error.
enableAutoUnmount(afterEach);

const { clean, reset } = editorShell();

describe("BBCodeEditor", () => {
  beforeEach(clean);
  afterEach(reset);

  describe("Basic Rendering", () => {
    it("renders editor content area", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      expect(wrapper.find(".editor-content").exists()).toBe(true);
    });
  });

  describe("Extension registry", () => {
    it("registers each extension once and leaves anchors to BbLink", async () => {
      const warnings: string[] = [];
      const warn = vi
        .spyOn(console, "warn")
        .mockImplementation((...args: unknown[]) => {
          warnings.push(args.map(String).join(" "));
        });

      const wrapper = mount(BBCodeEditor, { props: { modelValue: "" } });
      await nextTick();

      const editor = (wrapper.vm as unknown as { editor: Editor }).editor;
      const names = editor.extensionManager.extensions.map((e) => e.name);

      // StarterKit brings an `underline` and a `link` of its own, and this
      // editor registers a replacement for each. The underline collides by
      // name and tiptap says so out loud; the link does not collide by name,
      // so nothing warns — its parse rule is a bare `a[href]` and it outranks
      // BbLink's, which is how `data-bb-text` and `data-bb-selfref` came to be
      // dropped on the way into the document, autolink included. Both are off
      // in the kit's options, and this is the gate that keeps them off.
      expect(
        names.filter((name, index) => names.indexOf(name) !== index),
        "the kit registers an extension this editor also registers: turn the kit's copy off in StarterKit.configure",
      ).toEqual([]);
      expect(
        warnings.filter((line) => line.includes("Duplicate extension names")),
      ).toEqual([]);
      expect(Object.keys(editor.schema.marks)).toContain("bbLink");
      expect(
        Object.keys(editor.schema.marks),
        "the stock link mark parses every anchor and discards the BBCode attributes BbLink carries",
      ).not.toContain("link");

      warn.mockRestore();
    });
  });

  describe("Accessibility", () => {
    it('has role="region" on container', () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      expect(wrapper.find(".bbcode-editor").attributes("role")).toBe("region");
    });

    it("has aria-label on container", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "" },
      });
      expect(wrapper.find(".bbcode-editor").attributes("aria-label")).toBe(
        "Редактор BBCode",
      );
    });
  });

  describe("Disabled State", () => {
    it("applies disabled class when disabled", () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", disabled: true },
      });

      expect(wrapper.find(".bbcode-editor").classes()).toContain("disabled");
    });

    it("disables textarea when disabled", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "", disabled: true },
      });
      await nextTick();

      const textarea = wrapper.find(".bbcode-textarea");
      expect(textarea.attributes("disabled")).toBeDefined();
    });
  });

  describe("Exposed Methods", () => {
    it("clear method emits update:modelValue with empty string", async () => {
      const wrapper = mount(BBCodeEditor, {
        props: { modelValue: "test" },
      });

      wrapper.vm.clear();
      await nextTick();

      expect(wrapper.emitted("update:modelValue")).toBeTruthy();
      const emitted = wrapper.emitted("update:modelValue");
      expect(emitted![emitted!.length - 1]).toEqual([""]);
    });
  });
});

/**
 * The editor is one at every width, and its text stands on the left.
 *
 * - "0 слов | 0 / 50000 символов" under the field stands on the left, its
 *   first letter on the field's edge.
 * - The row of tags stands on the field's edges, its first button and the
 *   right end of the switch.
 * - The hint of an empty field takes the height of its lines, and the field
 *   grows under it: a float of no height in a box one line high cut a hint of
 *   two lines to a strip of the tops of its letters.
 *
 * Read off the stylesheet, each rule with the @media it stands in: jsdom lays
 * nothing out. That the browsers lay it out so is the live stand's to show.
 */
describe("BBCodeEditor at every width", () => {
  type Rule = {
    media: string | null;
    selector: string;
    declarations: Record<string, string>;
  };

  /** The flat compiled sheet, rule by rule, each with the @media over it. */
  const sheet: Rule[] = (() => {
    const css = editorCss();
    const rules: Rule[] = [];
    for (const [, condition, body] of css.matchAll(
      /@media\s*([^{]+)\{((?:[^{}]*\{[^{}]*\})*)\s*\}/g,
    )) {
      for (const rule of styleRules(body))
        rules.push({ media: condition.trim(), ...rule });
    }
    const outside = css.replace(
      /@media\s*([^{]+)\{((?:[^{}]*\{[^{}]*\})*)\s*\}/g,
      "",
    );
    for (const rule of styleRules(outside))
      rules.push({ media: null, ...rule });
    return rules;
  })();

  const rulesOf = (selector: string) =>
    sheet.filter((rule) => rule.selector === selector);

  /** The left and right of a padding shorthand or of its own sides. */
  const sides = (declarations: Record<string, string>) => {
    const parts = (declarations.padding ?? "").split(/\s+/).filter(Boolean);
    const [, right = parts[0], , left = right] = parts;
    return {
      left: declarations["padding-left"] ?? left,
      right: declarations["padding-right"] ?? right,
    };
  };

  const flush = (value: string | undefined) =>
    value === undefined || value === "0" || value === "0px";

  it("sets the counter line on the left at every width", () => {
    const counter = rulesOf(".status-bar");
    expect(counter.length).toBeGreaterThan(0);
    for (const { media, declarations } of counter) {
      expect(
        [undefined, "left", "start"],
        `.status-bar under ${media ?? "every width"}`,
      ).toContain(declarations["text-align"]);
    }
  });

  it("starts the counter line on the field's edge at every width", () => {
    for (const { media, declarations } of rulesOf(".status-bar")) {
      const { left } = sides(declarations);
      expect(flush(left), `.status-bar under ${media ?? "every width"}`).toBe(
        true,
      );
    }
  });

  it("stands the row of tags on the field's edges at every width", () => {
    const toolbar = rulesOf(".editor-toolbar");
    expect(toolbar.length).toBeGreaterThan(0);
    for (const { media, declarations } of toolbar) {
      const { left, right } = sides(declarations);
      const where = `.editor-toolbar under ${media ?? "every width"}`;
      expect(flush(left), where).toBe(true);
      expect(flush(right), where).toBe(true);
    }
  });

  it("resizes the field from its corner, in one rule for every reader", () => {
    expect(
      rulesOf(".bbcode-editor.resizable").map((rule) => [
        rule.media,
        rule.declarations.resize,
      ]),
    ).toEqual([[null, "vertical"]]);
  });

  it("gives the hint of an empty field the height of its lines and the caret the start of the line", () => {
    const hint = rulesOf(
      ".wysiwyg-content :deep(.tiptap) p.is-editor-empty:first-child::before",
    );
    expect(hint).toHaveLength(1);
    const [{ declarations }] = hint;

    // Of no height, the hint overflowed a box one line high, which cut it.
    expect(declarations).not.toHaveProperty("height");
    // It floats beside the empty line and takes none of its width: a hint as
    // wide as the line, its right margin taking that width back, so the caret
    // stands at the start of the line it shares with the hint's first line.
    expect(declarations).toMatchObject({
      float: "left",
      width: "100%",
      "margin-right": "-100%",
    });
    // The editable box holds the float, so all of the hint is the field to
    // tap, its second line too, and not the box around it.
    expect(
      rulesOf(".wysiwyg-content :deep(.tiptap)").map(
        (rule) => rule.declarations.display,
      ),
    ).toContain("flow-root");
  });
});

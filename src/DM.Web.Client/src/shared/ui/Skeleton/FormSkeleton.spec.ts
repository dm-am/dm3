/**
 * @vitest-environment jsdom
 */

/**
 * The skeleton of a form stands where the form will (Form with FormField): a
 * row is as far from the next as FormField's, its caption over its field by
 * FormField's gap, the caption's blank in the caption's font, the field and
 * the buttons at the height of a control, the buttons as far under the fields
 * as Form puts them.
 *
 * Read off the stylesheets of all three: the numbers live in components that
 * change apart.
 */
import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import {
  compileSass,
  componentCss,
  ruleMap,
} from "@/shared/lib/testing/styles";
import FormSkeleton from "./FormSkeleton.vue";

const HERE = dirname(fileURLToPath(import.meta.url));
const skeleton = ruleMap(componentCss(resolve(HERE, "FormSkeleton.vue")));
const field = ruleMap(componentCss(resolve(HERE, "../Form/FormField.vue")));
const form = ruleMap(componentCss(resolve(HERE, "../Form/Form.vue")));
const toolbar = ruleMap(
  componentCss(resolve(HERE, "../BBCodeEditor/BBCodeEditorToolbar.vue")),
);
const modeSwitch = ruleMap(
  componentCss(resolve(HERE, "../BBCodeEditor/EditorModeSwitch.vue")),
);
const control = ruleMap(
  compileSass('@use "@/assets/styles/Inputs" as *\n.probe\n  +input'),
);

describe("the skeleton of a form", () => {
  it("spaces its rows and its captions as FormField does", () => {
    const labeled = field.get(".form-field__labeled");
    expect(skeleton.get(".skeleton-field")?.margin).toBe(labeled?.margin);
    expect(skeleton.get(".skeleton-field")?.gap).toBe(labeled?.gap);
  });

  it("sets the caption's blank in the caption's font", () => {
    const caption = field.get(".form-field-label");
    for (const property of ["font-size", "font-weight"]) {
      expect(skeleton.get(".skeleton-caption")?.[property], property).toBe(
        caption?.[property],
      );
    }
  });

  it("draws the field and the buttons at a control's height, under the fields as Form puts them", () => {
    const line = [...control.entries()].find(([selector]) =>
      selector.includes(":not(textarea"),
    )?.[1];
    expect(line?.height).toBeDefined();
    expect(skeleton.get(".skeleton-control")?.height).toBe(line?.height);
    expect(skeleton.get(".skeleton-button")?.height).toBe(line?.height);
    expect(skeleton.get(".skeleton-actions")?.["margin-top"]).toBe(
      form.get(".controls")?.["margin-top"],
    );
  });

  it("draws the fields asked for and two buttons, hidden from assistive reading", () => {
    const wrapper = mount(FormSkeleton, { props: { fields: 4 } });

    expect(wrapper.findAll(".skeleton-field")).toHaveLength(4);
    expect(wrapper.findAll(".skeleton-button")).toHaveLength(2);
    expect(wrapper.attributes("aria-hidden")).toBe("true");
  });

  // The edit of a publication: the editor replaces the skeleton without a jump.
  it("draws an editor as the last field, its toolbar line over its box", () => {
    expect(skeleton.get(".skeleton-toolbar")?.padding).toBe(
      toolbar.get(".editor-toolbar")?.padding,
    );
    expect(skeleton.get(".skeleton-toolbar")?.["font-size"]).toBe(
      modeSwitch.get(".mode-tabs")?.["font-size"],
    );
    for (const property of ["padding", "font-size", "display"]) {
      expect(skeleton.get(".skeleton-tab")?.[property], property).toBe(
        modeSwitch.get(".mode-tab")?.[property],
      );
    }
    expect(skeleton.get(".skeleton-tab")?.["border-bottom"]).toMatch(
      new RegExp(
        `^${modeSwitch.get(".mode-tabs")?.["border-bottom"]?.split(" ")[0]}`,
      ),
    );

    const wrapper = mount(FormSkeleton, {
      props: { fields: 3, editorHeight: 240 },
    });
    const rows = wrapper.findAll(".skeleton-field");
    expect(rows.map((row) => row.find(".skeleton-editor").exists())).toEqual([
      false,
      false,
      true,
    ]);
    // The text area and the editor's border of 1px on either side.
    expect(
      (wrapper.get(".skeleton-editor-box").element as HTMLElement).style.height,
    ).toBe("242px");
  });
});

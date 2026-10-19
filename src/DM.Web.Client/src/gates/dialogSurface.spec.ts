/**
 * @vitest-environment jsdom
 */

/**
 * The window surface: the card, the cross, the heading, the fields and the row
 * of buttons that the window with a form (Layout/Dialog.vue) and the
 * confirmation (ConfirmDialog) share through _DialogSurface.sass. Most of it is
 * a rule and not a node, and jsdom applies no scoped styles, so those are read
 * off the stylesheets compiled the way the build compiles them and asked of
 * markup built the way the components render it; the half that is a node is
 * asked of a mount. The fields are dialogFields.spec.ts's, the row of buttons
 * dialogButtons.spec.ts's. The same form on a page keeps the page's measure,
 * and the last block says so from the same sheets.
 */
import { afterEach, describe, it, expect, vi } from "vitest";
import { mount } from "@vue/test-utils";
import { readFileSync } from "node:fs";
import {
  compileSass,
  componentCss,
  ruleMap,
  sassBlocks,
  scopedComponentCss,
  styleRules,
  type Declarations,
} from "@/shared/lib/testing/styles";
import { cascaded, matching } from "@/shared/lib/testing/cascade";
import {
  AUTH,
  CONFIRM,
  DIALOG,
  FIELD,
  FORM,
  SCOPE,
  TITLE,
  control,
  renderField,
} from "@/shared/lib/testing/windowMarkup";
import Dialog from "@/shared/ui/Layout/Dialog.vue";

vi.mock("vue-final-modal", async () =>
  (await import("@/shared/lib/testing/vueFinalModalStub")).vueFinalModal(),
);

afterEach(() => {
  document.body.innerHTML = "";
});

describe("the window with a form", () => {
  const sheet = () => ruleMap(componentCss(DIALOG));

  it("is 580 wide, and 920 when it holds the BBCode editor", () => {
    expect(sheet().get(".dialog")?.width).toBe("580px");
    // At 920 the editor's toolbar and its [bbcode]/wysiwyg switch stand in one
    // row.
    expect(sheet().get(".dialog--editor")?.width).toBe("920px");
  });

  it("has no narrow measure any more", () => {
    // A window of four fields at a narrow measure reads as a column.
    const selectors = styleRules(componentCss(DIALOG)).map((r) => r.selector);
    expect(selectors.filter((one) => one.includes("narrow"))).toEqual([]);
  });

  it("is the guest card: the page's fill, 24 of gutter, the site's radius, no frame, no shadow", () => {
    const card = sheet().get(".dialog");

    expect(card?.padding).toBe("24px");
    expect(card?.["box-sizing"]).toBe("border-box");
    expect(card?.["background-color"]).toBe("var(--bg-page)");
    expect(card?.["border-radius"]).toBe("8px");
    expect(card?.border).toBeUndefined();
    expect(card?.["box-shadow"]).toBeUndefined();
  });

  it("puts the cross in the corner", () => {
    const cross = sheet().get(".dialog > .dialog-close");

    expect([cross?.top, cross?.right, cross?.width, cross?.height]).toEqual([
      "10px",
      "10px",
      "32px",
      "32px",
    ]);
  });

  it("draws the cross, and it closes the window the way Escape does", async () => {
    const wrapper = mount(Dialog, { slots: { default: "<p>Тело</p>" } });
    const cross = wrapper.get(".dialog > button.dialog-close");

    expect(cross.attributes("aria-label")).toBe("Закрыть");
    // The bars are the stylesheet's; a glyph inside would stand behind them.
    expect(cross.element.innerHTML).toBe("");

    await cross.trigger("click");
    expect(wrapper.emitted("close")).toHaveLength(1);
    expect(wrapper.emitted("update:modelValue")).toEqual([[false]]);

    await wrapper.get(".vfm-content").trigger("keydown", { key: "Escape" });
    expect(wrapper.emitted("close")).toHaveLength(2);
  });

  it("takes the editor measure when it is asked for", () => {
    const wrapper = mount(Dialog, { props: { editor: true } });
    expect(wrapper.get(".vfm-content > .dialog").classes()).toEqual([
      "dialog",
      "dialog--editor",
    ]);
  });

  it("scrolls a tall card whole, from its top, with nothing sticky in it", () => {
    // The card stands in a frame the size of the screen, and the frame is the
    // one thing that scrolls: the cross and the buttons travel with the body.
    const frame = sheet().get(".dialog-frame");
    expect(frame).toMatchObject({
      position: "absolute",
      inset: "0",
      display: "flex",
      "overflow-y": "auto",
      padding: "16px",
    });
    // Centred when it fits, started at the top when it does not: centring by
    // align-items would cut a tall card at both ends.
    expect(sheet().get(".dialog-frame > .dialog")?.margin).toBe("auto");
    expect(frame?.["align-items"]).toBeUndefined();

    for (const file of [DIALOG, TITLE, FORM, CONFIRM]) {
      expect(componentCss(file), file).not.toMatch(/position:\s*sticky/);
    }
  });

  it("dims the cross while the window waits, and leaves it in the tab order", () => {
    // aria-disabled and not the attribute: the caret waits on the cross while
    // the pressed button is shut.
    const shut = sheet().get(".dialog > .dialog-close[aria-disabled=true]");
    expect(shut?.opacity).toBe("0.5");
    expect(shut?.cursor).toBe("default");
    const confirm = ruleMap(componentCss(CONFIRM)).get(
      ".dialog-close[aria-disabled=true]",
    );
    expect(confirm?.opacity).toBe("0.5");
  });

  it("leaves the guest card to itself", () => {
    // AuthDialog passes `auto`, and its card has a cross of its own.
    const wrapper = mount(Dialog, { props: { auto: true } });
    expect(wrapper.find("button.dialog-close").exists()).toBe(false);
  });
});

describe("the heading of a window", () => {
  const SITE_HEADING = {
    "font-size": "16px",
    "font-weight": "700",
    "text-transform": "uppercase",
    "letter-spacing": "0.5px",
    color: "var(--heading)",
  };

  const pick = (rule: Declarations | undefined) =>
    Object.fromEntries(
      Object.keys(SITE_HEADING).map((name) => [name, rule?.[name]]),
    );

  it("stands on the left of the window with a form, clear of the cross", () => {
    const heading = ruleMap(componentCss(TITLE)).get("h2");

    expect(pick(heading)).toEqual(SITE_HEADING);
    expect(heading?.["text-align"]).toBeUndefined();
    expect(heading?.["padding-right"]).toBe("24px");
  });

  it("stands in the centre of the confirmation, in the same style", () => {
    const sheet = ruleMap(componentCss(CONFIRM));

    expect(pick(sheet.get(".dialog-title"))).toEqual(SITE_HEADING);
    expect(sheet.get(".dialog-title")?.padding).toBe("0 24px");
    expect(sheet.get(".dialog-container")?.["text-align"]).toBe("center");
  });
});

describe("one surface, one source", () => {
  it("is taken by both windows from _DialogSurface.sass", () => {
    const dialog = sassBlocks(DIALOG);
    const confirm = sassBlocks(CONFIRM);

    for (const mixin of ["+dialog-card", "+dialog-close"]) {
      expect(dialog, `Dialog.vue: ${mixin}`).toContain(mixin);
      expect(confirm, `ConfirmDialog.vue: ${mixin}`).toContain(mixin);
    }
    expect(sassBlocks(FORM)).toContain("+dialog-actions");
    expect(confirm).toContain("+dialog-actions");
    expect(sassBlocks(TITLE)).toContain("+dialog-title");
    expect(confirm).toContain("+dialog-title");
    expect(sassBlocks(FORM)).toContain("+dialog-refusal");
    expect(confirm).toContain("+dialog-refusal");
  });

  it("keeps the guest card's numbers", () => {
    // The surface writes the guest's numbers out instead of reading them from
    // AuthSurface.sass, which emits. This is what holds the two in step.
    // Compiled as the root sheet it is in the build (app/main.ts imports it),
    // so the prelude reaches it and its own relative @use resolves.
    const guest = ruleMap(compileSass(readFileSync(AUTH, "utf8"), AUTH));
    const window = ruleMap(componentCss(DIALOG));
    const field = scopedComponentCss(FIELD, SCOPE.field);

    const same = (names: string[], a?: Declarations, b?: Declarations) =>
      expect(names.map((n) => a?.[n])).toEqual(names.map((n) => b?.[n]));

    same(
      ["padding", "background-color", "border-radius"],
      window.get(".dialog"),
      guest.get(".auth-surface .auth-card"),
    );
    same(
      ["top", "right", "width", "height", "color", "border-radius"],
      window.get(".dialog > .dialog-close"),
      guest.get(".auth-surface .auth-close"),
    );
    same(
      ["width", "height", "margin"],
      window.get(
        ".dialog > .dialog-close::before, .dialog > .dialog-close::after",
      ),
      guest.get(
        ".auth-surface .auth-close::before, .auth-surface .auth-close::after",
      ),
    );

    const input = cascaded(
      field,
      control(renderField({ inDialog: true, control: "<input>" })),
    );
    expect(input.height).toBe(
      guest.get(".auth-surface .auth-field input")?.height,
    );
    const area = cascaded(
      field,
      control(
        renderField({ inDialog: true, control: "<textarea></textarea>" }),
      ),
    );
    expect(area.padding).toBe(
      guest.get(".auth-surface .auth-field textarea")?.padding,
    );
    const caption = cascaded(
      field,
      renderField({ inDialog: true, control: "<input>" }).querySelector(
        ".form-field-label",
      )!,
    );
    expect(caption["font-size"]).toBe(
      guest.get(".auth-surface .auth-label")?.["font-size"],
    );
  });
});

describe("the same form on a page", () => {
  it("keeps the page's field, caption and refusal, and ends as the window does", () => {
    const sheet = scopedComponentCss(FIELD, SCOPE.field);
    const field = renderField({
      inDialog: false,
      control: "<input>",
      error: true,
      hint: true,
    });

    const input = cascaded(sheet, control(field));
    expect(input.height).toBeUndefined();
    expect(input["font-size"]).toBeUndefined();
    // A refused field keeps its frame on a page as in a window: the red line
    // under it is the one carrier.
    const red = matching(sheet, control(field)).filter(({ declarations }) =>
      Object.values(declarations).some((value) => /red/.test(value)),
    );
    expect(red.map(({ selector }) => selector)).toEqual([]);

    // The caption of a page is 14 and bold; a window's is 15 and bold.
    const caption = cascaded(sheet, field.querySelector(".form-field-label")!);
    expect(caption["font-size"]).toBe("14px");
    expect(caption["font-weight"]).toBe("700");

    expect(cascaded(sheet, field)).toMatchObject({
      margin: "8px 0",
      gap: "4px",
    });
    expect(
      cascaded(sheet, field.querySelector(".form-field-error")!)["margin-top"],
    ).toBe("4px");
    expect(
      cascaded(sheet, field.querySelector(".form-field-hint")!)["margin-top"],
    ).toBe("4px");

    // The buttons stand in a row one step of 16 under the last field, 8
    // apart, with no grey strip across the foot of the card: the strip was
    // the page's second picture of a form's end.
    const form = scopedComponentCss(FORM, SCOPE.form);
    const root = document.createElement("form");
    root.setAttribute(SCOPE.form, "");
    root.innerHTML = `<div ${SCOPE.form} class="controls"><button ${SCOPE.form}>Сохранить</button></div>`;
    document.body.appendChild(root);
    const row = cascaded(form, root.querySelector(".controls")!);
    expect(row).toMatchObject({
      display: "flex",
      gap: "8px",
      "margin-top": "16px",
    });
    expect(row["background-color"]).toBeUndefined();
    expect(row.padding).toBeUndefined();
    // The page's own height, the button's, and not one the form decides.
    expect(
      cascaded(form, root.querySelector("button")!).height,
    ).toBeUndefined();
  });
});

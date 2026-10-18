/**
 * @vitest-environment jsdom
 */

/**
 * The window surface: the card, the cross, the heading, the fields and the row
 * of buttons that the window with a form (Layout/Dialog.vue) and the
 * confirmation (ConfirmDialog) share through _DialogSurface.sass.
 *
 * What the owner settled on 21 September 2026 is almost all of it a rule and
 * not a node: the 580 and 920 measures, the 52px field and button, the frame
 * that stays neutral when a field is refused, the line under a field that holds
 * no place in advance, the heading in capitals and the colour of a heading, no
 * grey footer. jsdom applies no scoped styles and vitest compiles none, so those
 * are read off the stylesheets compiled the way the build compiles them, scope
 * attribute included, and asked of markup built the way the components render
 * it. The half that is a node (the window tells its form it is in a window, the
 * cross closes it) is asked of a mount.
 *
 * The same form on a page keeps the page's measure, and the last block says so
 * from the same sheets: a rule of the window that also reached a page would be
 * a redesign of eighteen pages nobody asked for. What the two share since the
 * owner's standard of 27 September 2026 is written there too: a refused field
 * keeps its frame on a page as in a window, and neither ends in a grey strip.
 */
import { afterEach, describe, it, expect, vi } from "vitest";
import { defineComponent, h, nextTick } from "vue";
import { mount } from "@vue/test-utils";
import { readdirSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import {
  compileSass,
  componentCss,
  outranks,
  ruleMap,
  sassBlocks,
  scopedComponentCss,
  specificity,
  styleRules,
  type Declarations,
} from "@/shared/lib/testing/styles";
import { loadFace } from "@/shared/lib/testing/fontFace";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";

/**
 * The library tier under the window, reduced to what the window asks of it:
 * a content box that carries the class it is handed and renders the default
 * slot.
 */
vi.mock("vue-final-modal", () => ({
  VueFinalModal: defineComponent({
    props: { contentClass: { type: String, default: "" } },
    setup:
      (props, { slots }) =>
      () =>
        h(
          "div",
          { class: ["vfm-content", props.contentClass] },
          slots.default?.(),
        ),
  }),
}));

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));
const at = (path: string) => join(CLIENT_SRC, path);

const DIALOG = at("shared/ui/Layout/Dialog.vue");
const TITLE = at("shared/ui/Layout/DialogTitle.vue");
const FORM = at("shared/ui/Form/Form.vue");
const FIELD = at("shared/ui/Form/FormField.vue");
const CONFIRM = at("shared/ui/ConfirmDialog/ConfirmDialog.vue");
const BUTTON = at("shared/ui/Button/Button.vue");
const AUTH = at("assets/styles/AuthSurface.sass");
const RESET = at("assets/styles/Reset.sass");
const BOLD = at("assets/fonts/PTSans-Bold.ttf");

/** Stand-ins for the build's scope ids; compileStyle writes them verbatim. */
const SCOPE = {
  field: "data-v-field",
  form: "data-v-form",
  button: "data-v-button",
  confirm: "data-v-confirm",
};

/** Every rule of a compiled sheet that matches the element, in sheet order. */
const matching = (css: string, element: Element) =>
  styleRules(css)
    .flatMap(({ selector, declarations }) =>
      selector
        .split(",")
        .map((one) => one.trim())
        .filter((one) => one && !one.includes("::"))
        .map((one) => ({ selector: one, declarations })),
    )
    .filter(({ selector }) => {
      try {
        return element.matches(selector);
      } catch {
        return false;
      }
    });

/**
 * What the sheet declares for the element, the heavier selector winning and
 * sheet order breaking a tie, as the cascade decides it.
 */
const declared = (css: string, element: Element): Declarations => {
  const rules = matching(css, element)
    .map((rule, index) => ({
      ...rule,
      index,
      weight: specificity(rule.selector),
    }))
    .sort((a, b) =>
      outranks(a.weight, b.weight)
        ? 1
        : outranks(b.weight, a.weight)
          ? -1
          : a.index - b.index,
    );
  return Object.assign({}, ...rules.map((rule) => rule.declarations));
};

/**
 * A field as FormField renders it: its own boxes carry its scope id, the
 * control in the row is the caller's and carries none.
 */
const renderField = (options: {
  inDialog: boolean;
  control: string;
  error?: boolean;
  hint?: boolean;
}): HTMLElement => {
  const root = document.createElement("div");
  root.className = [
    "form-field",
    options.inDialog ? "form-field--dialog" : "",
    "form-field__labeled",
    options.error ? "error" : "",
  ]
    .filter(Boolean)
    .join(" ");
  const own = (html: string) =>
    html.replace(/<(\w+) class=/g, `<$1 ${SCOPE.field} class=`);
  root.innerHTML = own(
    `<div class="form-field-label"><label>Причина</label><span class="form-field-optional">необязательно</span></div>` +
      `<div class="form-field-row">CONTROL</div>` +
      (options.error
        ? `<span class="form-field-error">Слишком длинное значение</span>`
        : "") +
      (options.hint ? `<div class="form-field-hint">Подсказка</div>` : ""),
  ).replace("CONTROL", options.control);
  root.setAttribute(SCOPE.field, "");
  document.body.appendChild(root);
  return root;
};

/** The first control of a rendered field. */
const control = (field: HTMLElement, selector = "input, textarea, select") =>
  field.querySelector(selector) as HTMLElement;

afterEach(() => {
  document.body.innerHTML = "";
});

describe("the window with a form", () => {
  const sheet = () => ruleMap(componentCss(DIALOG));

  it("is 580 wide, and 920 when it holds the BBCode editor", () => {
    expect(sheet().get(".dialog")?.width).toBe("580px");
    // At 920 the editor's toolbar and its [bbcode]/wysiwyg switch stand in one
    // row: 828px of toolbar in 872 of content.
    expect(sheet().get(".dialog--editor")?.width).toBe("920px");
  });

  it("has no narrow measure any more", () => {
    // The owner removed it: a window of four fields at 380 read as a column.
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

describe("a form in a window", () => {
  it("is told by the window, and not by each caller", async () => {
    const Inside = defineComponent({
      render: () =>
        h(Dialog, null, () =>
          h(Form, { action: "Сохранить", cancel: "Отмена" }, () =>
            h(FormField, { label: "Название" }, () => h("input")),
          ),
        ),
    });
    const inside = mount(Inside);
    await nextTick();

    expect(inside.get("form").classes()).toContain("form--dialog");
    expect(inside.get(".form-field").classes()).toContain("form-field--dialog");

    // The same form with no window around it, and inside the guest's.
    const outside = mount(Form, {
      props: { action: "Сохранить" },
      slots: {
        default: () => h(FormField, { label: "Название" }, () => h("input")),
      },
    });
    expect(outside.get("form").classes()).not.toContain("form--dialog");
    expect(outside.get(".form-field").classes()).not.toContain(
      "form-field--dialog",
    );

    const Guest = defineComponent({
      render: () =>
        h(Dialog, { auto: true }, () =>
          h(FormField, { label: "Почта" }, () => h("input")),
        ),
    });
    expect(mount(Guest).get(".form-field").classes()).not.toContain(
      "form-field--dialog",
    );
  });

  const css = () => scopedComponentCss(FIELD, SCOPE.field);

  it("stands its fields and its lists at the window's control height", () => {
    const sheet = css();
    for (const markup of [
      "<input type='text'>",
      "<input type='number'>",
      "<div class='select-control'><select class='select-input'></select></div>",
    ]) {
      const field = renderField({ inDialog: true, control: markup });
      const own = declared(sheet, control(field));
      expect(own.height, markup).toBe("52px");
      expect(own["font-size"], markup).toBe("15px");
    }
  });

  it("leaves a checkbox its own box", () => {
    const field = renderField({
      inDialog: true,
      control:
        "<label class='checkbox-label'><input type='checkbox'> Активна</label>",
    });
    expect(declared(css(), control(field)).height).toBeUndefined();
  });

  it("leaves the BBCode editor as it is", () => {
    // The editor carries a textarea of its own deep inside; the window's rule
    // for a text area is for the one the row holds itself.
    const field = renderField({
      inDialog: true,
      control:
        "<div class='bbcode-editor-wrapper'><div class='bbcode-editor'><textarea class='bbcode-textarea'></textarea></div></div>",
    });
    const own = declared(css(), control(field));
    expect(own.padding).toBeUndefined();
    expect(own["font-size"]).toBeUndefined();
  });

  it("sets the caption bold at 15, 8 above the field, and the optional mark at 14", () => {
    const field = renderField({ inDialog: true, control: "<input>" });
    const caption = declared(css(), field.querySelector(".form-field-label")!);

    expect(caption["font-size"]).toBe("15px");
    expect(caption["font-weight"]).toBe("700");
    expect(caption["margin-bottom"]).toBe("8px");

    const optional = declared(
      css(),
      field.querySelector(".form-field-optional")!,
    );
    expect(optional["font-size"]).toBe("14px");
    expect(optional["font-weight"]).toBe("400");
    expect(optional.color).toBe("var(--text-muted)");
  });

  it("keeps the frame neutral when the field is refused", () => {
    const field = renderField({
      inDialog: true,
      control: "<textarea></textarea>",
      error: true,
    });
    const red = matching(css(), control(field)).filter(({ declarations }) =>
      Object.values(declarations).some((value) => /red/.test(value)),
    );
    expect(red.map(({ selector }) => selector)).toEqual([]);
  });

  it("says the refusal in red 6 under the field, holding no place for it", () => {
    const field = renderField({
      inDialog: true,
      control: "<input>",
      error: true,
      hint: true,
    });
    const refusal = declared(css(), field.querySelector(".form-field-error")!);

    expect(refusal.color).toBe("var(--accent-red)");
    expect(refusal["font-size"]).toBe("14px");
    expect(refusal["margin-top"]).toBe("6px");
    // No reserve: nothing keeps an empty line open under a field that is fine.
    expect(refusal["min-height"]).toBeUndefined();
    expect(refusal.visibility).toBeUndefined();

    const hint = declared(css(), field.querySelector(".form-field-hint")!);
    expect(hint["margin-top"]).toBe("6px");
    expect(hint.color).toBe("var(--text-muted)");
    expect(hint["font-size"]).toBe("14px");
  });

  it("steps 16 from one field to the next", () => {
    const field = renderField({ inDialog: true, control: "<input>" });
    const own = declared(css(), field);

    expect(own.margin).toBe("0 0 16px");
    expect(own.gap).toBe("0");
  });
});

describe("a field of a date, a time or a year in a window", () => {
  // DateInput draws its frame on a box of its own and not on the input, so
  // FormField's rule for the input of its row does not reach it; the time
  // beside a date does not stand right in the row. Each says the window's
  // measure itself, from the numbers of _DialogSurface.sass.
  it("stands the date's frame at the window's 52, its text at 15, 14 in", () => {
    const sheet = ruleMap(
      componentCss(at("shared/ui/DatePicker/DateInput.vue")),
    );
    expect(sheet.get(".date-input-control--dialog .di-field")?.height).toBe(
      "52px",
    );
    expect(sheet.get(".date-input-control--dialog .di-input")).toMatchObject({
      "font-size": "15px",
      "padding-left": "14px",
    });
  });

  it("stands the time and the year as FormField stands its own", () => {
    for (const [file, selector] of [
      ["shared/ui/DatePicker/TimeInput.vue", ".time-input--dialog"],
      ["shared/ui/DatePicker/YearInput.vue", ".year-input--dialog"],
    ]) {
      expect(ruleMap(componentCss(at(file))).get(selector), file).toMatchObject(
        { height: "52px", padding: "0 14px", "font-size": "15px" },
      );
    }
  });
});

describe("the row of buttons in a window", () => {
  const form = () => scopedComponentCss(FORM, SCOPE.form);
  const button = () => scopedComponentCss(BUTTON, SCOPE.button);

  /** The rule of the site's button that sets its own height. */
  const buttonHeight = () => {
    const rule = styleRules(button()).find(
      ({ selector, declarations }) =>
        declarations.height && selector.startsWith("button"),
    );
    if (!rule) throw new Error("Button.vue declares no height");
    return rule;
  };

  /** A button of the row as Form renders it: its own scope and the form's. */
  const renderRow = (inDialog: boolean) => {
    const root = document.createElement("form");
    root.setAttribute(SCOPE.form, "");
    if (inDialog) root.className = "form--dialog";
    root.innerHTML = `<div ${SCOPE.form} class="controls"><button ${SCOPE.form} ${SCOPE.button} class="primary">Сохранить</button><button ${SCOPE.form} ${SCOPE.button}>Отмена</button></div>`;
    document.body.appendChild(root);
    return root;
  };

  it("stands two equal halves 52 high, over the site's own 38", () => {
    const row = renderRow(true);
    const first = row.querySelector("button")!;
    const rule = matching(form(), first).find(
      ({ declarations }) => declarations.height,
    );

    expect(rule?.declarations.height).toBe("52px");
    expect(rule?.declarations.flex).toBe("1 1 0");
    // The site's button says 38 for itself; the window's rule has to outweigh
    // it, or the row stays 38 on screen whatever the sheet says.
    expect(buttonHeight().declarations.height).toBe("38px");
    expect(
      outranks(
        specificity(rule!.selector),
        specificity(buttonHeight().selector),
      ),
    ).toBe(true);
  });

  it("has no grey footer", () => {
    const row = renderRow(true);
    const own = declared(form(), row.querySelector(".controls")!);

    // No strip to take away: the form paints none, in a window or on a page.
    expect(own["background-color"]).toBeUndefined();
    expect(own.background).toBeUndefined();
    expect(own.padding).toBeUndefined();
    expect(own.margin).toBe("16px 0 0");
  });

  it("stands the confirmation's row the same way", () => {
    const sheet = scopedComponentCss(CONFIRM, SCOPE.confirm);
    const rule = styleRules(sheet).find(
      ({ selector, declarations }) =>
        selector.includes("dialog-actions") && declarations.height,
    );

    expect(rule?.declarations.height).toBe("52px");
    expect(rule?.declarations.flex).toBe("1 1 0");
    expect(
      outranks(
        specificity(rule!.selector),
        specificity(buttonHeight().selector),
      ),
    ).toBe(true);
  });

  it("paints nothing red but the refusal", () => {
    const REFUSAL: Record<string, string[]> = {
      [FORM]: [".form--dialog .form-refusal"],
      [CONFIRM]: [".dialog-refusal"],
    };
    for (const file of [DIALOG, TITLE, FORM, CONFIRM]) {
      const red = styleRules(componentCss(file))
        .filter(({ declarations }) =>
          Object.values(declarations).some((value) =>
            /var\(--[\w-]*red[\w-]*\)/.test(value),
          ),
        )
        .map(({ selector }) => selector);
      expect(red, file).toEqual(REFUSAL[file] ?? []);
    }
  });

  it("says a refusal in red 14 over the row, holding no place for it", () => {
    const form = ruleMap(componentCss(FORM)).get(".form--dialog .form-refusal");
    const confirm = ruleMap(componentCss(CONFIRM)).get(".dialog-refusal");
    for (const line of [form, confirm]) {
      expect(line).toMatchObject({
        margin: "0",
        color: "var(--accent-red)",
        "font-size": "14px",
      });
      // Nothing keeps an empty line open in a window that was not refused.
      expect(line?.["min-height"]).toBeUndefined();
      expect(line?.visibility).toBeUndefined();
    }
  });
});

describe("a caption of the row on a phone", () => {
  // A half of the row is as wide as the row makes it, so a caption longer than
  // its half has to wrap or it is cut at both edges: the warning window once
  // said "Выдать предупреждение" and stood cut on a 360 and a 375 screen, and
  // now says "Выдать". What decides it is text against a
  // width, and jsdom lays out neither, so the captions are measured here in the
  // face they are set in, against a half computed from the same sheets the
  // build compiles. The two screens are guestTitleFits.spec.ts's: 360, the
  // phone the site meets, and a desktop that leaves every card the width it
  // declares.
  const PHONE = 360;
  const DESKTOP = 1200;

  const face = loadFace(BOLD);

  const number = (value: string | undefined, what: string): number => {
    const found = Number.parseFloat(value ?? "");
    if (!Number.isFinite(found)) throw new Error(`не нашел в листе: ${what}`);
    return found;
  };

  /**
   * Compiling the sheets is the slow part of this block, and nothing it reads
   * changes from one case to the next, so each answer is worked out once.
   */
  const once = <T>(make: () => T) => {
    let value: T | undefined;
    return () => (value ??= make());
  };

  /** The row and a button of it, as the window renders them. */
  const renderRowOf = (window: "form" | "confirm") => {
    const root = document.createElement("div");
    root.innerHTML =
      window === "form"
        ? `<form ${SCOPE.form} class="form--dialog"><div ${SCOPE.form} class="controls"><button ${SCOPE.form} ${SCOPE.button} class="primary">Сохранить</button></div></form>`
        : `<div ${SCOPE.confirm} class="dialog-container"><div ${SCOPE.confirm} class="dialog-actions"><button ${SCOPE.confirm} ${SCOPE.button} class="primary">Удалить</button></div></div>`;
    document.body.appendChild(root);
    const own =
      window === "form"
        ? scopedComponentCss(FORM, SCOPE.form)
        : scopedComponentCss(CONFIRM, SCOPE.confirm);
    const both = own + scopedComponentCss(BUTTON, SCOPE.button);
    const row = root.querySelector(".controls, .dialog-actions")!;
    return {
      row: declared(own, row),
      button: declared(both, row.querySelector("button")!),
    };
  };
  const ROW = {
    form: once(() => renderRowOf("form")),
    confirm: once(() => renderRowOf("confirm")),
  };
  const rendered = (window: "form" | "confirm") => ROW[window]();

  /**
   * What a half of the row leaves its caption: the card at the width the
   * screen lets it have, less its gutters, the gap between the halves, the
   * button's frame and its side padding.
   */
  const room = (
    window: "form" | "confirm",
    card: Declarations | undefined,
    screen: number,
  ): number => {
    if (!card) throw new Error("не нашел в листе: карточка окна");
    const cap = card["max-width"]?.match(/^calc\(100vw - (\d+)px \* 2\)$/);
    if (!cap) throw new Error("не нашел в листе: поле карточки от края окна");
    const { row, button } = rendered(window);
    const width = Math.min(
      number(card.width, "ширина окна"),
      screen - 2 * Number(cap[1]),
    );
    const inside = width - 2 * number(card.padding, "поля окна");
    const half = (inside - number(row.gap, "зазор между кнопками")) / 2;
    const side = number(
      button.padding?.split(" ")[1] ?? button.padding,
      "поля кнопки",
    );
    return half - 2 * number(button.border, "рамка кнопки") - 2 * side;
  };

  const windowSheet = () => ruleMap(componentCss(DIALOG));
  const confirmCard = () =>
    ruleMap(scopedComponentCss(CONFIRM, SCOPE.confirm)).get(
      `.dialog-container[${SCOPE.confirm}]`,
    );

  const rooms = once(() => ({
    formPhone: room("form", windowSheet().get(".dialog"), PHONE),
    formDesktop: room("form", windowSheet().get(".dialog"), DESKTOP),
    editorDesktop: room(
      "form",
      {
        ...windowSheet().get(".dialog"),
        ...windowSheet().get(".dialog--editor"),
      },
      DESKTOP,
    ),
    confirmPhone: room("confirm", confirmCard(), PHONE),
    confirmDesktop: room("confirm", confirmCard(), DESKTOP),
  }));

  const size = (window: "form" | "confirm") =>
    number(rendered(window).button["font-size"], "кегль кнопки");

  /**
   * How many lines a caption takes in a given room: the words laid one after
   * another and a new line opened wherever the next one does not fit. Every
   * caption is measured in bold, the primary's weight: what fits in bold fits
   * in the regular of "Отмена" as well.
   */
  const lines = (text: string, px: number, width: number): number => {
    let count = 1;
    let line = "";
    for (const word of text.split(/\s+/)) {
      const next = line ? `${line} ${word}` : word;
      if (line && face.width(next, px, 0) > width) {
        count += 1;
        line = word;
      } else line = next;
    }
    return count;
  };

  /** Every .vue file of the client, its path relative to src. */
  const vueFiles = (): { file: string; source: string }[] => {
    const found: { file: string; source: string }[] = [];
    const walk = (dir: string) => {
      for (const entry of readdirSync(dir, { withFileTypes: true })) {
        const full = join(dir, entry.name);
        if (entry.isDirectory()) walk(full);
        else if (entry.name.endsWith(".vue"))
          found.push({
            file: full.slice(CLIENT_SRC.length + 1).replaceAll("\\", "/"),
            source: readFileSync(full, "utf8"),
          });
      }
    };
    walk(CLIENT_SRC);
    return found;
  };

  /**
   * The captions that can reach the row: the action and the cancel of a form
   * in a window with a form (the guest's, `auto`, has no such row), and the
   * labels handed to the confirmation, its own defaults included. A caption
   * assembled at runtime has no text here to measure and is left out.
   */
  const captions = () => {
    const found: {
      text: string;
      window: "form" | "confirm";
      editor: boolean;
      file: string;
    }[] = [];
    // By the tag and not by the import: app/providers registers the window
    // globally, and UnsavedChangesGuard imports the confirmation by a relative
    // path.
    for (const { file, source } of vueFiles()) {
      if (
        /<Dialog[\s>]/.test(source) &&
        !/<Dialog\b[^>]*\sauto\b/.test(source)
      ) {
        const editor = /<Dialog\b[^>]*\seditor\b/.test(source);
        for (const m of source.matchAll(/\s(?:action|cancel)="([^"]+)"/g))
          found.push({ text: m[1], window: "form", editor, file });
      }
      if (
        file === "shared/ui/ConfirmDialog/ConfirmDialog.vue" ||
        /<ConfirmDialog[\s>]/.test(source)
      ) {
        for (const m of source.matchAll(
          /\s(?:confirm-label|cancel-label)="([^"]+)"|(?:confirmLabel|cancelLabel):\s*"([^"]+)"/g,
        ))
          found.push({
            text: m[1] ?? m[2],
            window: "confirm",
            editor: false,
            file,
          });
      }
    }
    return found;
  };

  it("measures the way the browser does", () => {
    // Measured in headless Chrome on 21 September 2026 in the shipped face,
    // bold at 14px: 154.95 and 106.09; and on 28 September at 16px, at a
    // window of 360: "Опубликовать", the longest word of a caption today,
    // 99.2, and "предупреждение", which a caption no longer holds, 121.3.
    expect(face.width("Выдать предупреждение", 14, 0)).toBeCloseTo(154.95, 1);
    expect(face.width("предупреждение", 14, 0)).toBeCloseTo(106.09, 1);
    expect(face.width("Опубликовать", 16, 0)).toBeCloseTo(99.2, 0);
    expect(face.width("предупреждение", 16, 0)).toBeCloseTo(121.3, 0);
  });

  it("is set in the guest button's 16, not in the page's 14", () => {
    // The windows are drawn after the guest card (owner's call, 21 September
    // 2026), and their captions had stayed the page's 14 in a button of 52,
    // small beside the fields of 15 (owner's standard, 28 September 2026: a
    // window's control differs from a page's in its measure alone).
    const guest = ruleMap(compileSass(readFileSync(AUTH, "utf8"), AUTH));
    const guestButton = number(
      guest.get(".auth-surface .auth-button")?.["font-size"],
      "кегль гостевой кнопки",
    );
    expect(guestButton).toBe(16);
    expect(size("form")).toBe(guestButton);
    expect(size("confirm")).toBe(guestButton);
  });

  it("is judged in the half the screen leaves", () => {
    // On a phone both windows are the screen less its margins, 328, and a
    // half of that holds 118 once the frame and the padding are out.
    expect(rooms()).toEqual({
      formPhone: 118,
      formDesktop: 244,
      editorDesktop: 414,
      confirmPhone: 118,
      confirmDesktop: 130,
    });
  });

  it("wraps inside its half, and two lines of it stand inside the button", () => {
    const body = ruleMap(compileSass(readFileSync(RESET, "utf8"), RESET)).get(
      "body",
    );
    for (const window of ["form", "confirm"] as const) {
      const { button } = rendered(window);
      expect(button["white-space"], window).toBe("normal");
      // The leading of the text around the row, not the button's own 1: two
      // lines set solid read as one blot.
      expect(button["line-height"], window).toBe("inherit");
      const leading =
        number(body?.["line-height"], "интерлиньяж страницы") * size(window);
      const inner =
        number(button.height, "высота кнопки") -
        2 * number(button.border, "рамка кнопки");
      expect(2 * leading, window).toBeLessThanOrEqual(inner);
    }
  });

  it("finds every caption of both windows", () => {
    const all = captions();
    const forms = new Set(
      all.filter((c) => c.window === "form").map((c) => c.file),
    );
    expect(forms.size).toBeGreaterThanOrEqual(11);
    expect(all.map((c) => c.text)).toEqual(
      expect.arrayContaining(["Выдать", "Отмена", "Удалить"]),
    );
  });

  it("would catch a caption that does not fit", () => {
    // A single word longer than a phone's half: no wrapping saves it. At the
    // 16 of the row "предупреждение" is one, and the warning window's
    // "Выдать" is why the row can be 16.
    expect(face.width("Администрирование", size("form"), 0)).toBeGreaterThan(
      rooms().formPhone,
    );
    expect(face.width("предупреждение", size("form"), 0)).toBeGreaterThan(
      rooms().formPhone,
    );
  });

  const cases = [
    ...new Map(
      captions().map((c) => [
        `${c.window}|${c.editor}|${c.text}`,
        [c.text, c.window, c.editor] as const,
      ]),
    ).values(),
  ];

  it.each(cases)("%s (%s) stands whole in its half", (text, window, editor) => {
    const all = rooms();
    const px = size(window);
    const wraps = rendered(window).button["white-space"] === "normal";

    // On a phone it may take two lines, but no word of it may be cut.
    const phone = window === "form" ? all.formPhone : all.confirmPhone;
    if (wraps) {
      for (const word of text.split(/\s+/)) {
        expect(face.width(word, px, 0), `${word} / ${phone}`).toBeLessThan(
          phone,
        );
      }
      expect(lines(text, px, phone)).toBeLessThanOrEqual(2);
    } else {
      expect(face.width(text, px, 0), `${text} / ${phone}`).toBeLessThan(phone);
    }

    // On a desktop it stands on one line, as it did before it could wrap.
    const desktop =
      window === "confirm"
        ? all.confirmDesktop
        : editor
          ? all.editorDesktop
          : all.formDesktop;
    expect(face.width(text, px, 0)).toBeLessThan(desktop);
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

    const input = declared(
      field,
      control(renderField({ inDialog: true, control: "<input>" })),
    );
    expect(input.height).toBe(
      guest.get(".auth-surface .auth-field input")?.height,
    );
    const area = declared(
      field,
      control(
        renderField({ inDialog: true, control: "<textarea></textarea>" }),
      ),
    );
    expect(area.padding).toBe(
      guest.get(".auth-surface .auth-field textarea")?.padding,
    );
    const caption = declared(
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

    const input = declared(sheet, control(field));
    expect(input.height).toBeUndefined();
    expect(input["font-size"]).toBeUndefined();
    // A refused field keeps its frame on a page as in a window (owner's
    // standard, 27 September 2026): the red line under it is the one carrier.
    const red = matching(sheet, control(field)).filter(({ declarations }) =>
      Object.values(declarations).some((value) => /red/.test(value)),
    );
    expect(red.map(({ selector }) => selector)).toEqual([]);

    // The caption of a page is 14 and bold (owner's standard, 28 September
    // 2026): a window's is 15 and bold, a page's was regular beside it.
    const caption = declared(sheet, field.querySelector(".form-field-label")!);
    expect(caption["font-size"]).toBe("14px");
    expect(caption["font-weight"]).toBe("700");

    expect(declared(sheet, field)).toMatchObject({
      margin: "8px 0",
      gap: "4px",
    });
    expect(
      declared(sheet, field.querySelector(".form-field-error")!)["margin-top"],
    ).toBe("4px");
    expect(
      declared(sheet, field.querySelector(".form-field-hint")!)["margin-top"],
    ).toBe("4px");

    // The buttons stand in a row one step of 16 under the last field, 8
    // apart, with no grey strip across the foot of the card: the strip was
    // the page's second picture of a form's end.
    const form = scopedComponentCss(FORM, SCOPE.form);
    const root = document.createElement("form");
    root.setAttribute(SCOPE.form, "");
    root.innerHTML = `<div ${SCOPE.form} class="controls"><button ${SCOPE.form}>Сохранить</button></div>`;
    document.body.appendChild(root);
    const row = declared(form, root.querySelector(".controls")!);
    expect(row).toMatchObject({
      display: "flex",
      gap: "8px",
      "margin-top": "16px",
    });
    expect(row["background-color"]).toBeUndefined();
    expect(row.padding).toBeUndefined();
    // The page's own height, the button's, and not one the form decides.
    expect(
      declared(form, root.querySelector("button")!).height,
    ).toBeUndefined();
  });
});

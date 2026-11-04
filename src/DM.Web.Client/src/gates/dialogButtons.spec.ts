/**
 * @vitest-environment jsdom
 */

/**
 * The row of buttons in a window: two equal halves of 52 over the site's own
 * 38, no grey footer, nothing red but the refusal, and a caption that stands
 * whole in its half. The rest of the window is dialogSurface.spec.ts's.
 */
import { afterEach, describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import {
  compileSass,
  componentCss,
  outranks,
  ruleMap,
  scopedComponentCss,
  specificity,
  styleRules,
  type Declarations,
} from "@/shared/lib/testing/styles";
import { loadFace } from "@/shared/lib/testing/fontFace";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";
import { cascaded, matching } from "@/shared/lib/testing/cascade";
import {
  AUTH,
  BOLD,
  BUTTON,
  CONFIRM,
  DIALOG,
  FORM,
  RESET,
  SCOPE,
  TITLE,
} from "@/shared/lib/testing/windowMarkup";

afterEach(() => {
  document.body.innerHTML = "";
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
    const own = cascaded(form(), row.querySelector(".controls")!);

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

describe("a caption of the row", () => {
  // A half of the row is as wide as the row makes it, so a caption longer than
  // its half has to wrap or it is cut at both edges. jsdom lays out neither
  // text nor width, so the captions are measured here in the face they are set
  // in, against a half computed from the same sheets the build compiles.
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
      row: cascaded(own, row),
      button: cascaded(both, row.querySelector("button")!),
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
    formDesktop: room("form", windowSheet().get(".dialog"), DESKTOP),
    editorDesktop: room(
      "form",
      {
        ...windowSheet().get(".dialog"),
        ...windowSheet().get(".dialog--editor"),
      },
      DESKTOP,
    ),
    confirmDesktop: room("confirm", confirmCard(), DESKTOP),
  }));

  const size = (window: "form" | "confirm") =>
    number(rendered(window).button["font-size"], "кегль кнопки");

  /** Every .vue file of the client, its path relative to src. */
  const vueFiles = (): { file: string; source: string }[] =>
    collect(CLIENT_SRC, (name) => name.endsWith(".vue")).map((full) => ({
      file: asPath(full),
      source: readFileSync(full, "utf8"),
    }));

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
    // Measured in headless Chrome in the shipped face: bold at 14px and at
    // 16px.
    expect(face.width("Выдать предупреждение", 14, 0)).toBeCloseTo(154.95, 1);
    expect(face.width("предупреждение", 14, 0)).toBeCloseTo(106.09, 1);
    expect(face.width("Опубликовать", 16, 0)).toBeCloseTo(99.2, 0);
    expect(face.width("предупреждение", 16, 0)).toBeCloseTo(121.3, 0);
  });

  it("is set in the guest button's 16, not in the page's 14", () => {
    // A window's control differs from a page's in its measure alone, so its
    // captions take the guest button's size.
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
    expect(rooms()).toEqual({
      formDesktop: 244,
      editorDesktop: 414,
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
    // Longer than the half of a confirmation: the warning window's "Выдать"
    // is why its caption stands on one line.
    expect(
      face.width("Выдать предупреждение", size("confirm"), 0),
    ).toBeGreaterThan(rooms().confirmDesktop);
  });

  const cases = [
    ...new Map(
      captions().map((c) => [
        `${c.window}|${c.editor}|${c.text}`,
        [c.text, c.window, c.editor] as const,
      ]),
    ).values(),
  ];

  // Every caption is measured in bold, the primary's weight: what fits in bold
  // fits in the regular of "Отмена" as well.
  it.each(cases)(
    "%s (%s) stands on one line in its half",
    (text, window, editor) => {
      const all = rooms();
      const half =
        window === "confirm"
          ? all.confirmDesktop
          : editor
            ? all.editorDesktop
            : all.formDesktop;
      expect(
        face.width(text, size(window), 0),
        `${text} / ${half}`,
      ).toBeLessThan(half);
    },
  );
});

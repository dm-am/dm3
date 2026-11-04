/**
 * @vitest-environment jsdom
 */

/**
 * The fields of a form in a window: the 52px field,
 * the caption bold at 15, the frame that stays neutral when a field is
 * refused, and the line under a field that holds no place in advance. A field
 * of a date, a time or a year draws its frame on a box of its own and says the
 * window's measure itself. The rest of the window is dialogSurface.spec.ts's.
 */
import { afterEach, describe, it, expect, vi } from "vitest";
import { defineComponent, h, nextTick } from "vue";
import { mount } from "@vue/test-utils";
import {
  componentCss,
  ruleMap,
  scopedComponentCss,
} from "@/shared/lib/testing/styles";
import { cascaded, matching } from "@/shared/lib/testing/cascade";
import {
  FIELD,
  SCOPE,
  at,
  control,
  renderField,
} from "@/shared/lib/testing/windowMarkup";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";

vi.mock("vue-final-modal", async () =>
  (await import("@/shared/lib/testing/vueFinalModalStub")).vueFinalModal(),
);

afterEach(() => {
  document.body.innerHTML = "";
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
      const own = cascaded(sheet, control(field));
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
    expect(cascaded(css(), control(field)).height).toBeUndefined();
  });

  it("leaves the BBCode editor as it is", () => {
    // The editor carries a textarea of its own deep inside; the window's rule
    // for a text area is for the one the row holds itself.
    const field = renderField({
      inDialog: true,
      control:
        "<div class='bbcode-editor-wrapper'><div class='bbcode-editor'><textarea class='bbcode-textarea'></textarea></div></div>",
    });
    const own = cascaded(css(), control(field));
    expect(own.padding).toBeUndefined();
    expect(own["font-size"]).toBeUndefined();
  });

  it("sets the caption bold at 15, 8 above the field, and the optional mark at 14", () => {
    const field = renderField({ inDialog: true, control: "<input>" });
    const caption = cascaded(css(), field.querySelector(".form-field-label")!);

    expect(caption["font-size"]).toBe("15px");
    expect(caption["font-weight"]).toBe("700");
    expect(caption["margin-bottom"]).toBe("8px");

    const optional = cascaded(
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
    const refusal = cascaded(css(), field.querySelector(".form-field-error")!);

    expect(refusal.color).toBe("var(--accent-red)");
    expect(refusal["font-size"]).toBe("14px");
    expect(refusal["margin-top"]).toBe("6px");
    // No reserve: nothing keeps an empty line open under a field that is fine.
    expect(refusal["min-height"]).toBeUndefined();
    expect(refusal.visibility).toBeUndefined();

    const hint = cascaded(css(), field.querySelector(".form-field-hint")!);
    expect(hint["margin-top"]).toBe("6px");
    expect(hint.color).toBe("var(--text-muted)");
    expect(hint["font-size"]).toBe("14px");
  });

  it("steps 16 from one field to the next", () => {
    const field = renderField({ inDialog: true, control: "<input>" });
    const own = cascaded(css(), field);

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

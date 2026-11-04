/**
 * @vitest-environment jsdom
 */

// The label and the control are siblings: FormField wires the id itself, over the row the slot rendered.
import { afterEach, describe, it, expect } from "vitest";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { nextTick } from "vue";
import { mount } from "@vue/test-utils";
import { scopedComponentCss } from "@/shared/lib/testing/styles";
import FormField from "./FormField.vue";

type Props = {
  label?: string;
  name?: string;
  errors?: string[];
  optional?: boolean;
};

const mountField = async (props: Props, slots: Record<string, string>) => {
  const wrapper = mount(FormField, { props, slots });
  await nextTick();
  return wrapper;
};

const fieldLabel = (wrapper: ReturnType<typeof mount>) =>
  wrapper.find(".form-field-label label");

describe("FormField label binding", () => {
  it("points the label at a control the caller left without an id", async () => {
    const wrapper = await mountField(
      { label: "Название", name: "pollTitle" },
      { default: "<input type='text' />" },
    );

    const input = wrapper.find("input").element;
    expect(input.id).not.toBe("");
    expect(fieldLabel(wrapper).attributes("for")).toBe(input.id);
  });

  it("points the label at the id the caller wrote, not at the name", async () => {
    const wrapper = await mountField(
      { label: "Почта", name: "email" },
      { default: "<input id='recovery-email' type='email' />" },
    );

    expect(fieldLabel(wrapper).attributes("for")).toBe("recovery-email");
  });

  it("reaches the control through a wrapping component's markup", async () => {
    const wrapper = await mountField(
      { label: "Тип бана", name: "ban-policy" },
      {
        default:
          "<div class='select-control'><select><option>Один</option></select></div>",
      },
    );

    const select = wrapper.find("select").element;
    expect(select.id).not.toBe("");
    expect(fieldLabel(wrapper).attributes("for")).toBe(select.id);
  });

  it("gives two fields with the same name different control ids", async () => {
    const wrapper = mount({
      components: { FormField },
      template: `<div>
        <FormField label="Статус" name="premoderation-status"><input /></FormField>
        <FormField label="Статус" name="premoderation-status"><input /></FormField>
      </div>`,
    });
    await nextTick();

    const [first, second] = wrapper.findAll("input");
    expect(first.element.id).not.toBe(second.element.id);
  });

  it("fills in the for of a label the caller rendered into the slot", async () => {
    const wrapper = await mountField(
      { name: "password" },
      { label: "<label>Пароль</label>", default: "<input type='password' />" },
    );

    expect(fieldLabel(wrapper).attributes("for")).toBe(
      wrapper.find("input").element.id,
    );
  });

  it("leaves a for the caller wrote in the slot alone", async () => {
    const wrapper = await mountField(
      { name: "password" },
      {
        label: "<label for='password'>Пароль</label>",
        default: "<input id='password' type='password' />",
      },
    );

    expect(fieldLabel(wrapper).attributes("for")).toBe("password");
  });
});

describe("FormField group naming", () => {
  it("names the row as a group when it holds several controls", async () => {
    const wrapper = await mountField(
      { label: "Варианты ответа", name: "pollOptions" },
      { default: "<input /><input />" },
    );

    const row = wrapper.find(".form-field-row");
    expect(row.attributes("role")).toBe("group");
    expect(row.attributes("aria-labelledby")).toBe(
      fieldLabel(wrapper).attributes("id"),
    );
    expect(fieldLabel(wrapper).attributes("for")).toBeUndefined();
  });

  it("does not point the label at options that carry their own label", async () => {
    const wrapper = await mountField(
      { label: "Тип опроса" },
      {
        default:
          "<label><input type='radio' /> Анонимный</label>" +
          "<label><input type='radio' /> Публичный</label>",
      },
    );

    expect(wrapper.find(".form-field-row").attributes("role")).toBe("group");
    expect(fieldLabel(wrapper).attributes("for")).toBeUndefined();
  });

  it("ignores a control hidden by v-show", async () => {
    const wrapper = await mountField(
      { label: "Ответ", name: "ticket-answer" },
      {
        default:
          "<div><textarea style='display: none'></textarea>" +
          "<div contenteditable='true'></div></div>",
      },
    );

    const row = wrapper.find(".form-field-row");
    expect(row.attributes("role")).toBe("group");
    expect(row.attributes("aria-labelledby")).toBe(
      fieldLabel(wrapper).attributes("id"),
    );
  });
});

describe("FormField error state", () => {
  it("marks the field while it carries an error", async () => {
    const wrapper = await mountField(
      { label: "Название", name: "title", errors: ["Обязательное поле"] },
      { default: "<input />" },
    );

    expect(wrapper.classes()).toContain("error");
  });

  it("marks a field that has no label", async () => {
    const wrapper = await mountField(
      { name: "title", errors: ["Обязательное поле"] },
      { default: "<input />" },
    );

    expect(wrapper.classes()).toContain("error");
  });

  it("takes the mark back when the errors go", async () => {
    const wrapper = await mountField(
      { label: "Название", name: "title", errors: ["Обязательное поле"] },
      { default: "<input />" },
    );

    await wrapper.setProps({ errors: [] });
    await nextTick();

    expect(wrapper.classes()).not.toContain("error");
  });

  it("does not mark the field for an empty error string", async () => {
    // .required("") produces one; it prints nothing, so it must paint nothing.
    const wrapper = await mountField(
      { label: "Название", name: "title", errors: [""] },
      { default: "<input />" },
    );

    expect(wrapper.classes()).not.toContain("error");
  });
});

describe("FormField description", () => {
  it("describes the control with its error and marks it invalid", async () => {
    const wrapper = await mountField(
      { label: "Название", name: "title", errors: ["Обязательное поле"] },
      { default: "<input />" },
    );

    const input = wrapper.find("input");
    expect(input.attributes("aria-describedby")).toBe(
      wrapper.find(".form-field-error").attributes("id"),
    );
    expect(input.attributes("aria-invalid")).toBe("true");
  });

  it("takes the description back when the errors go", async () => {
    const wrapper = await mountField(
      { label: "Название", name: "title", errors: ["Обязательное поле"] },
      { default: "<input />" },
    );

    await wrapper.setProps({ errors: [] });
    await nextTick();

    const input = wrapper.find("input");
    expect(input.attributes("aria-describedby")).toBeUndefined();
    expect(input.attributes("aria-invalid")).toBeUndefined();
  });

  it("describes the control with the hint", async () => {
    const wrapper = await mountField(
      { label: "Варианты ответа", name: "pollOptions" },
      { default: "<input />", hint: "Минимум два варианта" },
    );

    expect(wrapper.find("input").attributes("aria-describedby")).toBe(
      wrapper.find(".form-field-hint").attributes("id"),
    );
  });

  it("describes the group when the row holds no single control", async () => {
    const wrapper = await mountField(
      { label: "Варианты ответа", name: "pollOptions", errors: ["Мало"] },
      { default: "<input /><input />" },
    );

    expect(wrapper.find(".form-field-row").attributes("aria-describedby")).toBe(
      wrapper.find(".form-field-error").attributes("id"),
    );
  });
});

// The row stretches the fields the reader types into, and not a checkbox or a radio: the scoped
// selector outranks the site-wide checkbox rule. jsdom applies no scoped sheet, so the sheet is compiled.
describe("FormField control width", () => {
  /** Stands in for the build's scope id; compileStyle writes it verbatim. */
  const SCOPE = "data-v-formfield";
  const compileSheet = (): string =>
    scopedComponentCss(
      join(dirname(fileURLToPath(import.meta.url)), "FormField.vue"),
      SCOPE,
    );

  /** Every selector in the compiled sheet that stretches a control. */
  const stretchingSelectors = (): string[] =>
    [...compileSheet().matchAll(/([^{}]+)\{([^{}]*)\}/g)]
      .filter((rule) => /(^|;)\s*width:\s*100%\s*(;|$)/.test(rule[2]))
      .flatMap((rule) => rule[1].split(","))
      .map((selector) => selector.trim())
      .filter(Boolean);

  /** A row as a caller writes it, in the document so ancestors can match. */
  const renderRow = (markup: string): HTMLElement => {
    const row = document.createElement("div");
    row.className = "form-field-row";
    row.setAttribute(SCOPE, "");
    row.innerHTML = markup;
    document.body.appendChild(row);
    return row;
  };

  afterEach(() => {
    document.body.innerHTML = "";
  });

  it("leaves a checkbox and a radio out of the stretched controls", () => {
    const row = renderRow(
      '<label class="checkbox-label"><input type="checkbox" /> Комментарии включены</label>' +
        '<label><input type="radio" /> Анонимный</label>',
    );
    const selectors = stretchingSelectors();

    for (const option of row.querySelectorAll("input"))
      for (const selector of selectors)
        expect(
          option.matches(selector),
          `${option.type} matches ${selector}`,
        ).toBe(false);
  });

  it("still stretches the fields the reader types into", () => {
    // Without this the rule could be deleted outright and the case above would
    // read as satisfied.
    const row = renderRow(
      "<input type='text' /><textarea></textarea><select></select>",
    );
    const selectors = stretchingSelectors();

    const controls = row.querySelectorAll("input, textarea, select");
    expect(controls).toHaveLength(3);
    for (const control of controls)
      expect(
        selectors.some((selector) => control.matches(selector)),
        `${control.tagName} matches nothing that stretches it`,
      ).toBe(true);
  });
});

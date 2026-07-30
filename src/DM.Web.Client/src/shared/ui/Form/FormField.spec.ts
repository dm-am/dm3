/**
 * @vitest-environment jsdom
 */

/**
 * FormField renders its label and its control as siblings, so the only thing
 * that ties them together is an id — and the id was the caller's to write.
 * Thirty fields carried a label, twenty-one of them pointed it at an id no
 * element on the page had: a screen reader announced an unnamed edit box, and
 * a click on the label put the caret nowhere.
 *
 * The wiring now happens here, over the row the slot rendered, because that is
 * the only place that knows which element the caller put in. Every case below
 * fails if that binding is taken back out.
 */
import { describe, it, expect } from "vitest";
import { nextTick } from "vue";
import { mount } from "@vue/test-utils";
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
    const first = await mountField(
      { label: "Статус", name: "premoderation-status" },
      { default: "<input />" },
    );
    const second = await mountField(
      { label: "Статус", name: "premoderation-status" },
      { default: "<input />" },
    );

    expect(first.find("input").element.id).not.toBe(
      second.find("input").element.id,
    );
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

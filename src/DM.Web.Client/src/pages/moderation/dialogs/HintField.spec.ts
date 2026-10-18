/**
 * @vitest-environment jsdom
 */

/**
 * The field of a tag description and its preview, in the window of the tag.
 *
 * The buttons write the one tag of a description around the word the
 * moderator selected, through the editor's panel of an insertion; the check
 * under the field names what the page will print as typed; the preview draws
 * the tooltip a reader will get, and redraws it with every character.
 */
import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import { mount, enableAutoUnmount } from "@vue/test-utils";
import { defineComponent, h, nextTick, ref } from "vue";
import HintField from "./HintField.vue";
import HintPreview from "./HintPreview.vue";

enableAutoUnmount(afterEach);

beforeEach(() => {
  vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(null);
});

afterEach(() => {
  vi.restoreAllMocks();
  document.body.innerHTML = "";
});

/** The field with its model, and the model to read back. */
function mountField(initial: string) {
  const text = ref(initial);
  const Host = defineComponent({
    setup: () => () =>
      h("div", [
        h(HintField, {
          id: "tag-description",
          modelValue: text.value,
          "onUpdate:modelValue": (value: string) => (text.value = value),
        }),
        h(HintPreview, { description: text.value, title: "Острые темы" }),
      ]),
  });
  const wrapper = mount(Host, { attachTo: document.body });
  return { wrapper, text };
}

/** Select a part of the text, as the moderator does with the mouse. */
function select(
  wrapper: ReturnType<typeof mountField>["wrapper"],
  part: string,
) {
  const area = wrapper.get<HTMLTextAreaElement>("#tag-description").element;
  const start = area.value.indexOf(part);
  area.setSelectionRange(start, start + part.length);
}

const button = (
  wrapper: ReturnType<typeof mountField>["wrapper"],
  name: string,
) => wrapper.findAll("button").find((one) => one.text() === name)!;

const input = (name: string) =>
  document.querySelector<HTMLInputElement>(
    `.insert-panel input[name="${name}"]`,
  )!;

async function type(name: string, value: string) {
  const field = input(name);
  field.value = value;
  field.dispatchEvent(new Event("input"));
  await nextTick();
}

async function insert() {
  const submit = Array.from(
    document.querySelectorAll<HTMLButtonElement>(".insert-panel button"),
  ).find((one) => one.textContent?.trim() === "Вставить")!;
  submit.click();
  await nextTick();
  await nextTick();
}

describe("the field of a tag description", () => {
  it("wraps the selected word in a text tip", async () => {
    const { wrapper, text } = mountField("Игра затрагивает спорные темы");
    select(wrapper, "спорные");

    await button(wrapper, "tip text").trigger("click");
    await nextTick();
    expect(input("word").value).toBe("спорные");

    await type("text", "Политика, религия");
    await insert();

    expect(text.value).toBe(
      'Игра затрагивает [tip text="Политика, религия"]спорные[/tip] темы',
    );
    expect(document.querySelector(".insert-panel")).toBeNull();
  });

  it("writes a picture before its text", async () => {
    const { wrapper, text } = mountField("Эротические сцены");
    select(wrapper, "Эротические сцены");

    await button(wrapper, "tip img").trigger("click");
    await nextTick();
    await type("img", " /images/erp-tooltip.gif ");
    await type("text", "Подпись");
    await insert();

    expect(text.value).toBe(
      '[tip img="/images/erp-tooltip.gif" text="Подпись"]Эротические сцены[/tip]',
    );
  });

  it("refuses an address the page will not show, and says why", async () => {
    const { wrapper, text } = mountField("слово");
    select(wrapper, "слово");

    await button(wrapper, "tip img").trigger("click");
    await nextTick();
    await type("img", "images/a.gif");
    await insert();

    expect(text.value).toBe("слово");
    expect(document.querySelector(".insert-field-error")?.textContent).toBe(
      "Адрес начинается с http://, https:// или /",
    );

    await type("img", "//example.com/a.gif");
    await insert();
    expect(text.value).toBe("слово");
  });

  it("refuses a value the tag cannot hold", async () => {
    const { wrapper, text } = mountField("слово");
    select(wrapper, "слово");

    await button(wrapper, "tip text").trigger("click");
    await nextTick();
    await type("text", 'он сказал "да"] и ушел');
    await insert();

    expect(text.value).toBe("слово");
    expect(document.querySelector(".insert-field-error")?.textContent).toBe(
      "Нельзя ставить кавычку перед ]",
    );
  });

  it("names what the page prints as typed, under the field", async () => {
    const { wrapper, text } = mountField('[tip text="x"]слово[/tip]');
    expect(wrapper.find(".hint-check").exists()).toBe(false);

    text.value = "[tip]слово[/tip]";
    await nextTick();

    expect(wrapper.get(".hint-check").text()).toContain(
      'Строка 1: у [tip] нужен text или img: [tip text="Текст"]слово[/tip]',
    );
  });

  it("redraws the preview with every character", async () => {
    const { wrapper, text } = mountField("");
    expect(wrapper.get(".hint-preview .tooltip").text()).toBe("Острые темы");

    text.value = 'Игра затрагивает [tip text="Политика"]спорные[/tip] темы';
    await nextTick();

    const preview = wrapper.get(".hint-preview");
    expect(preview.get(".bb-tip").text()).toBe("спорные");
    expect(preview.get(".bb-tip").attributes("data-tip-text")).toBe("Политика");
    expect(preview.get(".hint-preview-tag").text()).toBe("Острые темы");
  });
});

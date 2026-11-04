/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { defineComponent, h } from "vue";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import AttributeSchemaEditor from "./AttributeSchemaEditor.vue";
import { useSchemaSources, type SchemaSource } from "../model/schemaSources";
import { attributeSchemaTemplateApi } from "@/entities/attribute-schema-template";
import {
  gameApi,
  gameCatalogApi,
  AttributeSpecificationType,
  GameParticipation,
  type AttributeSchemaDraft,
  type AttributeSpecification,
} from "@/entities/game";
import type { DialogActionResult } from "@/shared/lib/composables/useDialogAction";

const spec = (id: string, title: string): AttributeSpecification => ({
  id,
  title,
  required: false,
  type: AttributeSpecificationType.Text,
  order: 0,
  isDescriptor: false,
  isHidden: false,
  maxLength: null,
  values: [],
});

const DEFAULT_TEMPLATE_SPEC = spec("template-spec", "Мировоззрение");
const GAME_SPEC = spec("game-spec", "Фракция");

// One button per open dialog, named by its title: the real modal teleports to the body.
const confirmStub = defineComponent({
  props: {
    show: { type: Boolean, default: false },
    title: { type: String, default: "" },
  },
  emits: ["confirm", "cancel", "update:show"],
  setup:
    (props, { emit }) =>
    () =>
      props.show
        ? h(
            "button",
            {
              class: "confirm-stub",
              "data-title": props.title,
              onClick: () => emit("confirm"),
            },
            props.title,
          )
        : null,
});

const stubs = {
  ConfirmDialog: confirmStub,
  AttributeEditForm: true,
  CharacterFormPreview: true,
};

function serveCatalogue() {
  vi.spyOn(attributeSchemaTemplateApi, "getTemplates").mockResolvedValue({
    data: {
      resources: [
        {
          id: "default-template",
          title: "Базовая",
          isDefault: true,
          specifications: [DEFAULT_TEMPLATE_SPEC],
        },
        {
          id: "light-template",
          title: "Лайт",
          isDefault: false,
          specifications: [],
        },
      ],
      paging: null,
    },
    error: null,
  } as unknown as Awaited<
    ReturnType<typeof attributeSchemaTemplateApi.getTemplates>
  >);

  vi.spyOn(gameCatalogApi, "getParticipatingGames").mockResolvedValue({
    data: {
      resources: [
        {
          id: "led-game",
          publicId: "led",
          title: "Моя игра",
          participation: [GameParticipation.Authority],
        },
        {
          id: "read-game",
          publicId: "read",
          title: "Чужая игра",
          participation: [GameParticipation.Player],
        },
      ],
      paging: null,
    },
    error: null,
  } as unknown as Awaited<
    ReturnType<typeof gameCatalogApi.getParticipatingGames>
  >);

  vi.spyOn(gameApi, "getGame").mockResolvedValue({
    data: {
      resource: { schema: { specifications: [GAME_SPEC], version: 4 } },
    },
    error: null,
  } as unknown as Awaited<ReturnType<typeof gameApi.getGame>>);
}

async function loadSources(): Promise<SchemaSource[]> {
  const { sources, load } = useSchemaSources();
  await load();
  return sources.value;
}

const mountEditor = (
  sources: SchemaSource[],
  specifications: AttributeSpecification[] = [],
) =>
  mount(AttributeSchemaEditor, {
    props: { modelValue: { title: "", specifications }, sources },
    global: { stubs },
  });

const named = (wrapper: VueWrapper, text: string) =>
  wrapper.findAll("button").find((b) => b.text() === text)!;

const rows = (wrapper: VueWrapper) =>
  wrapper.findAll(".col-name").map((cell) => cell.text());

afterEach(() => vi.restoreAllMocks());

describe("the source list", () => {
  it("offers templates, then the games the reader leads, then an empty sheet", async () => {
    serveCatalogue();

    expect((await loadSources()).map((s) => s.label)).toEqual([
      "Базовая",
      "Лайт",
      "Из моей игры: Моя игра",
      "Пустая схема",
    ]);
  });

  it("picks the first entry, the default template, without being asked", async () => {
    serveCatalogue();
    const sources = await loadSources();

    const wrapper = mountEditor(sources);
    await flushPromises();

    const select = wrapper.find("#schema-source");
    expect((select.element as HTMLSelectElement).value).toBe(sources[0].value);
    expect(select.findAll("option").map((o) => o.text())).toEqual([
      "Базовая",
      "Лайт",
      "Из моей игры: Моя игра",
      "Пустая схема",
    ]);
  });
});

describe("copying a sheet into the editor", () => {
  it("fills an empty table straight away, with identifiers of its own", async () => {
    serveCatalogue();
    const sources = await loadSources();

    const wrapper = mountEditor(sources);
    await flushPromises();
    await named(wrapper, "Скопировать").trigger("click");
    await flushPromises();

    expect(wrapper.find(".confirm-stub").exists()).toBe(false);
    expect(rows(wrapper)).toEqual(["Мировоззрение"]);

    const saved = wrapper.emitted("update:modelValue")?.at(-1)?.[0] as {
      specifications: AttributeSpecification[];
    };
    expect(saved.specifications[0].id).not.toBe(DEFAULT_TEMPLATE_SPEC.id);
  });

  it("asks before it replaces a table that already has attributes", async () => {
    serveCatalogue();
    const sources = await loadSources();

    const wrapper = mountEditor(sources, [spec("kept", "Раса")]);
    await flushPromises();
    await named(wrapper, "Скопировать").trigger("click");
    await flushPromises();

    expect(rows(wrapper)).toEqual(["Раса"]);

    const question = wrapper.find(".confirm-stub");
    expect(question.attributes("data-title")).toBe(
      "Скопировать схему атрибутов?",
    );

    await question.trigger("click");
    await flushPromises();

    expect(rows(wrapper)).toEqual(["Мировоззрение"]);
  });

  it("reads the sheet of a game the reader leads out of that game's details", async () => {
    serveCatalogue();
    const sources = await loadSources();

    const wrapper = mountEditor(sources);
    await flushPromises();
    await wrapper.find("#schema-source").setValue(sources[2].value);
    await named(wrapper, "Скопировать").trigger("click");
    await flushPromises();

    expect(gameApi.getGame).toHaveBeenCalledWith("led");
    expect(rows(wrapper)).toEqual(["Фракция"]);
  });

  it("empties the table for the last entry of the list", async () => {
    serveCatalogue();
    const sources = await loadSources();

    const wrapper = mountEditor(sources, [spec("kept", "Раса")]);
    await flushPromises();
    await wrapper.find("#schema-source").setValue(sources.at(-1)!.value);
    await named(wrapper, "Скопировать").trigger("click");
    await flushPromises();
    await wrapper.find(".confirm-stub").trigger("click");
    await flushPromises();

    expect(rows(wrapper)).toEqual([]);
  });
});

describe("the line under the source list", () => {
  const mountWithRefusal = (sourcesRefusal: string) =>
    mount(AttributeSchemaEditor, {
      props: {
        modelValue: { title: "", specifications: [] },
        sources: [
          { value: "empty", label: "Пустая схема", read: async () => [] },
        ],
        sourcesRefusal,
      },
      global: { stubs },
    });

  it("prints what the host says went wrong with the list, right under it", () => {
    const wrapper = mountWithRefusal("Не удалось загрузить шаблоны схем");

    const line = wrapper.get(".copy-row + .form-error");
    expect(line.text()).toBe("Не удалось загрузить шаблоны схем");
    expect(line.attributes("role")).toBe("alert");
  });

  it("prints nothing under the list while the host has nothing to say", () => {
    const wrapper = mountWithRefusal("");

    expect(wrapper.find(".copy-row + .form-error").exists()).toBe(false);
  });
});

const saveButton = (wrapper: VueWrapper) =>
  wrapper.find(".editor-actions button");

const savedAnswer = (wrapper: VueWrapper) => ({
  label: saveButton(wrapper).text(),
  inert: saveButton(wrapper).attributes("disabled") !== undefined,
});

// Without the source list nothing here reaches for the network.
const mountActions = (saved = false, save = vi.fn().mockResolvedValue({})) =>
  mount(AttributeSchemaEditor, {
    props: {
      modelValue: { title: "", specifications: [spec("kept", "Раса")] },
      saved,
      save,
    },
    global: { stubs },
  });

describe("the editor's answer to a save", () => {
  it("offers the save, and takes the press, while the host confirms nothing", async () => {
    const save = vi.fn().mockResolvedValue({});
    const wrapper = mountActions(false, save);

    expect(savedAnswer(wrapper)).toEqual({ label: "Сохранить", inert: false });

    await saveButton(wrapper).trigger("click");
    expect(save).toHaveBeenCalledTimes(1);
  });

  it("reads Сохранено and takes no press once the host confirms one", async () => {
    const save = vi.fn().mockResolvedValue({});
    const wrapper = mountActions(true, save);

    expect(savedAnswer(wrapper)).toEqual({ label: "Сохранено", inert: true });

    await saveButton(wrapper).trigger("click");
    expect(save).not.toHaveBeenCalled();
  });

  it("offers the save again as soon as the host says the draft has moved", async () => {
    const wrapper = mountActions(true);

    await wrapper.setProps({ saved: false });

    expect(savedAnswer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });
});

describe("the data-loss question", () => {
  type Save = (
    draft: AttributeSchemaDraft,
    options?: object,
  ) => Promise<DialogActionResult>;

  const REFUSED = {
    error: {
      type: "",
      title: "Схему только что изменили. Проверьте и сохраните снова",
      status: 409,
      traceId: "t",
    },
  };

  const mountAsking = (save: Save) =>
    mount(AttributeSchemaEditor, {
      props: {
        modelValue: { title: "", specifications: [spec("kept", "Раса")] },
        hasCharacters: true,
        save,
      },
      global: {
        stubs: { AttributeEditForm: true, CharacterFormPreview: true },
      },
      attachTo: document.body,
    });

  const question = () =>
    document.body.querySelector(".dialog-title")?.textContent ?? "";
  const confirm = () =>
    document.body.querySelector<HTMLButtonElement>(".dialog-btn-submit");

  it("sends the save with the flags that keep the interceptor quiet", async () => {
    const save = vi.fn<Save>().mockResolvedValue({ error: null });
    const wrapper = mountAsking(save);

    await saveButton(wrapper).trigger("click");
    expect(save).not.toHaveBeenCalled();
    confirm()?.click();
    await flushPromises();

    expect(save).toHaveBeenCalledWith(expect.anything(), {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(question()).not.toContain("Сохранить схему атрибутов?");
    wrapper.unmount();
  });

  it("keeps the question open with the refusal in it", async () => {
    const save = vi.fn<Save>().mockResolvedValue(REFUSED);
    const wrapper = mountAsking(save);

    await saveButton(wrapper).trigger("click");
    confirm()?.click();
    await flushPromises();

    expect(question()).toContain("Сохранить схему атрибутов?");
    expect(
      document.body.querySelector(".dialog-refusal")?.textContent?.trim(),
    ).toBe("Схему только что изменили. Проверьте и сохраните снова");
    wrapper.unmount();
  });

  it("is shut while the save is on the wire, and sends it once", async () => {
    let answer: (value: DialogActionResult) => void = () => {};
    const save = vi.fn<Save>(
      () => new Promise((resolve) => (answer = resolve)),
    );
    const wrapper = mountAsking(save);

    await saveButton(wrapper).trigger("click");
    confirm()?.click();
    await flushPromises();
    confirm()?.click();
    document.body
      .querySelector<HTMLButtonElement>(".dialog-btn-cancel")
      ?.click();
    document.body
      .querySelector(".dialog-container")
      ?.dispatchEvent(
        new KeyboardEvent("keydown", { key: "Escape", bubbles: true }),
      );
    await flushPromises();

    expect(save).toHaveBeenCalledTimes(1);
    expect(question()).toContain("Сохранить схему атрибутов?");

    answer({ error: null });
    await flushPromises();
    expect(question()).not.toContain("Сохранить схему атрибутов?");
    wrapper.unmount();
  });
});

describe("the privacy of the descriptor", () => {
  const mountTable = (specifications: AttributeSpecification[]) =>
    mount(AttributeSchemaEditor, {
      props: { modelValue: { title: "", specifications } },
      global: { stubs },
    });

  const privacy = (wrapper: VueWrapper, title: string) =>
    wrapper.find(`input[aria-label="Приватность: ${title}"]`);

  const lastDraft = (wrapper: VueWrapper) =>
    wrapper.emitted("update:modelValue")?.at(-1)?.[0] as AttributeSchemaDraft;

  it("offers no privacy for the descriptor, and a dash in its place", () => {
    const wrapper = mountTable([
      { ...spec("class", "Класс"), isDescriptor: true },
      spec("race", "Раса"),
    ]);

    expect(privacy(wrapper, "Класс").exists()).toBe(false);
    expect(
      wrapper.findAll(".attr-row")[0].find(".col-flag .dash").exists(),
    ).toBe(true);
    expect(privacy(wrapper, "Раса").exists()).toBe(true);
  });

  it("takes the privacy off an attribute chosen for the descriptor", async () => {
    const wrapper = mountTable([{ ...spec("class", "Класс"), isHidden: true }]);

    await wrapper
      .find('input[aria-label="Дескриптор: Класс"]')
      .trigger("change");

    expect(lastDraft(wrapper).specifications[0]).toMatchObject({
      isDescriptor: true,
      isHidden: false,
    });
    expect(privacy(wrapper, "Класс").exists()).toBe(false);
  });
});

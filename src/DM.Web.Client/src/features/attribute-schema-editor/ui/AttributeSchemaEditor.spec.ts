/**
 * @vitest-environment jsdom
 */

/**
 * The one control that says where a character sheet comes from.
 *
 * Doc 4.2.2.10 gives the editor a single list, "Схема атрибутов", over the
 * table, with "Скопировать" beside it: the site's templates with the default
 * one first and already picked, then the sheets of the games the reader leads,
 * and an empty sheet last. Before this the same control offered other people's
 * public schemas, which the server no longer serves at all, so the list was
 * empty and the master had nothing to start from.
 *
 * Two things about a copy are worth pinning beyond the ordering. It replaces
 * the whole set rather than adding to it, which is why a table with anything
 * in it is asked about first; and the specifications arrive with fresh
 * identifiers, because a copy is a sheet of its own and the values stored
 * against the source's specifications are not its to claim.
 *
 * The last block of the file is about the action bar instead: what the save
 * button says once the host tells the editor the draft on screen is the one
 * the server has.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { defineComponent, h } from "vue";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import AttributeSchemaEditor from "./AttributeSchemaEditor.vue";
import { useSchemaSources, type SchemaSource } from "../model/schemaSources";
import { attributeSchemaTemplateApi } from "@/entities/attribute-schema-template";
import {
  gameApi,
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

/**
 * The modal is a double that draws one button per open dialog, named by the
 * dialog's title: the real one teleports to the body, and this file asks only
 * whether the question was put and what answering it does.
 */
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

  vi.spyOn(gameApi, "getParticipatingGames").mockResolvedValue({
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
  } as unknown as Awaited<ReturnType<typeof gameApi.getParticipatingGames>>);

  vi.spyOn(gameApi, "getGame").mockResolvedValue({
    data: {
      resource: { schema: { specifications: [GAME_SPEC], version: 4 } },
    },
    error: null,
  } as unknown as Awaited<ReturnType<typeof gameApi.getGame>>);
}

/** The list as the composable builds it. */
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

/** The attribute names the table draws, in table order. */
const rows = (wrapper: VueWrapper) =>
  wrapper.findAll(".col-name").map((cell) => cell.text());

afterEach(() => vi.restoreAllMocks());

describe("the source list", () => {
  it("offers templates, then the games the reader leads, then an empty sheet", async () => {
    // The game entries say they are games: a template and a game may be called
    // the same thing, and what the button does with them differs.
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

    // Nothing has moved yet: the question is out and the table is untouched.
    expect(rows(wrapper)).toEqual(["Раса"]);

    const question = wrapper.find(".confirm-stub");
    expect(question.attributes("data-title")).toBe(
      "Скопировать схему атрибутов?",
    );

    await question.trigger("click");
    await flushPromises();

    // The whole set, not an addition to it.
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

/**
 * The line under the list, for what went wrong behind it.
 *
 * The host owns the list and its reads (schemaSources.spec pins what they say
 * and when); the editor only prints the sentence, under the control it is
 * about, where the reader who pressed "Скопировать" or is looking for the
 * templates is looking. A plate in the corner used to say this.
 */
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

/**
 * The action bar, and the one thing it has to say.
 *
 * A schema save changes nothing the editor draws: the table after it holds the
 * same rows it held before, the preview under it is the same preview, and the
 * page does not move. Until the host was given a way to answer, the only thing
 * that ever said the save had happened was a message in the corner - which is
 * what UI_STANDARDS, "Всплывающие сообщения", sends back to the button.
 *
 * The editor cannot work the answer out for itself: the draft it holds is a
 * copy and the save belongs to whoever mounted it, so `saved` arrives as a
 * prop. What is pinned here is that the prop reaches the reader - the label
 * and the press - and nothing else about it.
 */

/** The first control of the action bar: the one that saves. */
const saveButton = (wrapper: VueWrapper) =>
  wrapper.find(".editor-actions button");

/** What the button says, and whether it can still be pressed. */
const savedAnswer = (wrapper: VueWrapper) => ({
  label: saveButton(wrapper).text(),
  inert: saveButton(wrapper).attributes("disabled") !== undefined,
});

/**
 * The editor with a sheet in it and no source list: the list is the subject of
 * the tests above, and without it nothing here reaches for the network.
 */
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

/**
 * The data-loss question, with the site's own window rather than the double.
 *
 * The save it guards cannot be taken back, and it used to close the question
 * before the request went out: a refused save then fell as a plate on a page
 * whose question was gone. The question runs the host's save as its action
 * now, so it waits for the answer, prints a refusal in itself and closes on
 * success alone.
 */
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

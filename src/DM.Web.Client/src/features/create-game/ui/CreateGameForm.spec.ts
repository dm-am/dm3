/**
 * @vitest-environment jsdom
 */

/**
 * What the creation form collects has to reach the request.
 *
 * The tag section drew working chips and a "Выбрано тегов: N" counter while the
 * payload carried no tags at all: the game was created unmarked, and the master
 * found that out only by never seeing it under the filters they had ticked. The
 * assistant one section down was the same story from the other end — the request
 * carried the username and the API mapping dropped it, so the picker changed
 * nothing and said nothing.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia } from "pinia";
import CreateGameForm from "./CreateGameForm.vue";
import {
  gameApi,
  AttributeSpecificationType,
  type AttributeSpecification,
  type Tag,
} from "@/entities/game";
import { attributeSchemaTemplateApi } from "@/entities/attribute-schema-template";
import { useToast } from "@/shared/lib/composables/useToast";
import type { BadRequestError, GeneralError } from "@/shared/api/models/common";

// The form navigates to the created game, and it guards the way out while the
// fields hold text (UnsavedChangesGuard registers onBeforeRouteLeave). Both are
// out of the scope of the checks below, so both are doubles; a mock that leaves
// the hook out fails the mount instead of the assertion.
vi.mock("vue-router", () => ({
  useRouter: () => ({ push: vi.fn() }),
  onBeforeRouteLeave: vi.fn(),
}));

// Three neighbours of the fields under test, none of them part of the check:
// the editor drags the whole tiptap stack in, the schema editor and the
// assistant picker their own trees. Render functions, not `template` strings —
// the test build runs the runtime-only Vue.
vi.mock("@/shared/ui/BBCodeEditor", async () => {
  const { defineComponent, h } = await import("vue");
  return {
    BBCodeEditor: defineComponent({
      name: "BBCodeEditor",
      props: { modelValue: { type: String, default: "" } },
      setup: (props) => () =>
        h("div", { class: "editor-stub" }, props.modelValue),
    }),
  };
});

// The editor itself is a double, because it drags the whole character-sheet
// tree in and what this file checks is the request. The source list beside it is NOT
// doubled: the form opens on the default template the catalogue names, and a
// stub of that read would have let the form open on nothing.
vi.mock(
  "@/features/attribute-schema-editor/@x/create-game",
  async (original) => {
    const { defineComponent, h } = await import("vue");
    return {
      ...(await original<Record<string, unknown>>()),
      AttributeSchemaEditor: defineComponent({
        name: "AttributeSchemaEditor",
        setup: () => () => h("div", { class: "schema-editor-stub" }),
      }),
    };
  },
);

vi.mock("./AssistantSelector.vue", async () => {
  const { defineComponent, h } = await import("vue");
  return {
    default: defineComponent({
      name: "AssistantSelector",
      props: { modelValue: { type: String, default: null } },
      setup: () => () => h("input", { class: "assistant-stub" }),
    }),
  };
});

const tag = (id: number, title: string): Tag => ({
  id,
  title,
  groupTitle: "Жанр",
  gamesCount: 0,
  sortOrder: id,
  groupSortOrder: 1,
});

const TAGS = [tag(3, "Фэнтези"), tag(7, "Хоррор")];

/** One specification of the sheet the site offers new games. */
const SPEC: AttributeSpecification = {
  id: "spec-of-the-template",
  title: "Мировоззрение",
  required: false,
  type: AttributeSpecificationType.TextList,
  order: 0,
  isDescriptor: true,
  isHidden: false,
  maxLength: null,
  values: [{ value: "Хаотично-добрый", modifier: null }],
};

const mountForm = () =>
  mount(CreateGameForm, { global: { plugins: [createPinia()] } });

/** The specifications of the single creation request the form sent. */
const sentSpecs = (createGame: ReturnType<typeof vi.spyOn>) =>
  (createGame.mock.calls.at(-1)?.[0] as { attributeSpecifications?: unknown })
    ?.attributeSpecifications as AttributeSpecification[] | undefined;

describe("CreateGameForm", () => {
  beforeEach(() => {
    localStorage.clear();
    // The form submits only for a signed-in viewer, and the auth store reads
    // who that is from here.
    localStorage.setItem("user", JSON.stringify({ username: "SolohinLex" }));
    vi.spyOn(gameApi, "getTags").mockResolvedValue({
      data: { resources: TAGS, paging: null },
      error: null,
    });
    // The two reads behind the "Схема атрибутов" list: the site's templates
    // and the games this master leads.
    vi.spyOn(attributeSchemaTemplateApi, "getTemplates").mockResolvedValue({
      data: {
        resources: [
          {
            id: "the-default",
            title: "Базовая",
            isDefault: true,
            specifications: [SPEC],
          },
        ],
        paging: null,
      },
      error: null,
    } as unknown as Awaited<
      ReturnType<typeof attributeSchemaTemplateApi.getTemplates>
    >);
    vi.spyOn(gameApi, "getParticipatingGames").mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    } as unknown as Awaited<ReturnType<typeof gameApi.getParticipatingGames>>);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("submits the tags the master ticked", async () => {
    // Only the id of the created game is read (the router push), so the rest of
    // the resource is not this test's business.
    const createGame = vi.spyOn(gameApi, "createGame").mockResolvedValue({
      data: { resource: { id: "game-1" } },
      error: null,
    } as unknown as Awaited<ReturnType<typeof gameApi.createGame>>);

    const wrapper = mountForm();
    await flushPromises();

    await wrapper.find("#title").setValue("Игра");
    const chips = wrapper.findAll(".tag-button");
    await chips[0].trigger("click");
    await chips[1].trigger("click");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(createGame).toHaveBeenCalledWith(
      expect.objectContaining({ tags: [3, 7] }),
    );
  });

  it("creates the game with the sheet the default template offers", async () => {
    // The sheet used to be a second request against a second resource, and the
    // form opened on nothing at all: a master who left the editor alone got a
    // game with no character sheet. It now opens on the site's default template
    // and travels inside the creation request, with fresh ids: the copy is a
    // sheet of its own and claims none of the template's stored values.
    const createGame = vi.spyOn(gameApi, "createGame").mockResolvedValue({
      data: { resource: { id: "game-1" } },
      error: null,
    } as unknown as Awaited<ReturnType<typeof gameApi.createGame>>);

    const wrapper = mountForm();
    await flushPromises();

    await wrapper.find("#title").setValue("Игра");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    const specs = sentSpecs(createGame);
    expect(specs?.map((s) => s.title)).toEqual(["Мировоззрение"]);
    expect(specs?.[0].values).toEqual(SPEC.values);
    expect(specs?.[0].id).not.toBe(SPEC.id);
  });

  it("leaves a table the master started before the catalogue answered", async () => {
    // The editor is on screen and taking edits while the catalogue is still on
    // the wire, and the answer used to overwrite the draft whatever was in it:
    // an attribute written into that window disappeared without a word.
    const createGame = vi.spyOn(gameApi, "createGame").mockResolvedValue({
      data: { resource: { id: "game-1" } },
      error: null,
    } as unknown as Awaited<ReturnType<typeof gameApi.createGame>>);

    const wrapper = mountForm();
    wrapper
      .findComponent({ name: "AttributeSchemaEditor" })
      .vm.$emit("update:modelValue", {
        title: "",
        specifications: [
          { ...SPEC, id: "spec-of-the-master", title: "Фракция" },
        ],
      });
    await flushPromises();

    await wrapper.find("#title").setValue("Игра");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(sentSpecs(createGame)?.map((s) => s.title)).toEqual(["Фракция"]);
  });

  it("shows the server's verdict on the assistant under the field", async () => {
    const refusal: BadRequestError = {
      type: "",
      title: "Validation failed",
      status: 400,
      traceId: "",
      errors: { AssistantUsername: ["Invalid"] },
    };
    vi.spyOn(gameApi, "createGame").mockResolvedValue({
      data: null,
      error: refusal,
    });

    const wrapper = mountForm();
    await flushPromises();

    await wrapper.find("#title").setValue("Игра");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(wrapper.find(".form-field-error").text()).toBe(
      "Некорректное значение",
    );
  });

  it("puts the verdict on the system and the setting under their own fields", async () => {
    // The dictionary comes back keyed by the properties of the request, so a
    // form that asked for its own field names found nothing and left the reader
    // with a bare toast that named no field at all.
    const refusal: BadRequestError = {
      type: "",
      title: "Validation failed",
      status: 400,
      traceId: "",
      errors: { SystemName: ["Empty"], NarrativeSetting: ["Empty"] },
    };
    vi.spyOn(gameApi, "createGame").mockResolvedValue({
      data: null,
      error: refusal,
    });

    const wrapper = mountForm();
    await flushPromises();

    await wrapper.find("#title").setValue("Игра");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    const shown = wrapper.findAll(".form-field-error").map((e) => e.text());
    expect(shown).toEqual(["Обязательное поле", "Обязательное поле"]);
  });

  /**
   * A refusal with no field to stand beside has nowhere of its own on this
   * screen - there is no strip under the form, only the lines next to the
   * inputs - so it is said in the corner, and the response interceptor says it
   * there too. On a 5xx that was the same sentence twice over, in two messages
   * that both stay until they are dismissed by hand.
   *
   * Both halves are checked, because silencing everything is the other way to
   * fail this: the statuses the interceptor speaks for are quiet here, and a
   * conflict, which it leaves alone, still reaches the reader.
   */
  describe("a refusal outside the fields", () => {
    const refusal = (status: number, title: string): GeneralError => ({
      type: "",
      title,
      status,
      traceId: "",
    });

    async function submitAgainst(error: GeneralError) {
      vi.spyOn(gameApi, "createGame").mockResolvedValue({
        data: null,
        error,
      });

      const wrapper = mountForm();
      await flushPromises();

      await wrapper.find("#title").setValue("Игра");
      await wrapper.find("form").trigger("submit");
      await flushPromises();

      return {
        plates: useToast().toasts.value.map((t) => t.message),
        line: wrapper.find(".form-actions + .action-result"),
      };
    }

    beforeEach(() => {
      const { toasts, dismiss } = useToast();
      [...toasts.value].forEach((t) => dismiss(t.id));
    });

    // Under the button that was pressed, and in no plate - the three the
    // interceptor used to plate included (the owner's rule of 26 September
    // 2026).
    it.each([
      ["a server error", 500, "Ошибка сервера", "Ошибка сервера"],
      ["a refused action", 403, "Недостаточно прав", "Недостаточно прав"],
      [
        "a rate limit",
        429,
        "Слишком много запросов",
        "Слишком много попыток, попробуйте позже",
      ],
      [
        "a conflict",
        409,
        "Игра с таким названием уже есть",
        "Игра с таким названием уже есть",
      ],
    ])("says %s under the button", async (_case, status, title, line) => {
      const { plates, line: said } = await submitAgainst(
        refusal(status, title),
      );

      expect(said.text()).toBe(line);
      expect(plates).toEqual([]);
    });

    it("leaves a request that never arrived to its plate", async () => {
      const { line } = await submitAgainst(refusal(0, ""));

      expect(line.exists()).toBe(false);
    });
  });

  /**
   * The form is DM2's CreateModule: one form of captioned rows in DM2's order
   * and words, with no headed blocks between them.
   *
   * What these catch: a field moved out of DM2's order or a block heading put
   * back ("Основная информация", "Настройки приватности"); the state of the
   * new game not sent, or sent the other way round; a third mode of the
   * discussion offered again, the one that closed reading to outsiders; the
   * boxes opened otherwise than DM2 opens them; the least length of the
   * description left unsaid until the server refuses it.
   *
   * The schema editor is a double in this file, so the heading check covers
   * the form's own markup and not the captions the editor draws inside
   * itself ("Схема атрибутов", "Атрибуты"): those belong to the editor.
   */
  describe("as DM2's form", () => {
    const sent = async (
      choose?: (w: ReturnType<typeof mountForm>) => Promise<void>,
    ) => {
      const createGame = vi.spyOn(gameApi, "createGame").mockResolvedValue({
        data: { resource: { id: "game-1" } },
        error: null,
      } as unknown as Awaited<ReturnType<typeof gameApi.createGame>>);
      const wrapper = mountForm();
      await flushPromises();
      await wrapper.find("#title").setValue("Игра");
      if (choose) await choose(wrapper);
      await wrapper.find("form").trigger("submit");
      await flushPromises();
      return createGame.mock.calls.at(-1)?.[0];
    };

    it("captions its fields in DM2's order, the sheet in its place", async () => {
      const wrapper = mountForm();
      await flushPromises();

      expect(
        wrapper
          .findAll(".form-field-label label, .sheet-caption")
          .map((c) => c.text()),
      ).toEqual([
        "Название:",
        "Состояние:",
        "Ассистент:",
        "Система:",
        "Сеттинг:",
        "Характеристики персонажей:",
        "Обсуждение игры:",
        "Описание:",
        "Теги:",
      ]);
      expect(wrapper.findAll(".checkbox-label").map((l) => l.text())).toEqual([
        "Показывать всем результаты бросков",
        "Показывать всем приватные сообщения",
      ]);
      expect(wrapper.find("h2, h3, .block-title").exists()).toBe(false);
    });

    it("creates the game in the making unless the master says it is ready", async () => {
      expect(await sent()).toMatchObject({ draft: true });
      expect(
        await sent(async (w) => {
          await w.find("#game-state").setValue("ready");
        }),
      ).toMatchObject({ draft: false });
    });

    it("offers the discussion open to everyone or read-only to outsiders, and no third mode", async () => {
      const wrapper = mountForm();
      await flushPromises();

      expect(
        wrapper
          .findAll("#game-commentaries-access option")
          .map((o) => o.text()),
      ).toEqual([
        "Доступно всем без ограничений",
        "Доступно посторонним только для чтения",
      ]);
    });

    it("opens both boxes clear and sends them as they stand", async () => {
      const wrapper = mountForm();
      await flushPromises();
      expect(
        wrapper
          .findAll(".checkbox-label input")
          .map((box) => (box.element as HTMLInputElement).checked),
      ).toEqual([false, false]);

      expect(await sent()).toMatchObject({
        privacySettings: { viewDice: false, viewPrivates: false },
      });
      expect(
        await sent(async (w) => {
          const boxes = w.findAll(".checkbox-label input");
          await boxes[0].setValue(true);
          await boxes[1].setValue(true);
        }),
      ).toMatchObject({
        privacySettings: { viewDice: true, viewPrivates: true },
      });
    });

    it("says the least length of the description under it", async () => {
      const wrapper = mountForm();
      await flushPromises();

      const description = wrapper
        .findAll(".form-field")
        .find((f) =>
          f
            .findAll(".form-field-label label")
            .some((label) => label.text() === "Описание:"),
        );
      expect(description?.find(".form-field-hint").text()).toBe(
        "Не менее 200 символов",
      );
    });
  });
});

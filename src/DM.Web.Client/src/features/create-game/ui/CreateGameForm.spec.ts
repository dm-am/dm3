/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia } from "pinia";
import CreateGameForm from "./CreateGameForm.vue";
import {
  gameApi,
  gameCatalogApi,
  AttributeSpecificationType,
  type AttributeSpecification,
  type Tag,
} from "@/entities/game";
import { attributeSchemaTemplateApi } from "@/entities/attribute-schema-template";
import { useToast } from "@/shared/lib/composables/useToast";
import type { BadRequestError, GeneralError } from "@/shared/api/models/common";

// A mock that leaves the guard hook out fails the mount instead of the assertion.
vi.mock("vue-router", () => ({
  useRouter: () => ({ push: vi.fn() }),
  onBeforeRouteLeave: vi.fn(),
}));

vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeEditor(),
);

// The source list is not doubled: a stub of that read would let the form open on nothing.
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

const sentSpecs = (createGame: ReturnType<typeof vi.spyOn>) =>
  (createGame.mock.calls.at(-1)?.[0] as { attributeSpecifications?: unknown })
    ?.attributeSpecifications as AttributeSpecification[] | undefined;

describe("CreateGameForm", () => {
  beforeEach(() => {
    localStorage.clear();
    // The auth store reads the signed-in viewer from here.
    localStorage.setItem("user", JSON.stringify({ username: "SolohinLex" }));
    vi.spyOn(gameApi, "getTags").mockResolvedValue({
      data: { resources: TAGS, paging: null },
      error: null,
    });
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
    vi.spyOn(gameCatalogApi, "getParticipatingGames").mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    } as unknown as Awaited<
      ReturnType<typeof gameCatalogApi.getParticipatingGames>
    >);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("submits the tags the master ticked", async () => {
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

  // The schema editor is a double: the heading check covers the form's own markup.
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
        "Показывать всем скрытые блоки",
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
        "Не менее 100 символов",
      );
    });
  });
});

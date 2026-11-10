/**
 * @vitest-environment jsdom
 */

/**
 * Saving the character sheet of a game, which drops the values of every
 * specification the new set leaves out. Its two guards are pinned at their
 * weakest point: the version the draft was built on, and an unread roster
 * taken for one with characters.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { defineComponent, h, ref, type PropType } from "vue";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  characterApi,
  gameApi,
  gameCatalogApi,
  useGameDetailsStore,
  AttributeSpecificationType,
  type AttributeSchemaDraft,
  type AttributeSpecification,
  type Character,
  type Game,
} from "@/entities/game";
import {
  createMockCharacter,
  createMockGame,
} from "@/entities/game/model/store.fixtures";
import { asServed } from "@/shared/api/models";
import { attributeSchemaTemplateApi } from "@/entities/attribute-schema-template";
import { useToast } from "@/shared/lib/composables/useToast";
import AttributeSchemaSection from "./AttributeSchemaSection.vue";

function serveEmptySources() {
  const empty = { data: { resources: [], paging: null }, error: null } as never;
  vi.spyOn(attributeSchemaTemplateApi, "getTemplates").mockResolvedValue(empty);
  vi.spyOn(gameCatalogApi, "getParticipatingGames").mockResolvedValue(empty);
}

const STORED_SPEC: AttributeSpecification = {
  id: "stored-spec",
  title: "Раса",
  required: false,
  type: AttributeSpecificationType.Text,
  order: 0,
  isDescriptor: false,
  isHidden: false,
  maxLength: null,
  values: [],
};

const THE_VERSION = 7;

// Its buttons: an edit, "Сохранить" on the bar, and "Сохранить" in the
// data-loss question, which sends the refusal flags and keeps the answer.
const editorStub = defineComponent({
  props: {
    modelValue: { type: Object, default: null },
    hasCharacters: { type: Boolean, default: false },
    saving: { type: Boolean, default: false },
    saved: { type: Boolean, default: false },
    refusal: { type: String, default: "" },
    save: {
      type: Function as PropType<
        (draft: AttributeSchemaDraft, options?: object) => Promise<unknown>
      >,
      required: true,
    },
  },
  emits: ["cancel", "update:modelValue"],
  setup(props, { emit }) {
    const answered = ref("");
    return () =>
      h(
        "div",
        {
          class: "editor-stub",
          "data-has-characters": String(props.hasCharacters),
          "data-saved": String(props.saved),
          "data-refusal": props.refusal,
          "data-answered": answered.value,
        },
        [
          h("button", {
            class: "stub-rename",
            onClick: () =>
              emit("update:modelValue", {
                title: "",
                specifications: [{ ...STORED_SPEC, title: "Мировоззрение" }],
              }),
          }),
          h("button", {
            class: "stub-save",
            onClick: () => props.save(props.modelValue as AttributeSchemaDraft),
          }),
          h("button", {
            class: "stub-save-asked",
            onClick: async () => {
              const answer = (await props.save(
                props.modelValue as AttributeSchemaDraft,
                { ownsRefusal: true, ownsRateLimit: true },
              )) as { error?: { status: number } | null } | undefined;
              answered.value = String(answer?.error?.status ?? "ok");
            },
          }),
          h("button", { class: "stub-cancel", onClick: () => emit("cancel") }),
        ],
      );
  },
});

const character = (status: Character["status"]): Character => ({
  ...createMockCharacter(`character-${status}`, "Кто-то"),
  status,
});

type RosterAnswer = Awaited<ReturnType<typeof characterApi.getCharacters>>;

function serveRoster(refused: boolean) {
  const answer = refused
    ? { data: null, error: { status: 500, type: "", title: "", traceId: "" } }
    : { data: { resources: [], paging: null }, error: null };
  vi.spyOn(characterApi, "getCharacters").mockResolvedValue(
    answer as unknown as RosterAnswer,
  );
}

function mountSection(
  characters: Character[] = [],
  options: { rosterRefused?: boolean; rereadForReal?: boolean } = {},
) {
  serveEmptySources();
  serveRoster(options.rosterRefused ?? false);
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useGameDetailsStore();
  store.game = {
    ...createMockGame("the-game-guid", "Игра"),
    publicId: asServed("the-game"),
    schema: { specifications: [STORED_SPEC], version: THE_VERSION },
  };
  store.characters = characters;
  if (!options.rereadForReal) {
    vi.spyOn(store, "loadGame").mockResolvedValue({ ok: true });
  }

  const wrapper = mount(AttributeSchemaSection, {
    global: {
      plugins: [pinia],
      stubs: { AttributeSchemaEditor: editorStub },
    },
  });
  return { wrapper, store };
}

const sentBody = (replace: ReturnType<typeof vi.spyOn>) =>
  replace.mock.calls.at(-1)?.[1] as {
    specifications: AttributeSpecification[];
    version: number;
  };

const refusalOf = (wrapper: VueWrapper) =>
  wrapper.find(".editor-stub").attributes("data-refusal");

const draftTitles = (wrapper: VueWrapper) =>
  (
    wrapper
      .findComponent(editorStub)
      .props("modelValue") as AttributeSchemaDraft
  ).specifications.map((s) => s.title);

function serveReplacement(status?: number, title = "") {
  return vi.spyOn(gameApi, "replaceAttributeSchema").mockResolvedValue(
    status
      ? { data: null, error: { status, type: "", title, traceId: "" } }
      : ({
          data: { resource: { specifications: [], version: THE_VERSION + 1 } },
          error: null,
        } as unknown as Awaited<
          ReturnType<typeof gameApi.replaceAttributeSchema>
        >),
  );
}

afterEach(() => vi.restoreAllMocks());

describe("saving the sheet", () => {
  it("sends the edited set together with the version it was read at", async () => {
    const replace = serveReplacement();
    const { wrapper } = mountSection();
    await flushPromises();

    await wrapper.find(".stub-rename").trigger("click");
    await wrapper.find(".stub-save").trigger("click");
    await flushPromises();

    expect(sentBody(replace).specifications.map((s) => s.title)).toEqual([
      "Мировоззрение",
    ]);
    expect(sentBody(replace).version).toBe(THE_VERSION);
  });

  it("sends the version the draft was built on, not the one the details now hold", async () => {
    // Another section's save re-reads the game under an untouched draft.
    const replace = serveReplacement();
    const { wrapper, store } = mountSection();
    await flushPromises();

    await wrapper.find(".stub-rename").trigger("click");
    store.game = {
      ...(store.game as Game),
      schema: {
        specifications: [STORED_SPEC],
        version: THE_VERSION + 1,
      },
    };
    await flushPromises();

    await wrapper.find(".stub-save").trigger("click");
    await flushPromises();

    expect(sentBody(replace).version).toBe(THE_VERSION);
  });

  it("opens on the stored sheet with the identifiers the server gave it", async () => {
    // Fresh ids would empty every sheet in the game.
    const { wrapper } = mountSection();
    await flushPromises();

    const draft = wrapper
      .findComponent(editorStub)
      .props("modelValue") as AttributeSchemaDraft;
    expect(draft.specifications.map((s) => s.id)).toEqual([STORED_SPEC.id]);
  });
});

// The server words the conflict; the section says its words, not a copy.
const CONFLICT = "Пока вы правили, схему сохранил кто-то другой";

describe("a sheet somebody else has already changed", () => {
  it("says so in the section, keeps the draft and reads the sheet again", async () => {
    serveReplacement(409, CONFLICT);
    const { wrapper, store } = mountSection();
    await flushPromises();

    await wrapper.find(".stub-rename").trigger("click");
    await wrapper.find(".stub-save").trigger("click");
    await flushPromises();

    expect(refusalOf(wrapper)).toBe(CONFLICT);
    expect(draftTitles(wrapper)).toEqual(["Мировоззрение"]);
    expect(store.loadGame).toHaveBeenCalledWith("the-game", {
      keepCurrentOnError: true,
    });
  });

  it("carries the version that re-read brought when the lead saves again", async () => {
    const replace = serveReplacement(409, CONFLICT);
    const { wrapper, store } = mountSection();
    await flushPromises();

    vi.mocked(store.loadGame).mockImplementation(async () => {
      store.game = {
        ...(store.game as Game),
        schema: {
          specifications: [STORED_SPEC],
          version: THE_VERSION + 1,
        },
      };
      return { ok: true };
    });

    await wrapper.find(".stub-rename").trigger("click");
    await wrapper.find(".stub-save").trigger("click");
    await flushPromises();
    await wrapper.find(".stub-save").trigger("click");
    await flushPromises();

    expect(sentBody(replace).version).toBe(THE_VERSION + 1);
  });

  it("keeps the game, the draft and the line when that re-read is refused", async () => {
    // The shell draws the settings only while the game is in the store.
    serveReplacement(409, CONFLICT);
    vi.spyOn(gameApi, "getGame").mockResolvedValue({
      data: null,
      error: { status: 500, type: "", title: "", traceId: "" },
    } as unknown as Awaited<ReturnType<typeof gameApi.getGame>>);
    const { wrapper, store } = mountSection([], { rereadForReal: true });
    await flushPromises();

    await wrapper.find(".stub-rename").trigger("click");
    await wrapper.find(".stub-save").trigger("click");
    await flushPromises();

    expect(store.game).not.toBeNull();
    expect(draftTitles(wrapper)).toEqual(["Мировоззрение"]);
    expect(refusalOf(wrapper)).toBe(CONFLICT);
  });

  it("drops the line and the edit when the lead gives up on it", async () => {
    serveReplacement(409, CONFLICT);
    const { wrapper } = mountSection();
    await flushPromises();

    await wrapper.find(".stub-rename").trigger("click");
    await wrapper.find(".stub-save").trigger("click");
    await flushPromises();
    await wrapper.find(".stub-cancel").trigger("click");
    await flushPromises();

    expect(refusalOf(wrapper)).toBe("");
    expect(draftTitles(wrapper)).toEqual(["Раса"]);
  });
});

describe("the data-loss warning", () => {
  it("is raised while the game holds a character", async () => {
    const { wrapper } = mountSection([character("Active")]);
    await flushPromises();

    expect(wrapper.find(".editor-stub").attributes("data-has-characters")).toBe(
      "true",
    );
  });

  it("is raised while the roster has not answered", async () => {
    const { wrapper } = mountSection([], { rosterRefused: true });
    await flushPromises();

    expect(wrapper.find(".editor-stub").attributes("data-has-characters")).toBe(
      "true",
    );
  });

  it("is not raised by a roster that answered with nobody", async () => {
    const { wrapper } = mountSection([]);
    await flushPromises();

    expect(wrapper.find(".editor-stub").attributes("data-has-characters")).toBe(
      "false",
    );
  });
});

describe("the section of the character sheet", () => {
  it("is titled by the glossary's name of the sheet", async () => {
    const { wrapper } = mountSection();
    await flushPromises();

    expect(wrapper.get("h2").text()).toBe("Схема атрибутов");
  });
});

// The editor's own button answers a save; `saved` is all the section hands it.
const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

const savedFlag = (wrapper: VueWrapper) =>
  wrapper.find(".editor-stub").attributes("data-saved");

describe("reporting a save of the sheet", () => {
  beforeEach(() => {
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  it("does not call the sheet saved before anything has been saved", async () => {
    const { wrapper } = mountSection();
    await flushPromises();

    expect(savedFlag(wrapper)).toBe("false");
  });

  it("calls the sheet saved once the server has taken it", async () => {
    serveReplacement();
    const { wrapper } = mountSection();
    await flushPromises();

    await wrapper.find(".stub-rename").trigger("click");
    await wrapper.find(".stub-save").trigger("click");
    await flushPromises();

    expect(savedFlag(wrapper)).toBe("true");
  });

  it("says nothing in the corner about the save it has just shown", async () => {
    serveReplacement();
    const { wrapper } = mountSection();
    await flushPromises();

    await wrapper.find(".stub-rename").trigger("click");
    await wrapper.find(".stub-save").trigger("click");
    await flushPromises();

    expect(saidOutLoud()).toEqual([]);
  });

  it("stops calling it saved as soon as the draft is edited again", async () => {
    serveReplacement();
    const { wrapper } = mountSection();
    await flushPromises();

    await wrapper.find(".stub-save").trigger("click");
    await flushPromises();
    expect(savedFlag(wrapper)).toBe("true");

    await wrapper.find(".stub-rename").trigger("click");
    await flushPromises();

    expect(savedFlag(wrapper)).toBe("false");
  });

  it("does not call it saved when the server refuses the version", async () => {
    serveReplacement(409, CONFLICT);
    const { wrapper } = mountSection();
    await flushPromises();

    await wrapper.find(".stub-rename").trigger("click");
    await wrapper.find(".stub-save").trigger("click");
    await flushPromises();

    expect(savedFlag(wrapper)).toBe("false");
  });
});

// The question prints its own refusal, so the section says nothing of its own.
describe("a save sent from the data-loss question", () => {
  beforeEach(() => {
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  it("carries the flags that keep the interceptor quiet", async () => {
    const replace = serveReplacement();
    const { wrapper } = mountSection([character("Active")]);
    await flushPromises();

    await wrapper.find(".stub-save-asked").trigger("click");
    await flushPromises();

    expect(replace.mock.calls.at(-1)?.[2]).toEqual({
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(wrapper.find(".editor-stub").attributes("data-answered")).toBe("ok");
  });

  it("hands the refusal by version to the window and keeps the draft", async () => {
    serveReplacement(409, CONFLICT);
    const { wrapper, store } = mountSection([character("Active")]);
    await flushPromises();

    await wrapper.find(".stub-rename").trigger("click");
    await wrapper.find(".stub-save-asked").trigger("click");
    await flushPromises();

    expect(wrapper.find(".editor-stub").attributes("data-answered")).toBe(
      "409",
    );
    expect(refusalOf(wrapper)).toBe("");
    expect(draftTitles(wrapper)).toEqual(["Мировоззрение"]);
    expect(store.loadGame).toHaveBeenCalledWith("the-game", {
      keepCurrentOnError: true,
    });
  });

  it("hands any other refusal to the window without a plate", async () => {
    // A 400: a refusal the section would otherwise plate tells the paths apart.
    serveReplacement(400);
    const { wrapper } = mountSection([character("Active")]);
    await flushPromises();

    await wrapper.find(".stub-save-asked").trigger("click");
    await flushPromises();

    expect(wrapper.find(".editor-stub").attributes("data-answered")).toBe(
      "400",
    );
    expect(saidOutLoud()).toEqual([]);
    expect(refusalOf(wrapper)).toBe("");
  });
});

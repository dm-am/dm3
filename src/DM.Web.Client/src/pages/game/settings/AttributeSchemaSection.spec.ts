/**
 * @vitest-environment jsdom
 */

/**
 * Saving the character sheet of a game.
 *
 * A save replaces the whole set and the server drops the values of every
 * specification the new set leaves out, across every character of the game.
 * Two guards stand between a lead and that, and both are this section's to
 * carry: the version the sheet was read at, which the server refuses when
 * somebody else has saved since, and the warning the editor raises while the
 * game has characters.
 *
 * The refusal by version is the one that has to be shown rather than toasted.
 * The reader is standing in the editor holding a sheet they have just written;
 * throwing it away and reloading over them would cost them the work, so the
 * draft stays, the sheet is read again behind it, and one line says what
 * happened. That is the whole of doc 4.2.2.10 on this point.
 *
 * Both guards are answers to what the section does not know, so both are
 * pinned here at their weakest point: the version is the one the draft was
 * built on and not whatever the details happen to hold when Save is pressed,
 * and an unread roster is treated as a roster with characters in it.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { defineComponent, h, ref, type PropType } from "vue";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  gameApi,
  useGameDetailsStore,
  AttributeSpecificationType,
  type AttributeSchemaDraft,
  type AttributeSpecification,
  type Character,
  type Game,
} from "@/entities/game";
import { attributeSchemaTemplateApi } from "@/entities/attribute-schema-template";
import { useToast } from "@/shared/lib/composables/useToast";
import AttributeSchemaSection from "./AttributeSchemaSection.vue";

/**
 * The two reads behind the "Схема атрибутов" list. Served empty: the list is
 * the editor's own subject, and left unserved these would go to the network.
 */
function serveEmptySources() {
  vi.spyOn(attributeSchemaTemplateApi, "getTemplates").mockResolvedValue({
    data: { resources: [], paging: null },
    error: null,
  } as unknown as Awaited<
    ReturnType<typeof attributeSchemaTemplateApi.getTemplates>
  >);
  vi.spyOn(gameApi, "getParticipatingGames").mockResolvedValue({
    data: { resources: [], paging: null },
    error: null,
  } as unknown as Awaited<ReturnType<typeof gameApi.getParticipatingGames>>);
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

/**
 * The editor is a double: what it draws is its own spec's business, and what
 * this file needs of it is the draft it holds, the flags the section hands it,
 * and a way to press Save. Its buttons stand for editing an attribute, for
 * pressing "Сохранить" on the action bar, and for answering "Сохранить" in the
 * data-loss question, which sends the save with the refusal flags and keeps
 * what the section answered.
 */
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

const character = (status: Character["status"]): Character =>
  ({
    id: `character-${status}`,
    name: "Кто-то",
    status,
  }) as unknown as Character;

type RosterAnswer = Awaited<ReturnType<typeof gameApi.getCharacters>>;

/** The roster read the section runs when the store holds none. */
function serveRoster(refused: boolean) {
  const answer = refused
    ? { data: null, error: { status: 500, type: "", title: "", traceId: "" } }
    : { data: { resources: [], paging: null }, error: null };
  vi.spyOn(gameApi, "getCharacters").mockResolvedValue(
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
    id: "the-game-guid",
    publicId: "the-game",
    schema: { specifications: [STORED_SPEC], version: THE_VERSION },
  } as unknown as Game;
  store.characters = characters;
  // The re-read behind the section is a double everywhere except where the
  // question is what a refused read does to the page under it.
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

/** The body of the single replacement the section sent. */
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

function serveReplacement(status?: number) {
  return vi.spyOn(gameApi, "replaceAttributeSchema").mockResolvedValue(
    status
      ? { data: null, error: { status, type: "", title: "", traceId: "" } }
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
    // The information form one section up re-reads the game after its own save,
    // and the details move under a draft nobody has touched. A version taken
    // off them at that moment claims a sheet this draft was never written
    // against, and the server accepts the replacement it was meant to refuse.
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
    } as unknown as Game;
    await flushPromises();

    await wrapper.find(".stub-save").trigger("click");
    await flushPromises();

    expect(sentBody(replace).version).toBe(THE_VERSION);
  });

  it("opens on the stored sheet with the identifiers the server gave it", async () => {
    // A specification keeps the values characters wrote against it only while
    // the save sends its id back; a draft built with fresh ids would empty
    // every sheet in the game.
    const { wrapper } = mountSection();
    await flushPromises();

    const draft = wrapper
      .findComponent(editorStub)
      .props("modelValue") as AttributeSchemaDraft;
    expect(draft.specifications.map((s) => s.id)).toEqual([STORED_SPEC.id]);
  });
});

describe("a sheet somebody else has already changed", () => {
  it("says so in the section, keeps the draft and reads the sheet again", async () => {
    serveReplacement(409);
    const { wrapper, store } = mountSection();
    await flushPromises();

    await wrapper.find(".stub-rename").trigger("click");
    await wrapper.find(".stub-save").trigger("click");
    await flushPromises();

    expect(refusalOf(wrapper)).toBe(
      "Схему только что изменили. Проверьте и сохраните снова",
    );
    expect(draftTitles(wrapper)).toEqual(["Мировоззрение"]);
    expect(store.loadGame).toHaveBeenCalledWith("the-game", {
      keepCurrentOnError: true,
    });
  });

  it("carries the version that re-read brought when the lead saves again", async () => {
    // Otherwise the second attempt repeats the version the first was refused
    // for, and the reader is left pressing a button that can only refuse.
    const replace = serveReplacement(409);
    const { wrapper, store } = mountSection();
    await flushPromises();

    vi.mocked(store.loadGame).mockImplementation(async () => {
      store.game = {
        ...(store.game as Game),
        schema: {
          specifications: [STORED_SPEC],
          version: THE_VERSION + 1,
        },
      } as unknown as Game;
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
    // The shell draws the settings only while the details are in the store, so
    // emptying them over a refusal of a read nobody asked for takes the page
    // down, and the draft with it, in the one branch that promised to keep it.
    serveReplacement(409);
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
    expect(refusalOf(wrapper)).toBe(
      "Схему только что изменили. Проверьте и сохраните снова",
    );
  });

  it("drops the line and the edit when the lead gives up on it", async () => {
    serveReplacement(409);
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
    // The read of the roster was refused, so the section cannot tell whether
    // there are filled sheets behind the set it is about to replace. The
    // replacement is not reversible, so not knowing is a reason to ask.
    const { wrapper } = mountSection([], { rosterRefused: true });
    await flushPromises();

    expect(wrapper.find(".editor-stub").attributes("data-has-characters")).toBe(
      "true",
    );
  });

  it("is not raised by an application that was declined", async () => {
    // A declined application never entered the game, so there is no filled
    // sheet behind it to lose.
    const { wrapper } = mountSection([character("Declined")]);
    await flushPromises();

    expect(wrapper.find(".editor-stub").attributes("data-has-characters")).toBe(
      "false",
    );
  });
});

/**
 * How a save of the sheet is reported.
 *
 * Nothing on this section moves when one goes through: the table under the
 * editor holds the rows it held before, the page stays where it was, and for
 * that reason the save used to be announced by a message in the corner. The
 * answer belongs to the editor's own button instead, and the section is what
 * works out whether to draw it - `saved` is the whole of what it hands over.
 *
 * What each test catches, given that a button reading "Сохранить" forever
 * looks perfectly normal:
 *  - remember() left out of save(): the button never answers;
 *  - remember() above the refusal return: a save the server threw out reads as
 *    a save that happened;
 *  - the snapshot taken of what was sent rather than of the draft the re-read
 *    rebuilt: the button goes back to "Сохранить" over a sheet nobody touched,
 *    the moment the server normalises anything;
 *  - a snapshot that never moves again: the button stays "Сохранено" over a
 *    draft the lead has since rewritten;
 *  - the toast put back beside the button: one save announced twice.
 */
const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

/** What the section tells the editor about the draft under it. */
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
    serveReplacement(409);
    const { wrapper } = mountSection();
    await flushPromises();

    await wrapper.find(".stub-rename").trigger("click");
    await wrapper.find(".stub-save").trigger("click");
    await flushPromises();

    expect(savedFlag(wrapper)).toBe("false");
  });
});

/**
 * The save the data-loss question sends.
 *
 * The question prints a refusal in itself and closes on success alone, so the
 * section hands it the answer and says nothing of its own: a line under the
 * editor or a plate in the corner would say the same refusal a second time,
 * beside the window that already says it.
 */
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
    serveReplacement(409);
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
    // A 400 and not a 5xx: the section keeps quiet about a 5xx on either path,
    // and only a refusal it would otherwise put on a plate tells the two apart.
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

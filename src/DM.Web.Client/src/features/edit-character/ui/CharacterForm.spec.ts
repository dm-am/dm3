/**
 * @vitest-environment jsdom
 */

/**
 * CharacterForm Component Tests
 *
 * Covers the schema-driven character form:
 *  - one control per specification type (all 6 types)
 *  - required-field validation (error message + no API call)
 *  - list-with-modifier display (TextNumberList option labels and view mode)
 *  - hidden attributes absent from the payload are NOT fabricated client-side
 *  - submit payload maps only non-empty values
 *  - how a save is reported, which on this screen is the submit button
 *  - what the unsaved-work guard asks about after that save
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { defineComponent, h, nextTick } from "vue";
import { createMemoryHistory, createRouter, RouterView } from "vue-router";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import { CHARACTER_NAME_MAX_LENGTH } from "@/shared/lib/constants/game";
import { useToast } from "@/shared/lib/composables/useToast";
import { UnsavedChangesGuard } from "@/shared/ui/UnsavedChangesGuard";
import CharacterForm from "./CharacterForm.vue";
import {
  gameApi,
  AttributeSpecificationType,
  type AttributeSpecification,
  type Character,
} from "@/entities/game";

// BBCodeEditor drags the whole tiptap stack in — replace it with a light
// render-function stub (render fn, not `template`: the test build runs the
// runtime-only Vue, so string templates cannot be compiled).
vi.mock("@/shared/ui/BBCodeEditor", async () => {
  const { defineComponent, h } = await import("vue");
  const BBCodeEditor = defineComponent({
    name: "BBCodeEditor",
    props: {
      modelValue: { type: String, default: "" },
      context: { type: String, default: "" },
      maxLength: { type: Number, default: 0 },
    },
    emits: ["update:modelValue"],
    setup(props) {
      return () => h("div", { class: "bbcode-editor-stub" }, props.modelValue);
    },
  });
  return { BBCodeEditor };
});

// --- Fixtures ---------------------------------------------------------------

type SpecSeed = Pick<
  AttributeSpecification,
  "id" | "title" | "type" | "order"
> &
  Partial<AttributeSpecification>;

function makeSpec(seed: SpecSeed): AttributeSpecification {
  return {
    required: false,
    isDescriptor: false,
    isHidden: false,
    maxLength: null,
    values: [],
    ...seed,
  };
}

/** Full 6-type schema: one specification per AttributeSpecificationType. */
function makeSchema(specs?: AttributeSpecification[]): {
  specifications: AttributeSpecification[];
} {
  return {
    specifications: specs ?? [
      makeSpec({
        id: "text-spec",
        title: "Род занятий",
        type: AttributeSpecificationType.Text,
        order: 1,
        required: true,
        maxLength: 100,
      }),
      makeSpec({
        id: "number-spec",
        title: "Возраст",
        type: AttributeSpecificationType.Number,
        order: 2,
        maxLength: 3,
      }),
      makeSpec({
        id: "textlist-spec",
        title: "Класс",
        type: AttributeSpecificationType.TextList,
        order: 3,
        values: [
          { value: "Воин", modifier: null },
          { value: "Маг", modifier: null },
        ],
      }),
      makeSpec({
        id: "numberlist-spec",
        title: "Сила",
        type: AttributeSpecificationType.NumberList,
        order: 4,
        values: [
          { value: "10", modifier: null },
          { value: "12", modifier: null },
        ],
      }),
      makeSpec({
        id: "textnumberlist-spec",
        title: "Оружие",
        type: AttributeSpecificationType.TextNumberList,
        order: 5,
        values: [
          { value: "Меч", modifier: 2 },
          { value: "Лук", modifier: -1 },
        ],
      }),
      makeSpec({
        id: "bbcode-spec",
        title: "Биография",
        type: AttributeSpecificationType.BbCode,
        order: 6,
        maxLength: 5000,
      }),
    ],
  };
}

function makeCharacter(attributes: Record<string, unknown>[]): Character {
  return {
    id: "character-1",
    status: "Active",
    name: "Арагорн",
    isNpc: false,
    privacy: { isNpc: false, editByMaster: false, editPostByMaster: false },
    attributes,
    totalPostsCount: 0,
  } as unknown as Character;
}

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("CharacterForm", () => {
  // ============================================================================
  // RENDERING — ONE CONTROL PER SPECIFICATION TYPE
  // ============================================================================

  describe("Rendering (edit mode)", () => {
    it("renders a text input with maxlength for a Text specification", () => {
      const wrapper = mount(CharacterForm, {
        props: { schema: makeSchema(), gameId: "game-1" },
      });

      const input = wrapper.find("input#spec-text-spec");
      expect(input.exists()).toBe(true);
      expect(input.attributes("type")).toBe("text");
      expect(input.attributes("maxlength")).toBe("100");
    });

    it("stops the name field at the limit the server enforces", () => {
      const wrapper = mount(CharacterForm, {
        props: { schema: makeSchema(), gameId: "game-1" },
      });

      // Only the form's use of the constant is asserted here. That the number
      // itself equals the validator's and the column's is held on the server,
      // which is the only side able to read all three.
      const input = wrapper.find("input#character-name");
      expect(input.attributes("maxlength")).toBe(
        String(CHARACTER_NAME_MAX_LENGTH),
      );
    });

    it("renders a numeric input for a Number specification and strips non-digits", async () => {
      const wrapper = mount(CharacterForm, {
        props: { schema: makeSchema(), gameId: "game-1" },
      });

      const input = wrapper.find("input#spec-number-spec");
      expect(input.exists()).toBe(true);
      expect(input.attributes("inputmode")).toBe("numeric");
      expect(input.attributes("maxlength")).toBe("3");

      await input.setValue("1a2");
      expect((input.element as HTMLInputElement).value).toBe("12");
    });

    it("renders a Select for each of the three list specifications", () => {
      const wrapper = mount(CharacterForm, {
        props: { schema: makeSchema(), gameId: "game-1" },
      });

      // TextList + NumberList + TextNumberList -> exactly 3 native selects
      const selects = wrapper.findAll("select");
      expect(selects.length).toBe(3);

      // Each select carries the "Не выбрано" placeholder option
      for (const select of selects) {
        expect(select.find("option[disabled]").text()).toBe("Не выбрано");
      }
    });

    it("renders the BBCodeEditor for a BbCode specification with the spec max length", async () => {
      const wrapper = mount(CharacterForm, {
        props: { schema: makeSchema(), gameId: "game-1" },
      });

      // The editor is a defineAsyncComponent (it carries TipTap, which must
      // stay off the entry chunk), so it appears a microtask after mount.
      await flushPromises();

      const editor = wrapper.findComponent({ name: "BBCodeEditor" });
      expect(editor.exists()).toBe(true);
      expect(editor.props("maxLength")).toBe(5000);
      expect(editor.props("context")).toBe("info");
    });

    it("renders specification labels in schema order (sorted by `order`)", () => {
      const shuffled = makeSchema().specifications.reverse();
      const wrapper = mount(CharacterForm, {
        props: { schema: makeSchema(shuffled), gameId: "game-1" },
      });

      const labels = wrapper
        .findAll(".form-field-label label")
        .map((l) => l.text().replace(/\s*\*$/, ""));
      // First label is the character name field, then specs by `order`
      expect(labels).toEqual([
        "Имя персонажа",
        "Род занятий",
        "Возраст",
        "Класс",
        "Сила",
        "Оружие",
        "Биография",
      ]);
    });

    it("marks hidden specifications with the privacy indicator", () => {
      const schema = makeSchema([
        makeSpec({
          id: "text-spec",
          title: "Тайное знание",
          type: AttributeSpecificationType.Text,
          order: 1,
          isHidden: true,
        }),
      ]);
      const wrapper = mount(CharacterForm, {
        props: { schema, gameId: "game-1" },
      });

      expect(wrapper.find(".privacy-indicator").exists()).toBe(true);
      expect(wrapper.find(".privacy-indicator").text()).toContain(
        "Приватность",
      );
    });
  });

  // ============================================================================
  // REQUIRED FIELDS
  // ============================================================================

  describe("Required fields", () => {
    it("shows an asterisk only on required specification labels", () => {
      const wrapper = mount(CharacterForm, {
        props: { schema: makeSchema(), gameId: "game-1" },
      });

      // Only "Род занятий" is required in the fixture
      expect(wrapper.findAll(".required-mark").length).toBe(1);
    });

    it("shows a validation error and does not call the API when a required field is empty", async () => {
      const createSpy = vi.spyOn(gameApi, "createCharacter");
      const wrapper = mount(CharacterForm, {
        props: { schema: makeSchema(), gameId: "game-1" },
      });

      await wrapper.find("input#character-name").setValue("Арагорн");
      await wrapper.find("form").trigger("submit");
      await nextTick();

      expect(wrapper.find(".form-field-error").exists()).toBe(true);
      expect(wrapper.find(".form-field-error").text()).toBe(
        "Обязательное поле",
      );
      expect(createSpy).not.toHaveBeenCalled();
    });

    it("clears the error once the required field is filled after a failed submit", async () => {
      const wrapper = mount(CharacterForm, {
        props: { schema: makeSchema(), gameId: "game-1" },
      });

      await wrapper.find("form").trigger("submit");
      await nextTick();
      expect(wrapper.findAll(".form-field-error").length).toBeGreaterThan(0);

      await wrapper.find("input#character-name").setValue("Арагорн");
      await wrapper.find("input#spec-text-spec").setValue("Следопыт");
      await nextTick();

      expect(wrapper.find(".form-field-error").exists()).toBe(false);
    });
  });

  // ============================================================================
  // LIST WITH MODIFIER
  // ============================================================================

  describe("List with modifier (TextNumberList)", () => {
    it("shows option labels with a signed modifier; plain lists stay unmodified", () => {
      const wrapper = mount(CharacterForm, {
        props: { schema: makeSchema(), gameId: "game-1" },
      });

      const optionTexts = wrapper
        .findAll("option:not([disabled])")
        .map((o) => o.text());
      // TextNumberList: value with signed modifier
      expect(optionTexts).toContain("Меч (+2)");
      expect(optionTexts).toContain("Лук (-1)");
      // TextList / NumberList: plain values, no fabricated modifier
      expect(optionTexts).toContain("Воин");
      expect(optionTexts).toContain("12");
    });

    it("renders the stored modifier next to the value in view mode", () => {
      const character = makeCharacter([
        {
          id: "textnumberlist-spec",
          title: "Оружие",
          value: "Меч",
          modifier: "+2",
        },
      ]);
      const wrapper = mount(CharacterForm, {
        props: { schema: makeSchema(), mode: "view", character },
      });

      const values = wrapper.findAll(".view-value").map((v) => v.text());
      expect(values).toContain("Меч (+2)");
    });
  });

  // ============================================================================
  // HIDDEN ATTRIBUTES — NEVER FABRICATED CLIENT-SIDE
  // ============================================================================

  describe("Hidden attributes absent from the payload", () => {
    it("renders the missing-value token in view mode instead of fabricating a value", () => {
      const schema = makeSchema([
        makeSpec({
          id: "text-spec",
          title: "Род занятий",
          type: AttributeSpecificationType.Text,
          order: 1,
        }),
        makeSpec({
          id: "hidden-spec",
          title: "Тайное знание",
          type: AttributeSpecificationType.Text,
          order: 2,
          isHidden: true,
        }),
      ]);
      // Server omitted the hidden attribute for this (unprivileged) viewer
      const character = makeCharacter([
        { id: "text-spec", title: "Род занятий", value: "Следопыт" },
      ]);

      const wrapper = mount(CharacterForm, {
        props: { schema, mode: "view", character },
      });

      const rows = wrapper.findAll(".view-row");
      expect(rows.length).toBe(2);
      // Visible attribute keeps its server value
      expect(rows[0].find(".view-value").text()).toBe("Следопыт");
      // Hidden attribute row shows the missing-value token, no fabricated value
      expect(rows[1].find(".view-value__empty").exists()).toBe(true);
      expect(rows[1].find(".view-value").text()).toBe(VALUE_UNAVAILABLE);
    });

    it("omits untouched (empty) attributes from the submit payload", async () => {
      // The answer is the single-resource envelope the server sends, not the
      // character on its own: the form is what takes the wrapper off.
      const created = makeCharacter([]);
      const createSpy = vi
        .spyOn(gameApi, "createCharacter")
        .mockResolvedValue({ data: { resource: created }, error: null });
      const wrapper = mount(CharacterForm, {
        props: { schema: makeSchema(), gameId: "game-1" },
      });

      await wrapper.find("input#character-name").setValue("Арагорн");
      await wrapper.find("input#spec-text-spec").setValue("Следопыт");
      await wrapper.find("form").trigger("submit");
      await flushPromises();

      expect(createSpy).toHaveBeenCalledTimes(1);
      const [gameId, payload] = createSpy.mock.calls[0];
      expect(gameId).toBe("game-1");
      expect(payload.name).toBe("Арагорн");
      // Only the filled attribute is sent — nothing fabricated for the
      // untouched Number / list / BbCode specifications
      expect(payload.attributes).toEqual([
        { id: "text-spec", value: "Следопыт" },
      ]);
      // Saved character is emitted back to the caller, unwrapped
      expect(wrapper.emitted("saved")?.[0]).toEqual([created]);
    });
  });
});

// ============================================================================
// HOW A SAVE IS REPORTED
// ============================================================================

/**
 * The sheet after a save holds the values the reader typed, the form stays
 * open and the page does not move, so nothing on screen says the save
 * happened - and a message in the corner is what used to say it instead.
 * The submit button says it now: "Сохранено" and inert while the sheet
 * matches the one the server has, "Сохранить" again the moment a field moves.
 *
 * What each test below catches, given that a button reading "Сохранить"
 * forever looks perfectly normal:
 *  - the label written as a constant again: the button never answers;
 *  - `:valid="isValid"` without `&& !saved`: the sheet can be sent twice with
 *    nothing between the two sends;
 *  - the saved snapshot taken above the error return: a refused save reads as
 *    a good one;
 *  - a snapshot that never moves after the first save: the button stays
 *    "Сохранено" over a sheet the reader has since rewritten;
 *  - a toast put back beside the button: one save announced twice.
 */

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

/** A one-field sheet: this half of the file is about the button, not the fields. */
const SHEET = () =>
  makeSchema([
    makeSpec({
      id: "text-spec",
      title: "Род занятий",
      type: AttributeSpecificationType.Text,
      order: 1,
    }),
  ]);

/** The props of a sheet that exists — the surface where a save reports itself. */
const editProps = () => ({
  schema: SHEET(),
  characterId: "character-1",
  initialName: "Арагорн",
  initialValues: { "text-spec": "Следопыт" },
});

/** The server accepts the save and answers with the character it stored. */
function serveUpdate() {
  return vi.spyOn(gameApi, "updateCharacter").mockResolvedValue({
    data: { resource: makeCharacter([]) },
    error: null,
  } as unknown as Awaited<ReturnType<typeof gameApi.updateCharacter>>);
}

/** The submit button of the form, the one control that answers for the save. */
const submitButton = (wrapper: VueWrapper) => wrapper.find(".controls button");

/** What the button says, and whether it can still be pressed. */
const answer = (wrapper: VueWrapper) => ({
  label: submitButton(wrapper).text(),
  inert: submitButton(wrapper).attributes("disabled") !== undefined,
});

/** Press the submit and let the request settle. */
async function save(wrapper: VueWrapper) {
  await wrapper.find("form").trigger("submit");
  await flushPromises();
}

describe("CharacterForm save confirmation", () => {
  beforeEach(() => {
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  it("does not claim a save the reader has not made", () => {
    const wrapper = mount(CharacterForm, { props: editProps() });

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });

  it("reads Сохранено and takes no press once the server confirms", async () => {
    serveUpdate();
    const wrapper = mount(CharacterForm, { props: editProps() });

    await save(wrapper);

    expect(answer(wrapper)).toEqual({ label: "Сохранено", inert: true });
  });

  it("says nothing in the corner about the save it has just shown", async () => {
    serveUpdate();
    const wrapper = mount(CharacterForm, { props: editProps() });

    await save(wrapper);

    expect(saidOutLoud()).toEqual([]);
  });

  it("offers the save again as soon as an attribute changes", async () => {
    serveUpdate();
    const wrapper = mount(CharacterForm, { props: editProps() });
    await save(wrapper);

    await wrapper.find("input#spec-text-spec").setValue("Следопыт из Арнора");

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });

  // The name is held apart from the attribute values, so it is asked for
  // separately: a comparison written over the values alone leaves a renamed
  // character reading "Сохранено".
  it("offers the save again as soon as the name changes", async () => {
    serveUpdate();
    const wrapper = mount(CharacterForm, { props: editProps() });
    await save(wrapper);

    await wrapper.find("input#character-name").setValue("Странник");

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });

  it("keeps offering the save after the server refuses it", async () => {
    vi.spyOn(gameApi, "updateCharacter").mockResolvedValue({
      data: null,
      error: { status: 400, type: "", title: "", traceId: "t" },
    } as unknown as Awaited<ReturnType<typeof gameApi.updateCharacter>>);
    const wrapper = mount(CharacterForm, { props: editProps() });

    await save(wrapper);

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
    expect(wrapper.find(".form-error").text()).toBe(
      "Не удалось сохранить персонажа",
    );
    expect(saidOutLoud()).toEqual([]);
  });

  // A sheet that does not exist yet is not reported here at all: the create
  // leaves this form for the character it made, and that page is the answer.
  it("keeps its own word on a character that has no id yet", async () => {
    vi.spyOn(gameApi, "createCharacter").mockResolvedValue({
      data: { resource: makeCharacter([]) },
      error: null,
    } as unknown as Awaited<ReturnType<typeof gameApi.createCharacter>>);
    const wrapper = mount(CharacterForm, {
      props: { schema: SHEET(), gameId: "game-1", initialName: "Арагорн" },
    });

    expect(answer(wrapper)).toEqual({
      label: "Создать персонажа",
      inert: false,
    });

    await save(wrapper);

    expect(submitButton(wrapper).text()).toBe("Создать персонажа");
  });
});

// ============================================================================
// LEAVING THE PAGE AFTER A SAVE
// ============================================================================

/**
 * The guard against losing a half-written sheet used to read a latch: one
 * boolean, set by the first save and never cleared. Everything typed after
 * that save left the page in silence - which is the very loss the guard was
 * added for, and the saved-state comparison is what fixes both answers at
 * once.
 *
 * A real router is used because the guard registers against the matched route
 * record; with a stub it would register against nothing and pass regardless.
 */
const Elsewhere = defineComponent({ setup: () => () => h("div", "elsewhere") });

const SheetPage = defineComponent({
  setup() {
    // Built once: a fresh schema object on every render would hand the form a
    // new prop identity for nothing.
    const props = editProps();
    return () => h(CharacterForm, props);
  },
});

async function mountAtSheet() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: "/sheet", component: SheetPage },
      { path: "/elsewhere", component: Elsewhere },
    ],
  });
  router.push("/sheet");
  await router.isReady();

  const wrapper = mount(defineComponent({ setup: () => () => h(RouterView) }), {
    global: { plugins: [router] },
  });
  await flushPromises();

  return { router, wrapper };
}

describe("CharacterForm unsaved-work guard", () => {
  it("lets an untouched sheet go without a word", async () => {
    const { router } = await mountAtSheet();

    await router.push("/elsewhere");

    expect(router.currentRoute.value.path).toBe("/elsewhere");
  });

  it("asks again about leaving when the sheet is edited after a save", async () => {
    serveUpdate();
    const { router, wrapper } = await mountAtSheet();
    await save(wrapper);

    await wrapper.find("input#spec-text-spec").setValue("Следопыт из Арнора");
    const leaving = router.push("/elsewhere");
    await flushPromises();

    expect(router.currentRoute.value.path).toBe("/sheet");
    expect(document.body.textContent).toContain("Уйти со страницы?");

    wrapper.findComponent(UnsavedChangesGuard).vm.answer(true);
    await leaving;
    await flushPromises();
    expect(router.currentRoute.value.path).toBe("/elsewhere");
  });

  it("lets the sheet go without a word while it matches the save", async () => {
    serveUpdate();
    const { router, wrapper } = await mountAtSheet();

    await wrapper.find("input#spec-text-spec").setValue("Следопыт из Арнора");
    await save(wrapper);
    await router.push("/elsewhere");

    expect(router.currentRoute.value.path).toBe("/elsewhere");
  });
});

// ============================================================================
// WHO SAYS THE REFUSAL
// ============================================================================

/**
 * A refused save has one carrier, and on this screen it is the strip under the
 * fields: it holds the sentence the server sent, it stays until the reader
 * corrects something, and it is where the rest of the form's answers already
 * are. The response interceptor used to raise a plate over it saying the same
 * thing - on a 5xx the identical sentence, because both sides read it out of
 * the same problem document.
 *
 * The flag on the request is what the interceptor reads to know it must keep
 * quiet, so that is what these check: the interceptor itself lives below the
 * api double this file installs, and never runs here.
 */
describe("CharacterForm refusal", () => {
  const OWNED = { ownsRefusal: true, ownsRateLimit: true };

  it("sends a save of an existing sheet with the refusal taken over", async () => {
    const update = serveUpdate();
    const wrapper = mount(CharacterForm, { props: editProps() });

    await save(wrapper);

    expect(update).toHaveBeenCalledWith(
      "character-1",
      expect.any(Object),
      OWNED,
    );
  });

  it("sends the creation of a sheet the same way", async () => {
    const create = vi.spyOn(gameApi, "createCharacter").mockResolvedValue({
      data: { resource: makeCharacter([]) },
      error: null,
    } as unknown as Awaited<ReturnType<typeof gameApi.createCharacter>>);
    const wrapper = mount(CharacterForm, {
      props: { schema: SHEET(), gameId: "game-1", initialName: "Арагорн" },
    });

    await save(wrapper);

    expect(create).toHaveBeenCalledWith("game-1", expect.any(Object), OWNED);
  });

  // Taking the wait over costs the reader nothing only if the strip says what
  // the plate would have: the seconds the server asked for. The title the API
  // sends with a rate limit names no number at all.
  it("names the wait under the fields when the save is rate limited", async () => {
    vi.spyOn(gameApi, "updateCharacter").mockResolvedValue({
      data: null,
      error: {
        status: 429,
        type: "",
        title: "Слишком много запросов",
        traceId: "t",
        retryAfterSeconds: 30,
      },
    } as unknown as Awaited<ReturnType<typeof gameApi.updateCharacter>>);
    const wrapper = mount(CharacterForm, { props: editProps() });

    await save(wrapper);

    expect(wrapper.find(".form-error").text()).toBe(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });

  // The reason a refusal names is the whole point of keeping it here. The
  // interceptor relays that same reason - both sides read it out of one problem
  // document with describeFailure - so what the strip changes is the carrier
  // and not the sentence: it stands under the fields the reader is about to
  // correct, and it stays there until something is corrected.
  it("reads out the reason the server named for a refusal", async () => {
    vi.spyOn(gameApi, "updateCharacter").mockResolvedValue({
      data: null,
      error: {
        status: 403,
        type: "",
        title: "Мастер закрыл правку листов",
        traceId: "t",
      },
    } as unknown as Awaited<ReturnType<typeof gameApi.updateCharacter>>);
    const wrapper = mount(CharacterForm, { props: editProps() });

    await save(wrapper);

    expect(wrapper.find(".form-error").text()).toBe(
      "Мастер закрыл правку листов",
    );
  });

  // The status a reader can correct nothing about, and the one a screen that
  // took the refusal over is likeliest to drop on the floor. The API titles a
  // 500 "Ошибка сервера" (ExceptionProblemDetailsFactoryExtensions.CreateFrom,
  // and generalRefusal.spec.ts holds the client copy of those words equal to
  // it), so that is the sentence the plate would have carried and the sentence
  // the strip has to carry in its place. A strip left empty here is the save
  // failing in silence.
  it("says a broken server in the words the server titled it with", async () => {
    vi.spyOn(gameApi, "updateCharacter").mockResolvedValue({
      data: null,
      error: {
        status: 500,
        type: "",
        title: "Ошибка сервера",
        traceId: "t",
      },
    } as unknown as Awaited<ReturnType<typeof gameApi.updateCharacter>>);
    const wrapper = mount(CharacterForm, { props: editProps() });

    await save(wrapper);

    expect(wrapper.find(".form-error").text()).toBe("Ошибка сервера");
  });

  // The two statuses no flag reaches: the interceptor raises its plate for an
  // expired session and for a request that never came back before it looks at
  // what the request claimed. The strip used to add a second sentence under
  // that plate - "Не удалось сохранить персонажа" beside "Сессия истекла,
  // войдите снова" - one event told twice in two wordings. What the strip
  // would have added is on screen anyway: the button still reads "Сохранить".
  it.each([
    ["an expired session", 401, "Сессия истекла"],
    ["a request that never came back", 0, ""],
  ])(
    "leaves %s to the plate and says nothing under the fields",
    async (_, status, title) => {
      vi.spyOn(gameApi, "updateCharacter").mockResolvedValue({
        data: null,
        error: { status, type: "", title, traceId: "t" },
      } as unknown as Awaited<ReturnType<typeof gameApi.updateCharacter>>);
      const wrapper = mount(CharacterForm, { props: editProps() });

      await save(wrapper);

      expect(wrapper.find(".form-error").exists()).toBe(false);
      expect(wrapper.text()).not.toContain("Не удалось сохранить персонажа");
      expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
    },
  );

  // Whoever ends up carrying the sentence, a refusal takes nothing off the
  // screen: the text the reader typed is still in the fields and the button
  // takes the next press. Without both, the answer to a refusal is retyping
  // the sheet.
  it("keeps the typed sheet and takes another press after a refusal", async () => {
    vi.spyOn(gameApi, "updateCharacter").mockResolvedValue({
      data: null,
      error: {
        status: 403,
        type: "",
        title: "Мастер закрыл правку листов",
        traceId: "t",
      },
    } as unknown as Awaited<ReturnType<typeof gameApi.updateCharacter>>);
    const wrapper = mount(CharacterForm, { props: editProps() });
    await wrapper.find("input#character-name").setValue("Странник");
    await wrapper.find("input#spec-text-spec").setValue("Следопыт из Арнора");

    await save(wrapper);

    const typed = (selector: string) =>
      (wrapper.find(selector).element as HTMLInputElement).value;

    expect(typed("input#character-name")).toBe("Странник");
    expect(typed("input#spec-text-spec")).toBe("Следопыт из Арнора");
    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });
});

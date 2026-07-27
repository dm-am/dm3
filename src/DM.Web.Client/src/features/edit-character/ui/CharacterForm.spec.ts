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
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { nextTick } from "vue";
import CharacterForm from "./CharacterForm.vue";
import {
  gameApi,
  AttributeSchemaType,
  AttributeSpecificationType,
  type AttributeSchema,
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
    values: null,
    ...seed,
  };
}

/** Full 6-type schema: one specification per AttributeSpecificationType. */
function makeSchema(
  specs?: AttributeSpecification[],
  title = "Тестовая схема",
): AttributeSchema {
  return {
    id: "schema-1",
    title,
    author: null,
    type: AttributeSchemaType.Public,
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

    it("renders the BBCodeEditor for a BbCode specification with the spec max length", () => {
      const wrapper = mount(CharacterForm, {
        props: { schema: makeSchema(), gameId: "game-1" },
      });

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
    it("renders an empty dash in view mode instead of fabricating a value", () => {
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
      // Hidden attribute row shows the empty dash, no fabricated value
      expect(rows[1].find(".view-value__empty").exists()).toBe(true);
      expect(rows[1].find(".view-value").text()).toBe("—");
    });

    it("omits untouched (empty) attributes from the submit payload", async () => {
      const createSpy = vi
        .spyOn(gameApi, "createCharacter")
        .mockResolvedValue({ data: makeCharacter([]), error: null });
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
      // Saved character is emitted back to the caller
      expect(wrapper.emitted("saved")).toBeTruthy();
    });
  });
});

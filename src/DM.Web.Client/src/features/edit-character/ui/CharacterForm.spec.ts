/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { nextTick } from "vue";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import { CHARACTER_NAME_MAX_LENGTH } from "@/shared/lib/constants/game";
import CharacterForm from "./CharacterForm.vue";
import { characterApi, AttributeSpecificationType } from "@/entities/game";
import { makeCharacter, makeSchema, makeSpec } from "./CharacterForm.fixtures";

// Render function, not `template`: the test build runs the runtime-only Vue.
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

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("CharacterForm", () => {
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

      // That the number equals the validator's is held on the server.
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

      const selects = wrapper.findAll("select");
      expect(selects.length).toBe(3);

      for (const select of selects) {
        expect(select.find("option[disabled]").text()).toBe("Не выбрано");
      }
    });

    it("renders the BBCodeEditor for a BbCode specification with the spec max length", async () => {
      const wrapper = mount(CharacterForm, {
        props: { schema: makeSchema(), gameId: "game-1" },
      });

      // The editor is an async component (TipTap stays off the entry chunk).
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

  describe("Required fields", () => {
    it("shows an asterisk only on required specification labels", () => {
      const wrapper = mount(CharacterForm, {
        props: { schema: makeSchema(), gameId: "game-1" },
      });

      expect(wrapper.findAll(".required-mark").length).toBe(1);
    });

    it("shows a validation error and does not call the API when a required field is empty", async () => {
      const createSpy = vi.spyOn(characterApi, "createCharacter");
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

  describe("List with modifier (TextNumberList)", () => {
    it("shows option labels with a signed modifier; plain lists stay unmodified", () => {
      const wrapper = mount(CharacterForm, {
        props: { schema: makeSchema(), gameId: "game-1" },
      });

      const optionTexts = wrapper
        .findAll("option:not([disabled])")
        .map((o) => o.text());
      expect(optionTexts).toContain("Меч (+2)");
      expect(optionTexts).toContain("Лук (-1)");
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
      // The server omitted the hidden attribute for this viewer.
      const character = makeCharacter([
        { id: "text-spec", title: "Род занятий", value: "Следопыт" },
      ]);

      const wrapper = mount(CharacterForm, {
        props: { schema, mode: "view", character },
      });

      const rows = wrapper.findAll(".view-row");
      expect(rows.length).toBe(2);
      expect(rows[0].find(".view-value").text()).toBe("Следопыт");
      expect(rows[1].find(".view-value__empty").exists()).toBe(true);
      expect(rows[1].find(".view-value").text()).toBe(VALUE_UNAVAILABLE);
    });

    it("omits untouched (empty) attributes from the submit payload", async () => {
      // The single-resource envelope: the form takes the wrapper off.
      const created = makeCharacter([]);
      const createSpy = vi
        .spyOn(characterApi, "createCharacter")
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
      expect(payload.attributes).toEqual([
        { id: "text-spec", value: "Следопыт" },
      ]);
      expect(wrapper.emitted("saved")?.[0]).toEqual([created]);
    });
  });
});

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
import { gameApi, type Tag } from "@/entities/game";
import type { BadRequestError } from "@/shared/api/models/common";

vi.mock("vue-router", () => ({
  useRouter: () => ({ push: vi.fn() }),
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

vi.mock("@/features/attribute-schema-editor/@x/create-game", async () => {
  const { defineComponent, h } = await import("vue");
  return {
    AttributeSchemaEditor: defineComponent({
      name: "AttributeSchemaEditor",
      setup: () => () => h("div", { class: "schema-editor-stub" }),
    }),
  };
});

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

const mountForm = () =>
  mount(CreateGameForm, { global: { plugins: [createPinia()] } });

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
});

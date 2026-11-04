/**
 * @vitest-environment jsdom
 */

/**
 * The general settings of a game: the tags a save sends, and the submit button
 * that answers the save. The tag selector is mounted for real, only its catalog
 * stubbed.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { createRouter, createMemoryHistory } from "vue-router";
import { gameApi, useGameDetailsStore, type Game } from "@/entities/game";
import { useAuthStore, type User } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import type { GeneralError } from "@/shared/api/models/common";
import GameInfoSection from "./GameInfoSection.vue";
import { problem } from "@/shared/lib/testing/problem";
import { expectRefusalUnderField } from "@/shared/lib/testing/formField";

// The editor is tiptap; the form reads its value and nothing else.
vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeEditor(),
);

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

const CATALOG = [
  { id: 3, title: "Фэнтези", groupTitle: "Жанр", sortOrder: 1 },
  { id: 7, title: "Медленный", groupTitle: "Жанр", sortOrder: 2 },
  { id: 9, title: "Хоррор", groupTitle: "Жанр", sortOrder: 3 },
];

/** The settings of a game; the reader, when named, is signed in. */
function mountSection(tagIds: number[], readerId?: string) {
  const pinia = createPinia();
  setActivePinia(pinia);
  if (readerId) useAuthStore().user = { id: readerId } as User;
  const store = useGameDetailsStore();
  store.game = {
    id: "the-game-guid",
    publicId: "the-game",
    master: { id: "master-id", username: "Мастер" },
    title: "Игра",
    system: "GURPS",
    setting: "Свой мир",
    tagIds,
    recruitment: { isOpen: true },
    privacySettings: { viewPrivates: false, viewDice: true },
  } as unknown as Game;
  vi.spyOn(store, "loadGame").mockResolvedValue({ ok: true });

  // The link to the game is resolved by the router's table.
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: "/", component: { render: () => null } },
      { name: "game", path: "/game/:id", component: { render: () => null } },
    ],
  });

  return mount(GameInfoSection, { global: { plugins: [pinia, router] } });
}

const chips = (wrapper: ReturnType<typeof mountSection>) =>
  wrapper.findAll(".tag-button").map((b) => ({
    title: b.text(),
    selected: b.classes().includes("selected"),
  }));

const chip = (wrapper: ReturnType<typeof mountSection>, title: string) =>
  wrapper.findAll(".tag-button").find((b) => b.text() === title)!;

const sentTags = (update: ReturnType<typeof vi.fn>) =>
  update.mock.calls.at(-1)?.[1]?.tags;

/** The tag catalog and the save, answered; the save is returned. */
function stubTheApi() {
  vi.spyOn(gameApi, "getTags").mockResolvedValue({
    data: { resources: CATALOG },
    error: null,
  } as unknown as Awaited<ReturnType<typeof gameApi.getTags>>);
  const update = vi.fn().mockResolvedValue({ data: null, error: null });
  vi.spyOn(gameApi, "updateGame").mockImplementation(
    update as unknown as typeof gameApi.updateGame,
  );
  return update;
}

describe("GameInfoSection tags", () => {
  let update: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    update = stubTheApi();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("draws the selector and lights the tags the game already carries", async () => {
    const wrapper = mountSection([3, 9]);
    await flushPromises();

    expect(chips(wrapper)).toEqual([
      { title: "Фэнтези", selected: true },
      { title: "Медленный", selected: false },
      { title: "Хоррор", selected: true },
    ]);
  });

  it("sends the whole set after a tag is added and another removed", async () => {
    const wrapper = mountSection([3, 9]);
    await flushPromises();

    await chip(wrapper, "Хоррор").trigger("click");
    await chip(wrapper, "Медленный").trigger("click");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(update).toHaveBeenCalledTimes(1);
    expect(sentTags(update)).toEqual([3, 7]);
  });

  it("sends an empty list when the last tag is taken off", async () => {
    const wrapper = mountSection([3]);
    await flushPromises();

    await chip(wrapper, "Фэнтези").trigger("click");
    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(sentTags(update)).toEqual([]);
  });

  it("does not send the tags of a game saved without touching them", async () => {
    const wrapper = mountSection([3, 7]);
    await flushPromises();

    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(sentTags(update)).toBeUndefined();
    expect(update.mock.calls.at(-1)?.[1]?.title).toBeDefined();
  });
});

const submitButton = (wrapper: VueWrapper) => wrapper.find(".controls button");

const answer = (wrapper: VueWrapper) => ({
  label: submitButton(wrapper).text(),
  inert: submitButton(wrapper).attributes("disabled") !== undefined,
});

async function save(wrapper: VueWrapper) {
  await wrapper.find("form").trigger("submit");
  await flushPromises();
}

// A button that always reads "Сохранить" looks perfectly normal on screen.
describe("GameInfoSection save confirmation", () => {
  let update: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    update = stubTheApi();
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("does not claim a save the reader has not made", async () => {
    const wrapper = mountSection([3]);
    await flushPromises();

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });

  it("reads Сохранено and stops answering once the server confirms", async () => {
    const wrapper = mountSection([3]);
    await flushPromises();

    await save(wrapper);

    expect(answer(wrapper)).toEqual({ label: "Сохранено", inert: true });
  });

  it("says nothing in the corner about the save it has just shown", async () => {
    const wrapper = mountSection([3]);
    await flushPromises();

    await save(wrapper);

    expect(saidOutLoud()).toEqual([]);
  });

  it("offers the save again as soon as the title changes", async () => {
    const wrapper = mountSection([3]);
    await flushPromises();
    await save(wrapper);

    await wrapper.find("#game-title").setValue("Другая игра");

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });

  it("offers the save again when the recruitment switch is flipped", async () => {
    const wrapper = mountSection([3]);
    await flushPromises();
    await save(wrapper);

    // The game mounted here is recruiting.
    await wrapper.findAll(".checkbox-label input")[0].setValue(false);

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });

  it("offers the save again when a tag is taken off", async () => {
    const wrapper = mountSection([3]);
    await flushPromises();
    await save(wrapper);

    await chip(wrapper, "Фэнтези").trigger("click");

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });

  // The reload writes the fields again, so the form is remembered after it.
  it("stays saved while the reload writes the form back", async () => {
    const wrapper = mountSection([3]);
    await flushPromises();
    const store = useGameDetailsStore();
    vi.spyOn(store, "loadGame").mockImplementation(async () => {
      store.game = { ...store.game!, tagIds: [3, 7] } as unknown as Game;
      return { ok: true };
    });

    await chip(wrapper, "Медленный").trigger("click");
    await save(wrapper);

    expect(sentTags(update)).toEqual([3, 7]);
    expect(answer(wrapper)).toEqual({ label: "Сохранено", inert: true });
  });

  it("keeps offering the save after the server refuses it", async () => {
    const wrapper = mountSection([3]);
    await flushPromises();
    update.mockResolvedValue({
      data: null,
      error: { status: 400, title: "", type: "", traceId: "t" } as GeneralError,
    });

    await save(wrapper);

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
    // Where the press was, in no plate (UI_STANDARDS "Всплывающие сообщения").
    expect(wrapper.get(".action-result").text()).toBe(
      "Не удалось сохранить информацию игры",
    );
    expect(saidOutLoud()).toEqual([]);
  });
});

// DM2's ModuleSettings: its captions in its order and words.
describe("GameInfoSection as DM2's settings", () => {
  beforeEach(() => {
    stubTheApi();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("captions its fields in DM2's order and words", async () => {
    const wrapper = mountSection([3]);
    await flushPromises();

    expect(
      wrapper.findAll(".form-field-label label").map((l) => l.text()),
    ).toEqual([
      "Название:",
      "Количество игроков:",
      "Ссылка на игру:",
      "Система:",
      "Сеттинг:",
      "Обсуждение игры:",
      "Теги:",
    ]);
    expect(wrapper.findAll(".checkbox-label").map((l) => l.text())).toEqual([
      "Набор открыт",
      "Показывать всем результаты бросков",
      "Показывать всем скрытые блоки",
    ]);
  });

  it("gives the address of the game as a plain link, with nothing to copy it", async () => {
    const wrapper = mountSection([3]);
    await flushPromises();

    const link = wrapper.get(".game-link a");
    expect(link.attributes("href")).toBe("/game/the-game");
    expect(link.text()).toBe(`${window.location.origin}/game/the-game`);
    expect(wrapper.find(".game-link button").exists()).toBe(false);
    // The caption says what the link is; no line under it says it again.
    expect(
      wrapper
        .findAll(".form-field")
        .find((field) => field.text().includes("Ссылка на игру:"))
        ?.find(".form-field-hint")
        .exists(),
    ).toBe(false);
  });

  it("offers the discussion open to everyone or read-only to outsiders, and no third mode", async () => {
    const wrapper = mountSection([3]);
    await flushPromises();

    const options = wrapper
      .findAll("#game-settings-commentaries-access option")
      .map((o) => o.text());
    expect(options).toEqual([
      "Доступно всем без ограничений",
      "Доступно посторонним только для чтения",
    ]);
  });
});

// The server gives the description as typed to the master alone: a lead or a
// moderator would be given it drawn, and a save of the drawing would write the
// markup over.
describe("GameInfoSection description", () => {
  const TYPED = "[b]Песочница[/b] без рельсов\n[spoiler]и с картой[/spoiler]";
  let update: ReturnType<typeof vi.fn>;
  let read: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    update = stubTheApi();
    read = vi.fn().mockResolvedValue({
      data: { resource: { publicId: "the-game", info: TYPED } },
      error: null,
    });
    vi.spyOn(gameApi, "getGameForEdit").mockImplementation(
      read as unknown as typeof gameApi.getGameForEdit,
    );
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  const editor = (wrapper: VueWrapper) =>
    wrapper.findComponent({ name: "BBCodeEditor" });

  it("opens in front of the master on the text as typed, before the tags", async () => {
    const wrapper = mountSection([3], "master-id");
    await flushPromises();

    expect(read).toHaveBeenCalledWith("the-game");
    expect(editor(wrapper).props("modelValue")).toBe(TYPED);
    const labels = wrapper
      .findAll(".form-field-label label")
      .map((l) => l.text());
    expect(labels.slice(-2)).toEqual(["Описание:", "Теги:"]);
    expect(wrapper.text()).toContain("Не менее 100 символов");
  });

  it("is not offered to a lead of the game, and not read for him", async () => {
    const wrapper = mountSection([3], "assistant-id");
    await flushPromises();

    expect(read).not.toHaveBeenCalled();
    expect(editor(wrapper).exists()).toBe(false);
  });

  it("is not sent untouched", async () => {
    const wrapper = mountSection([3], "master-id");
    await flushPromises();

    await save(wrapper);

    expect(update.mock.calls[0][1].info).toBeUndefined();
  });

  it("is sent as typed once changed", async () => {
    const wrapper = mountSection([3], "master-id");
    await flushPromises();

    editor(wrapper).vm.$emit("update:modelValue", "[i]Новое[/i] описание");
    await flushPromises();
    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
    await save(wrapper);

    expect(update.mock.calls.at(-1)?.[1].info).toBe("[i]Новое[/i] описание");
  });

  it("sends an erased description as an empty string", async () => {
    const wrapper = mountSection([3], "master-id");
    await flushPromises();

    editor(wrapper).vm.$emit("update:modelValue", "");
    await flushPromises();
    await save(wrapper);

    expect(update.mock.calls.at(-1)?.[1].info).toBe("");
  });

  it("says the server's refusal of the description under the field, never Сохранено", async () => {
    const wrapper = mountSection([3], "master-id");
    await flushPromises();
    update.mockResolvedValue({
      data: null,
      error: problem(400, "", {
        errors: { Info: ["Описание не короче 100 символов"] },
      } as never),
    });

    editor(wrapper).vm.$emit("update:modelValue", "");
    await flushPromises();
    await save(wrapper);

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
    await expectRefusalUnderField(
      wrapper,
      "Описание:",
      "Описание не короче 100 символов",
      () => {
        editor(wrapper).vm.$emit("update:modelValue", "и снова");
        return flushPromises();
      },
    );
  });

  it("sends the title, the system and the setting as typed, spaces included", async () => {
    const wrapper = mountSection([3], "master-id");
    await flushPromises();
    await wrapper.find("#game-title").setValue(" Игра ");
    await wrapper.find("#game-system").setValue("GURPS ");
    await wrapper.find("#game-setting").setValue(" Свой мир");

    await save(wrapper);

    expect(update.mock.calls.at(-1)?.[1]).toMatchObject({
      title: " Игра ",
      system: "GURPS ",
      setting: " Свой мир",
    });
  });
});

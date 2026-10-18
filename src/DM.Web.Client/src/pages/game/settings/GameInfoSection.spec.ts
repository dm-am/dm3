/**
 * @vitest-environment jsdom
 */

/**
 * Tags on the settings page.
 *
 * They used to be a creation-time decision and nothing else: the creation form
 * offered the selector, the settings page did not, and a game labelled wrongly
 * on day one stayed labelled wrongly. This spec pins what makes them editable
 * rather than merely visible — the control starts from the tags the game
 * already carries, a save sends the whole set, and a set emptied by hand
 * travels as an empty list instead of silently vanishing from the patch, which
 * is the only way the last tag ever comes off.
 *
 * The selector itself is mounted for real (only the catalog behind it is
 * stubbed): a stub would have let the page hand it a model it never reads.
 *
 * The second half of the file is about how the form reports a save at all. It
 * cannot report one by changing - the fields afterwards hold the very values
 * the reader typed - so the answer belongs to the submit button: "Сохранено"
 * and inert while the form still makes the patch that was sent, "Сохранить"
 * again the moment anything moves.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { createRouter, createMemoryHistory } from "vue-router";
import { gameApi, useGameDetailsStore, type Game } from "@/entities/game";
import { useToast } from "@/shared/lib/composables/useToast";
import type { GeneralError } from "@/shared/api/models/common";
import GameInfoSection from "./GameInfoSection.vue";

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

/** The catalog GET /games/tags serves, one group, three tags. */
const CATALOG = [
  { id: 3, title: "Фэнтези", groupTitle: "Жанр", sortOrder: 1 },
  { id: 7, title: "Медленный", groupTitle: "Жанр", sortOrder: 2 },
  { id: 9, title: "Хоррор", groupTitle: "Жанр", sortOrder: 3 },
];

/** A game the viewer is about to re-tag. */
function mountSection(tagIds: number[]) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useGameDetailsStore();
  store.game = {
    id: "the-game-guid",
    publicId: "the-game",
    title: "Игра",
    system: "GURPS",
    setting: "Свой мир",
    tagIds,
    recruitment: { isOpen: true },
    privacySettings: { viewPrivates: false, viewDice: true },
  } as unknown as Game;
  // The reload after a successful save is not this spec's subject, and left
  // real it would reach for the endpoint.
  vi.spyOn(store, "loadGame").mockResolvedValue({ ok: true });

  // The link to the game is resolved by the router's own table, so the
  // section is mounted with the route it names.
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: "/", component: { render: () => null } },
      { name: "game", path: "/game/:id", component: { render: () => null } },
    ],
  });

  return mount(GameInfoSection, { global: { plugins: [pinia, router] } });
}

/** The tag chips on screen, and which of them are lit. */
const chips = (wrapper: ReturnType<typeof mountSection>) =>
  wrapper.findAll(".tag-button").map((b) => ({
    title: b.text(),
    selected: b.classes().includes("selected"),
  }));

/** The chip carrying a given title. */
const chip = (wrapper: ReturnType<typeof mountSection>, title: string) =>
  wrapper.findAll(".tag-button").find((b) => b.text() === title)!;

/** The tags of the single patch the section sent. */
const sentTags = (update: ReturnType<typeof vi.fn>) =>
  update.mock.calls.at(-1)?.[1]?.tags;

describe("GameInfoSection tags", () => {
  let update: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.spyOn(gameApi, "getTags").mockResolvedValue({
      data: { resources: CATALOG },
      error: null,
    } as unknown as Awaited<ReturnType<typeof gameApi.getTags>>);
    update = vi.fn().mockResolvedValue({ data: null, error: null });
    vi.spyOn(gameApi, "updateGame").mockImplementation(
      update as unknown as typeof gameApi.updateGame,
    );
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
    // The per-group limits are measured on what is submitted. A game imported
    // over its limits would otherwise refuse every settings save - including
    // one that only fixes a typo - until its tags were sorted out first.
    const wrapper = mountSection([3, 7]);
    await flushPromises();

    await wrapper.find("form").trigger("submit");
    await flushPromises();

    expect(sentTags(update)).toBeUndefined();
    // The save itself still happens: it is the tag field that stays behind.
    expect(update.mock.calls.at(-1)?.[1]?.title).toBeDefined();
  });
});

/** The submit button of the form, the one control that answers for the save. */
const submitButton = (wrapper: VueWrapper) => wrapper.find(".controls button");

/** What the button says, and whether it can still be pressed. */
const answer = (wrapper: VueWrapper) => ({
  label: submitButton(wrapper).text(),
  inert: submitButton(wrapper).attributes("disabled") !== undefined,
});

/** Press the submit and let the request, the reload and the tick settle. */
async function save(wrapper: VueWrapper) {
  await wrapper.find("form").trigger("submit");
  await flushPromises();
}

/**
 * How the section reports a save.
 *
 * What each test here catches, since a button that always reads "Сохранить"
 * looks perfectly normal on screen:
 *  - remember() left out of save(), or called before the request: the button
 *    never changes, or changes over a save that never happened;
 *  - a comparison written over the text fields alone: the switches and the
 *    tags stop taking the button back to "Сохранить";
 *  - remember() above the error return: a refused save reads as a good one;
 *  - remember() before the reload has re-seeded the form: the game answers,
 *    the fields are written again behind the snapshot, and an untouched form
 *    offers a save the reader did not ask for;
 *  - the toast put back beside the button: one event announced twice.
 */
describe("GameInfoSection save confirmation", () => {
  let update: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.spyOn(gameApi, "getTags").mockResolvedValue({
      data: { resources: CATALOG },
      error: null,
    } as unknown as Awaited<ReturnType<typeof gameApi.getTags>>);
    update = vi.fn().mockResolvedValue({ data: null, error: null });
    vi.spyOn(gameApi, "updateGame").mockImplementation(
      update as unknown as typeof gameApi.updateGame,
    );
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

  // The fields the button forgets when the comparison is written over the
  // three text inputs: they travel in the same patch and have to count.
  it("offers the save again when the recruitment switch is flipped", async () => {
    const wrapper = mountSection([3]);
    await flushPromises();
    await save(wrapper);

    // The game mounted here is recruiting, so the flip is the switch going off.
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

  // The reload is the reason the form is remembered a tick later than the
  // answer arrives. It writes every field again from the game the server has
  // just served, and the tags it sent stop travelling in the patch the moment
  // the game carries them: a snapshot taken before that lands on a patch that
  // no longer exists, and the button offers a save nobody asked for.
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
    // A 400: the statuses the interceptor speaks for are the ones the section
    // stays quiet about, and this is not one of them.
    update.mockResolvedValue({
      data: null,
      error: { status: 400, title: "", type: "", traceId: "t" } as GeneralError,
    });

    await save(wrapper);

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
    // Where the press was, and in no plate (the owner's rule of 26 September
    // 2026, UI_STANDARDS "Всплывающие сообщения").
    expect(wrapper.get(".action-result").text()).toBe(
      "Не удалось сохранить информацию игры",
    );
    expect(saidOutLoud()).toEqual([]);
  });
});

/**
 * The form is DM2's ModuleSettings: one form of captioned rows in DM2's order
 * and words, the link to the game a plain link with the address written out,
 * and the discussion offered in the two modes it can be in, both open to read.
 *
 * What these catch: a caption moved out of DM2's order or renamed back into
 * our older words ("Лимит игроков", "Доступ к комментариям"); the link drawn
 * as a button that copies, or as text that is not a link; a third mode of the
 * discussion offered again, the one that closed reading to outsiders.
 */
describe("GameInfoSection as DM2's settings", () => {
  beforeEach(() => {
    vi.spyOn(gameApi, "getTags").mockResolvedValue({
      data: { resources: CATALOG },
      error: null,
    } as unknown as Awaited<ReturnType<typeof gameApi.getTags>>);
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
      "Показывать всем приватные сообщения",
    ]);
  });

  it("gives the address of the game as a plain link, with nothing to copy it", async () => {
    const wrapper = mountSection([3]);
    await flushPromises();

    const link = wrapper.get(".game-link a");
    expect(link.attributes("href")).toBe("/game/the-game");
    expect(link.text()).toBe(`${window.location.origin}/game/the-game`);
    expect(wrapper.find(".game-link button").exists()).toBe(false);
    expect(wrapper.text()).not.toMatch(/копир/i);
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

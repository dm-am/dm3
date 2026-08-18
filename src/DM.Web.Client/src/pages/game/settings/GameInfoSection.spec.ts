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
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { gameApi, useGameDetailsStore, type Game } from "@/entities/game";
import GameInfoSection from "./GameInfoSection.vue";

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

  return mount(GameInfoSection, { global: { plugins: [pinia] } });
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

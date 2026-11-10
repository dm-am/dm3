/**
 * @vitest-environment jsdom
 */

// A created character answers with the roster, read again before the page
// leaves for it, and with no plate.
import { beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h, watchEffect } from "vue";
import { flushPromises } from "@vue/test-utils";
import { createPinia } from "pinia";
import type { Router } from "vue-router";
import {
  GameStatus,
  useGameDetailsStore,
  type Character,
  type GameParticipation,
} from "@/entities/game";
import { createMockGame } from "@/entities/game/model/store.fixtures";
import { asServed } from "@/shared/api/models";
import { useToast } from "@/shared/lib/composables/useToast";
import { provideZoneSection } from "@/shared/lib/composables/useZoneSection";
import { mountPage } from "@/shared/lib/testing/mountPage";
import CharacterCreate from "./CharacterCreate.vue";

const FORM = "/game/the-game/characters/create";
const ROSTER = "/game/the-game/characters";
const routes: [string, string][] = [
  ["game-character-create", "/game/:id/characters/create"],
  ["game-characters", "/game/:id/characters"],
];

const address = (router: Router) => router.currentRoute.value.fullPath;

// The form owns its submit (CharacterForm.saving.spec); the page sees "saved".
const CharacterFormStub = defineComponent({
  inheritAttrs: false,
  emits: ["saved", "cancel"],
  setup:
    (_, { emit }) =>
    () =>
      h("button", {
        class: "save",
        onClick: () => emit("saved", { id: "char-new", name: "Ронин" }),
      }),
});

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

/** The fields of a roster entry these tests set; ids are branded on the wire. */
type CharacterOverrides = Partial<{
  id: string;
  name: string;
  status: string;
  isNpc: boolean;
}>;

const character = (over: CharacterOverrides = {}): Character =>
  ({
    id: "char-old",
    name: "Старый",
    status: "Active",
    isNpc: false,
    author: { id: "player-1", username: "gamer", role: "Player" },
    attributes: [],
    ...over,
  }) as unknown as Character;

const CREATED = character({ id: "char-new", name: "Ронин" });

async function render({
  npc = false,
  participation = [] as GameParticipation[],
  blacklisted = false,
  recruiting = true,
} = {}) {
  const pinia = createPinia();
  const store = useGameDetailsStore(pinia);
  const game = createMockGame("the-game-guid", "Игра");
  store.game = {
    ...game,
    publicId: asServed("the-game"),
    status: GameStatus.Active,
    participation: asServed(participation),
    recruitment: asServed({ ...game.recruitment, isOpen: recruiting }),
    schema: { specifications: [], version: 0 },
    viewerBlacklisted: asServed(blacklisted),
  };
  store.characters = [character()];
  const loadCharacters = vi
    .spyOn(store, "loadCharacters")
    .mockResolvedValue(undefined);

  // Provided the way the zone shell provides it, which renders the heading.
  const announced = { value: undefined as string | undefined };
  const host = defineComponent({
    setup() {
      const section = provideZoneSection();
      watchEffect(() => {
        announced.value = section.value;
      });
      return () => h(CharacterCreate);
    },
  });

  const { wrapper, router } = await mountPage(
    host,
    npc ? `${FORM}?npc=1` : FORM,
    { routes, pinia, stubs: { CharacterForm: CharacterFormStub } },
  );

  return { wrapper, store, loadCharacters, announced, router };
}

describe("CharacterCreate", () => {
  beforeEach(() => {
    for (const shown of [...toast.toasts.value]) toast.dismiss(shown.id);
    vi.restoreAllMocks();
  });

  it.each([
    ["an application", {}],
    ["a new NPC", { npc: true, participation: ["Owner" as const] }],
  ])("leaves %s for a roster read again, silently", async (_, options) => {
    const { wrapper, loadCharacters, router } = await render(options);

    await wrapper.get("button.save").trigger("click");
    await flushPromises();

    expect(loadCharacters).toHaveBeenCalledWith("the-game");
    expect(address(router)).toBe(ROSTER);
    expect(saidOutLoud()).toEqual([]);
  });

  it("does not leave until the roster has answered", async () => {
    const { wrapper, store, router } = await render();
    let answer!: () => void;
    vi.spyOn(store, "loadCharacters").mockImplementation(
      () =>
        new Promise<void>((resolve) => {
          answer = () => {
            store.characters = [CREATED, character()];
            resolve();
          };
        }),
    );

    await wrapper.get("button.save").trigger("click");
    await flushPromises();

    expect(address(router)).toBe(FORM);

    answer();
    await flushPromises();

    expect(address(router)).toBe(ROSTER);
  });

  it("leads the viewer on the blacklist back to the roster", async () => {
    const { wrapper, router } = await render({ blacklisted: true });

    expect(wrapper.find("button.save").exists()).toBe(false);
    expect(wrapper.text()).toBe("");
    await flushPromises();
    expect(address(router)).toBe(ROSTER);
  });

  it("leads a viewer who does not lead the game away from the NPC form", async () => {
    const { wrapper, router } = await render({
      npc: true,
      participation: ["Player"],
    });

    expect(wrapper.find("button.save").exists()).toBe(false);
    expect(wrapper.text()).toBe("");
    await flushPromises();
    expect(address(router)).toBe(ROSTER);
  });

  // An invited player applies past a closed recruitment, so the form stays.
  it("says over the form who applies while the recruitment is closed", async () => {
    const { wrapper, router } = await render({ recruiting: false });

    expect(wrapper.find("button.save").exists()).toBe(true);
    expect(wrapper.get(".intro").text()).toBe(
      "Набор игроков закрыт: заявку подают по приглашению мастера",
    );
    await flushPromises();
    expect(address(router)).toBe(FORM);
  });

  it.each([
    ["an application", {}, "Новый персонаж"],
    [
      "a non-player character",
      { npc: true, participation: ["Owner" as const] },
      "Новый неигровой персонаж",
    ],
  ])("names the form of %s in the heading", async (_, options, section) => {
    const { announced } = await render(options);

    expect(announced.value).toBe(section);
  });

  it("offers the form to a reader who is not on the blacklist", async () => {
    const { wrapper, router } = await render();

    expect(wrapper.find("button.save").exists()).toBe(true);
    await flushPromises();
    expect(address(router)).toBe(FORM);
  });
});

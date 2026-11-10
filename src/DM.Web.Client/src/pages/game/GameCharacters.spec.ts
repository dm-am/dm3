/**
 * @vitest-environment jsdom
 */

// The roster links every character to the actions the backend lets the
// viewer take on it: the master and assistants act, the game mentor reads.
import { describe, expect, it, vi } from "vitest";
import { createPinia } from "pinia";
import {
  useGameDetailsStore,
  type Character,
  type GameParticipation,
} from "@/entities/game";
import { createMockGame } from "@/entities/game/model/store.fixtures";
import { asServed } from "@/shared/api/models";
import { mountPage } from "@/shared/lib/testing/mountPage";
import GameCharacters from "./GameCharacters.vue";

const routes: [string, string][] = [
  ["game-characters", "/game/:id/characters"],
  ["game-character", "/game/:id/characters/:characterId"],
  ["game-character-edit", "/game/:id/characters/:characterId/edit"],
  ["game-character-create", "/game/:id/characters/create"],
  ["profile", "/users/:username"],
];

/** The fields of a roster entry this page reads; ids are branded on the wire. */
type CharacterOverrides = Partial<{
  id: string;
  name: string;
  status: Character["status"];
  isDead: boolean;
  isPlayerLeft: boolean;
  isNpc: boolean;
}>;

function character(over: CharacterOverrides = {}): Character {
  return {
    id: "char-1",
    name: "Ронин",
    status: "UnderReview",
    isDead: false,
    isPlayerLeft: false,
    isPlayerExiled: false,
    isNpc: false,
    author: { username: "gamer", role: "Player" },
    attributes: [],
    totalPostsCount: 0,
    ...over,
  } as unknown as Character;
}

/** The read of the roster the page asks for, spied in every render. */
let loadCharacters: ReturnType<typeof vi.spyOn>;

async function render(
  participation: GameParticipation[],
  characters: Character[],
  state: { charactersLoading?: boolean; charactersError?: string } = {},
) {
  const pinia = createPinia();
  const store = useGameDetailsStore(pinia);
  store.game = {
    ...createMockGame("the-game-guid", "Игра"),
    publicId: asServed("the-game"),
    participation: asServed(participation),
  };
  // Seeded, so mounting the page does not reach for the endpoint.
  store.characters = characters;
  store.charactersLoading = state.charactersLoading ?? false;
  store.charactersError = state.charactersError ?? null;
  // An empty roster makes the page ask for one.
  loadCharacters = vi
    .spyOn(store, "loadCharacters")
    .mockResolvedValue(undefined);

  const { wrapper } = await mountPage(
    GameCharacters,
    "/game/the-game/characters",
    { routes, pinia },
  );
  return wrapper;
}

/** Every link the roster draws into a character's own page. */
function manageLinks(wrapper: Awaited<ReturnType<typeof render>>) {
  return wrapper
    .findAll("a")
    .filter((link) => link.attributes("href")?.endsWith("/edit"));
}

describe("GameCharacters", () => {
  it("lets the master answer an application in a game with no NPC", async () => {
    const wrapper = await render(["Owner", "Authority"], [character()]);

    const links = manageLinks(wrapper);
    expect(links).toHaveLength(1);
    expect(links[0].text()).toBe("Рассмотреть заявку");
    expect(links[0].attributes("href")).toBe(
      "/game/the-game/characters/char-1/edit",
    );
  });

  it("links a character in the game to the actions the master has on it", async () => {
    const wrapper = await render(
      ["Owner", "Authority"],
      [character({ status: "Active" })],
    );

    const links = manageLinks(wrapper);
    expect(links).toHaveLength(1);
    expect(links[0].text()).toBe("Управление персонажем");
  });

  it("draws no link to a page that would offer the master nothing", async () => {
    // Staff have no transition for a character that left.
    const wrapper = await render(
      ["Owner", "Authority"],
      [character({ status: "Retired", isPlayerLeft: true })],
    );

    expect(manageLinks(wrapper)).toHaveLength(0);
  });

  it("names the two lists of the roster and a character with no player", async () => {
    const wrapper = await render(
      [],
      [
        character({ id: "char-1", status: "Active" }),
        {
          ...character({ id: "char-2", status: "Active", isNpc: true }),
          author: undefined,
        },
      ],
    );

    expect(wrapper.findAll(".section-title").map((h) => h.text())).toEqual([
      "Персонажи игроков",
      "Неигровые персонажи",
    ]);
    expect(wrapper.get(".character-card[data-id='char-2']").text()).toContain(
      "Неигровой персонаж",
    );
  });

  it("shows an application to a guest, with no way to decide it", async () => {
    const wrapper = await render([], [character()]);

    expect(wrapper.find(".section-title").text()).toBe("На рассмотрении");
    expect(wrapper.text()).toContain("Ронин");
    expect(manageLinks(wrapper)).toHaveLength(0);
  });

  it("shows the mentor the application and no way to decide it", async () => {
    // GameParticipation.Moderator is the game mentor: canManage in the store,
    // refused by CharacterIntentionResolver.
    const wrapper = await render(["Moderator"], [character()]);

    expect(wrapper.text()).toContain("На рассмотрении");
    expect(manageLinks(wrapper)).toHaveLength(0);
  });
});

// A roster still loading or failed is not an empty one.
describe("GameCharacters load states", () => {
  const EMPTY = "В этой игре пока нет персонажей";

  it("says nothing about the roster while it is being fetched", async () => {
    const wrapper = await render([], [], { charactersLoading: true });

    expect(loadCharacters).toHaveBeenCalledWith("the-game");
    expect(wrapper.text()).not.toContain(EMPTY);
    expect(wrapper.find(".character-skeleton-grid").exists()).toBe(true);
  });

  it("names a failed load and offers to repeat it", async () => {
    const wrapper = await render([], [], {
      charactersError: "Не удалось загрузить персонажей",
    });

    expect(wrapper.text()).not.toContain(EMPTY);
    expect(wrapper.text()).toContain("Не удалось загрузить персонажей");
    expect(wrapper.text()).toContain("Повторить");
  });
});

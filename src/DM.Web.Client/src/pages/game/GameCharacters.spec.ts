/**
 * @vitest-environment jsdom
 */

// The roster links every character to the actions the backend lets the
// viewer take on it: the master and assistants act, the game mentor reads.
import { describe, expect, it, vi } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  useGameDetailsStore,
  type Character,
  type Game,
} from "@/entities/game";
import GameCharacters from "./GameCharacters.vue";

// The rest of the router stays real for the components that import it.
vi.mock("vue-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-router")>()),
  useRoute: () => ({ params: { id: "the-game" }, query: {} }),
  useRouter: () => ({ replace: vi.fn() }),
}));

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

function render(
  participation: string[],
  characters: Character[],
  state: { charactersLoading?: boolean; charactersError?: string } = {},
) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useGameDetailsStore();
  store.game = {
    id: "the-game-guid",
    publicId: "the-game",
    participation,
  } as unknown as Game;
  // Seeded, so mounting the page does not reach for the endpoint.
  store.characters = characters;
  store.charactersLoading = state.charactersLoading ?? false;
  store.charactersError = state.charactersError ?? null;
  // An empty roster makes the page ask for one.
  vi.spyOn(store, "loadCharacters").mockResolvedValue(undefined);

  return mount(GameCharacters, {
    global: {
      plugins: [pinia],
      stubs: { RouterLink: RouterLinkStub },
    },
  });
}

/** Every link the roster draws into a character's own page. */
function manageLinks(wrapper: ReturnType<typeof render>) {
  return wrapper.findAllComponents(RouterLinkStub).filter((link) => {
    const to = link.props("to") as unknown as { name?: string };
    return to?.name === "game-character-edit";
  });
}

describe("GameCharacters", () => {
  it("lets the master answer an application in a game with no NPC", () => {
    const wrapper = render(["Owner", "Authority"], [character()]);

    const links = manageLinks(wrapper);
    expect(links).toHaveLength(1);
    expect(links[0].text()).toBe("Рассмотреть заявку");
    expect(links[0].props("to")).toEqual({
      name: "game-character-edit",
      params: { id: "the-game", characterId: "char-1" },
    });
  });

  it("links a character in the game to the actions the master has on it", () => {
    const wrapper = render(
      ["Owner", "Authority"],
      [character({ status: "Active" })],
    );

    const links = manageLinks(wrapper);
    expect(links).toHaveLength(1);
    expect(links[0].text()).toBe("Управление персонажем");
  });

  it("draws no link to a page that would offer the master nothing", () => {
    // Staff have no transition for a character that left.
    const wrapper = render(
      ["Owner", "Authority"],
      [character({ status: "Retired", isPlayerLeft: true })],
    );

    expect(manageLinks(wrapper)).toHaveLength(0);
  });

  it("names the two lists of the roster and a character with no player", () => {
    const wrapper = render(
      [],
      [
        character({ id: "char-1", status: "Active" }),
        {
          ...character({ id: "char-2", status: "Active", isNpc: true }),
          author: undefined,
        } as unknown as Character,
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

  it("shows an application to a guest, with no way to decide it", () => {
    const wrapper = render([], [character()]);

    expect(wrapper.find(".section-title").text()).toBe("На рассмотрении");
    expect(wrapper.text()).toContain("Ронин");
    expect(manageLinks(wrapper)).toHaveLength(0);
  });

  it("shows the mentor the application and no way to decide it", () => {
    // GameParticipation.Moderator is the game mentor: canManage in the store,
    // refused by CharacterIntentionResolver.
    const wrapper = render(["Moderator"], [character()]);

    expect(wrapper.text()).toContain("На рассмотрении");
    expect(manageLinks(wrapper)).toHaveLength(0);
  });
});

// A roster still loading or failed is not an empty one.
describe("GameCharacters load states", () => {
  const EMPTY = "В этой игре пока нет персонажей";

  it("says nothing about the roster while it is being fetched", () => {
    const wrapper = render([], [], { charactersLoading: true });

    expect(wrapper.text()).not.toContain(EMPTY);
    expect(wrapper.find(".character-skeleton-grid").exists()).toBe(true);
  });

  it("names a failed load and offers to repeat it", () => {
    const wrapper = render([], [], {
      charactersError: "Не удалось загрузить персонажей",
    });

    expect(wrapper.text()).not.toContain(EMPTY);
    expect(wrapper.text()).toContain("Не удалось загрузить персонажей");
    expect(wrapper.text()).toContain("Повторить");
  });
});

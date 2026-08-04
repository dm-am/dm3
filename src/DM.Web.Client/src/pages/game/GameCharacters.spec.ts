/**
 * @vitest-environment jsdom
 */

/**
 * The roster is where a master meets an application: the "Новый персонаж"
 * notification points at this page. Accept and decline live on the character's
 * own page, and every link into it used to depend on something the master of a
 * young game does not have — an NPC (the sidebar item) or a character of their
 * own (the join actions, which the master is never shown). The application was
 * readable here and answerable nowhere, and the same dead end held the rest of
 * the lifecycle: kill, exile, resurrect.
 *
 * So the roster carries the link, and it is drawn by the rule the backend
 * enforces rather than by the game's canManage: the master and assistants act
 * on characters, the game mentor reads them.
 */
import { describe, expect, it, vi } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  useGameDetailsStore,
  type Character,
  type Game,
} from "@/entities/game";
import GameCharacters from "./GameCharacters.vue";

// Only the route param is needed; the rest of the router stays real so the
// components that import it from the same module keep working.
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
  // An empty roster makes the page ask for one; the endpoint is not the subject.
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
    // The same dead end, one status further: without an NPC the master could
    // not reach "Отметить погибшим" or "Изгнать из игры" either.
    const wrapper = render(
      ["Owner", "Authority"],
      [character({ status: "Active" })],
    );

    const links = manageLinks(wrapper);
    expect(links).toHaveLength(1);
    expect(links[0].text()).toBe("Управление персонажем");
  });

  it("draws no link to a page that would offer the master nothing", () => {
    // Retired by leaving: staff have no transition for it and the sheet is the
    // owner's, so a link would only move the dead end one screen further.
    const wrapper = render(
      ["Owner", "Authority"],
      [character({ status: "Retired", isPlayerLeft: true })],
    );

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

/**
 * The same class of lie the messenger told about conversations: a roster still
 * on the wire, and a roster that failed to arrive, were both rendered as "нет
 * персонажей" — a statement about the game, made without knowing anything about
 * it. The store carried the loading flag and the failure the whole time.
 */
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

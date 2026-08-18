/**
 * @vitest-environment jsdom
 */

/**
 * A character's name pointed at the roster of the whole game, carrying a
 * `scrollTo` query that no roster has ever read — so the link that promised one
 * character delivered the cast, scrolled nowhere. The sheet was served per
 * character all along (GET /v1/characters/{id}) and was fetched only by the
 * edit form.
 *
 * What this holds: the page asks for the character named in the address, draws
 * the sheet through the shared view mode rather than a second layout of its
 * own, and answers a failed load with something the reader can repeat.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { defineComponent, h, watchEffect } from "vue";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  gameApi,
  GameParticipation,
  type Character,
  type Game,
} from "@/entities/game";
import { useGameDetailsStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import GameCharacter from "./GameCharacter.vue";
import { provideZoneSection } from "@/shared/lib/composables/useZoneSection";

vi.mock("vue-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-router")>()),
  useRoute: () => ({
    params: { id: "the-game", characterId: "char-1" },
    query: {},
  }),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

const SHEET: Character = {
  id: "char-1",
  name: "Ронин",
  status: "Retired",
  isDead: true,
  isPlayerLeft: false,
  isPlayerExiled: false,
  isNpc: false,
  author: { id: "player-1", username: "gamer", role: "Player" },
  totalPostsCount: 17,
  attributes: [{ id: "spec-1", title: "Раса", value: "Полурослик" }],
} as unknown as Character;

/**
 * @param participation how the viewer takes part in the game
 * @param viewerId who is looking; null for a visitor with no account
 */
function render({
  participation = [] as GameParticipation[],
  viewerId = null as string | null,
} = {}) {
  const pinia = createPinia();
  setActivePinia(pinia);
  useAuthStore().user = viewerId ? ({ id: viewerId } as never) : null;
  const store = useGameDetailsStore();
  store.game = {
    id: "the-game-guid",
    publicId: "the-game",
    title: "Хроники Амбера",
    participation,
    schema: {
      id: "schema-1",
      title: "Лист",
      type: 0,
      specifications: [
        {
          id: "spec-1",
          title: "Раса",
          type: 0,
          order: 1,
          isRequired: false,
          isHidden: false,
          isDescriptor: false,
        },
      ],
    },
  } as unknown as Game;

  // The zone shell is what renders the heading, so the section this page
  // announces is what the test can see of it. Provided here the way the shell
  // provides it, and read back through `announced`.
  const announced = { value: undefined as string | undefined };
  const host = defineComponent({
    setup() {
      const section = provideZoneSection();
      watchEffect(() => {
        announced.value = section.value;
      });
      return () => h(GameCharacter);
    },
  });

  const wrapper = mount(host, {
    global: {
      plugins: [pinia],
      stubs: { RouterLink: RouterLinkStub },
    },
  });
  return { wrapper, announced };
}

describe("GameCharacter", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("reads the character named in the address and draws its sheet", async () => {
    const get = vi
      .spyOn(gameApi, "getCharacter")
      .mockResolvedValue({ data: { resource: SHEET } } as never);

    const { wrapper, announced } = render();
    await flushPromises();

    expect(get).toHaveBeenCalledWith("char-1");
    // The name reaches the reader through the heading of the zone, which reads
    // "{game} | {character}" — the page announces the second half.
    expect(announced.value).toBe("Ронин");
    // The lifecycle caption, in the wording the entity owns.
    expect(wrapper.text()).toContain("Персонаж мертв");
    expect(wrapper.text()).toContain("Постов: 17");
    // The sheet itself comes from the shared view mode, in schema order.
    expect(wrapper.text()).toContain("Раса");
    expect(wrapper.text()).toContain("Полурослик");
  });

  it("names a failed load and offers to repeat it", async () => {
    vi.spyOn(gameApi, "getCharacter").mockResolvedValue({
      error: { status: 500 },
    } as never);

    const { wrapper } = render();
    await flushPromises();

    expect(wrapper.text()).toContain("Не удалось загрузить персонажа");
    expect(wrapper.text()).toContain("Повторить");
  });

  it("leads back to the roster of the game", async () => {
    vi.spyOn(gameApi, "getCharacter").mockResolvedValue({
      data: { resource: SHEET },
    } as never);

    const { wrapper } = render();
    await flushPromises();

    const back = wrapper.findAllComponents(RouterLinkStub).filter((link) => {
      const to = link.props("to") as unknown as { name?: string };
      return to?.name === "game-characters";
    });
    expect(back).toHaveLength(1);
    expect(back[0].props("to")).toEqual({
      name: "game-characters",
      params: { id: "the-game" },
    });
  });

  /**
   * The two notepads under the sheet. They are the same widget with opposite
   * audiences, so the only thing worth pinning is who gets to see which — and
   * the section has to be absent rather than refused: a heading reading "the
   * notes the master keeps about you", greyed out, tells the player what it
   * was meant to hide.
   *
   * Every case asserts both ways round. A gate that shows the right section is
   * half a gate if it shows the other one too, and both mistakes are one
   * mistyped `||` away from each other.
   */
  describe("notepads", () => {
    const PLAYER_NOTES = "Заметки игрока";
    const MASTER_NOTES = "Заметки мастера";

    async function sections(options?: Parameters<typeof render>[0]) {
      vi.spyOn(gameApi, "getCharacter").mockResolvedValue({
        data: { resource: SHEET },
      } as never);
      vi.spyOn(gameApi, "getCharacterNotepad").mockResolvedValue({
        data: { resources: [] },
      } as never);
      vi.spyOn(gameApi, "getCharacterMasterNotepad").mockResolvedValue({
        data: { resources: [] },
      } as never);

      const { wrapper } = render(options);
      await flushPromises();
      return wrapper.text();
    }

    it("shows the player their own notes and not the ones kept about them", async () => {
      const text = await sections({
        participation: [GameParticipation.Player],
        viewerId: "player-1",
      });

      expect(text).toContain(PLAYER_NOTES);
      expect(text).not.toContain(MASTER_NOTES);
    });

    it("shows the master the notes kept about the character and not the player's", async () => {
      const text = await sections({
        participation: [GameParticipation.Owner],
        viewerId: "master-1",
      });

      // The one notepad of a game its master cannot open.
      expect(text).toContain(MASTER_NOTES);
      expect(text).not.toContain(PLAYER_NOTES);
    });

    it("gives an assistant the same as the master", async () => {
      const text = await sections({
        participation: [GameParticipation.Authority],
        viewerId: "assistant-1",
      });

      expect(text).toContain(MASTER_NOTES);
      expect(text).not.toContain(PLAYER_NOTES);
    });

    it("gives the curating mentor neither", async () => {
      // HasEditAccess is master and assistant; curating a game is not leading
      // it, and the API refuses a mentor both notepads.
      const text = await sections({
        participation: [GameParticipation.Moderator],
        viewerId: "mentor-1",
      });

      expect(text).not.toContain(MASTER_NOTES);
      expect(text).not.toContain(PLAYER_NOTES);
    });

    it("gives another player in the game neither", async () => {
      const text = await sections({
        participation: [GameParticipation.Player],
        viewerId: "player-2",
      });

      expect(text).not.toContain(PLAYER_NOTES);
      expect(text).not.toContain(MASTER_NOTES);
    });

    it("gives a visitor without an account neither", async () => {
      const text = await sections();

      expect(text).not.toContain(PLAYER_NOTES);
      expect(text).not.toContain(MASTER_NOTES);
    });

    it("asks for neither notepad it does not show", async () => {
      vi.spyOn(gameApi, "getCharacter").mockResolvedValue({
        data: { resource: SHEET },
      } as never);
      const playerNotes = vi
        .spyOn(gameApi, "getCharacterNotepad")
        .mockResolvedValue({ data: { resources: [] } } as never);
      const masterNotes = vi
        .spyOn(gameApi, "getCharacterMasterNotepad")
        .mockResolvedValue({ data: { resources: [] } } as never);

      render({
        participation: [GameParticipation.Player],
        viewerId: "player-2",
      });
      await flushPromises();

      // A hidden section that still fetches would earn a pair of 403s per
      // visit and put the answer into the network tab regardless.
      expect(playerNotes).not.toHaveBeenCalled();
      expect(masterNotes).not.toHaveBeenCalled();
    });
  });
});

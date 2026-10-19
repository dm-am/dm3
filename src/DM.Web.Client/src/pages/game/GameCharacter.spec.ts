/**
 * @vitest-environment jsdom
 */

// The sheet of the character named in the address, drawn by the shared view
// mode, and a failed load the reader can repeat.
import { describe, expect, it, vi, beforeEach } from "vitest";
import { defineComponent, h, watchEffect } from "vue";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  characterApi,
  gameNotepadApi,
  GameParticipation,
  useGameDetailsStore,
  type Character,
  type Game,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { provideZoneSection } from "@/shared/lib/composables/useZoneSection";
import { announceWorded } from "@/shared/lib/errors";
import { problem } from "@/shared/lib/testing/problem";
import { deferred, endOfTask } from "@/shared/lib/testing/tasks";
import {
  CharacterCardSkeleton,
  CharacterSheetSkeleton,
} from "@/shared/ui/Skeleton";
import GameCharacter from "./GameCharacter.vue";

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

  // Provided the way the zone shell provides it, which renders the heading.
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
      .spyOn(characterApi, "getCharacter")
      .mockResolvedValue({ data: { resource: SHEET } } as never);

    const { wrapper, announced } = render();
    await flushPromises();

    expect(get).toHaveBeenCalledWith("char-1");
    expect(announced.value).toBe("Ронин");
    expect(wrapper.text()).toContain("Персонаж мертв");
    expect(wrapper.text()).toContain("Постов: 17");
    expect(wrapper.text()).toContain("Раса");
    expect(wrapper.text()).toContain("Полурослик");
  });

  it("names a failed load and offers to repeat it", async () => {
    vi.spyOn(characterApi, "getCharacter").mockResolvedValue({
      error: { status: 500 },
    } as never);

    const { wrapper } = render();
    await flushPromises();

    expect(wrapper.text()).toContain("Не удалось загрузить персонажа");
    expect(wrapper.text()).toContain("Повторить");
  });

  // The page says the server's refusal and takes its plate down; a session
  // that ran out is the plate's to say.
  it.each([
    ["the server's refusal", problem(404, "Персонаж не найден"), 0],
    ["a session that ran out", problem(401), 1],
  ])("answers %s where it belongs", async (_, failure, plates) => {
    const raise = vi.fn();
    announceWorded(failure, raise);
    vi.spyOn(characterApi, "getCharacter").mockResolvedValue({
      error: failure,
    } as never);

    const { wrapper } = render();
    await flushPromises();
    await endOfTask();

    expect(wrapper.text()).toContain(
      failure.title || "Не удалось загрузить персонажа",
    );
    expect(raise).toHaveBeenCalledTimes(plates);
  });

  it("stands the shared skeletons of a card and of a sheet in while it is on the wire", async () => {
    const read = deferred<unknown>();
    vi.spyOn(characterApi, "getCharacter").mockReturnValue(
      read.promise as never,
    );

    const { wrapper } = render();
    await flushPromises();

    expect(wrapper.findComponent(CharacterCardSkeleton).exists()).toBe(true);
    expect(wrapper.findComponent(CharacterSheetSkeleton).exists()).toBe(true);

    read.resolve({ data: { resource: SHEET } });
    await flushPromises();

    expect(wrapper.findComponent(CharacterCardSkeleton).exists()).toBe(false);
    expect(wrapper.findComponent(CharacterSheetSkeleton).exists()).toBe(false);
    expect(wrapper.text()).toContain("Постов: 17");
  });

  it("names a character with no player a non-player one", async () => {
    vi.spyOn(characterApi, "getCharacter").mockResolvedValue({
      data: { resource: { ...SHEET, author: undefined, isNpc: true } },
    } as never);

    const { wrapper } = render();
    await flushPromises();

    expect(wrapper.findAll(".meta-line").map((line) => line.text())).toContain(
      "Неигровой персонаж",
    );
  });

  // The title the API sends with a rate limit carries no number.
  it("names the wait on the card when the read is rate limited", async () => {
    vi.spyOn(characterApi, "getCharacter").mockResolvedValue({
      error: {
        status: 429,
        title: "Слишком много запросов",
        retryAfterSeconds: 30,
      },
    } as never);

    const { wrapper } = render();
    await flushPromises();

    expect(wrapper.text()).toContain(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });

  it("leads back to the roster of the game", async () => {
    vi.spyOn(characterApi, "getCharacter").mockResolvedValue({
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
    expect(back[0].text()).toBe("Назад к персонажам");
  });

  // Every case asserts both ways round: a gate that shows the other notepad
  // too is half a gate.
  describe("notepads", () => {
    const PLAYER_NOTES = "Заметки игрока";
    const MASTER_NOTES = "Заметки мастера";

    /** Both notepads of the character, empty; the lists are the calls the page makes. */
    function emptyNotepads() {
      const list = () => vi.fn().mockResolvedValue({ data: { resources: [] } });
      const playerNotes = list();
      const masterNotes = list();
      vi.spyOn(gameNotepadApi, "ofCharacter").mockReturnValue({
        list: playerNotes,
      } as never);
      vi.spyOn(gameNotepadApi, "ofCharacterMaster").mockReturnValue({
        list: masterNotes,
      } as never);
      return { playerNotes, masterNotes };
    }

    async function sections(options?: Parameters<typeof render>[0]) {
      vi.spyOn(characterApi, "getCharacter").mockResolvedValue({
        data: { resource: SHEET },
      } as never);
      emptyNotepads();

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
      vi.spyOn(characterApi, "getCharacter").mockResolvedValue({
        data: { resource: SHEET },
      } as never);
      const { playerNotes, masterNotes } = emptyNotepads();

      render({
        participation: [GameParticipation.Player],
        viewerId: "player-2",
      });
      await flushPromises();

      expect(playerNotes).not.toHaveBeenCalled();
      expect(masterNotes).not.toHaveBeenCalled();
    });
  });
});

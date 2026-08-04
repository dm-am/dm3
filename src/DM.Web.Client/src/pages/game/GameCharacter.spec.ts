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
import { gameApi, type Character, type Game } from "@/entities/game";
import { useGameDetailsStore } from "@/entities/game";
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
  author: { username: "gamer", role: "Player" },
  totalPostsCount: 17,
  attributes: [{ id: "spec-1", title: "Раса", value: "Полурослик" }],
} as unknown as Character;

function render() {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useGameDetailsStore();
  store.game = {
    id: "the-game-guid",
    publicId: "the-game",
    title: "Хроники Амбера",
    participation: [],
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
});

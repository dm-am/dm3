/**
 * @vitest-environment jsdom
 */

// A created character answers with the roster, read again before the page
// leaves for it, and with no plate.
import { beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h, watchEffect } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  useGameDetailsStore,
  type Character,
  type Game,
} from "@/entities/game";
import { useToast } from "@/shared/lib/composables/useToast";
import { provideZoneSection } from "@/shared/lib/composables/useZoneSection";
import CharacterCreate from "./CharacterCreate.vue";

/** The route this page reads, and the navigations it makes. */
const routed = vi.hoisted(() => ({
  query: {} as Record<string, string>,
  push: vi.fn(),
  replace: vi.fn(),
}));

vi.mock("vue-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-router")>()),
  useRoute: () => ({ params: { id: "the-game" }, query: routed.query }),
  useRouter: () => ({ push: routed.push, replace: routed.replace }),
}));

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

function render({
  npc = false,
  participation = [] as string[],
  blacklisted = false,
  recruiting = true,
} = {}) {
  routed.query = npc ? { npc: "1" } : {};

  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useGameDetailsStore();
  store.game = {
    id: "the-game-guid",
    publicId: "the-game",
    status: "Active",
    participation,
    recruitment: { isOpen: recruiting },
    schema: { specifications: [] },
    viewerBlacklisted: blacklisted,
  } as unknown as Game;
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

  const wrapper = mount(host, {
    global: {
      plugins: [pinia],
      stubs: { CharacterForm: CharacterFormStub },
    },
  });

  return { wrapper, store, loadCharacters, announced };
}

describe("CharacterCreate", () => {
  beforeEach(() => {
    for (const shown of [...toast.toasts.value]) toast.dismiss(shown.id);
    routed.push.mockReset();
    vi.restoreAllMocks();
  });

  it.each([
    ["an application", {}],
    ["a new NPC", { npc: true, participation: ["Owner"] }],
  ])("leaves %s for a roster read again, silently", async (_, options) => {
    const { wrapper, loadCharacters } = render(options);

    await wrapper.get("button.save").trigger("click");
    await flushPromises();

    expect(loadCharacters).toHaveBeenCalledWith("the-game");
    expect(routed.push).toHaveBeenCalledWith({
      name: "game-characters",
      params: { id: "the-game" },
    });
    expect(saidOutLoud()).toEqual([]);
  });

  it("does not leave until the roster has answered", async () => {
    const { wrapper, store } = render();
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

    expect(routed.push).not.toHaveBeenCalled();

    answer();
    await flushPromises();

    expect(routed.push).toHaveBeenCalledTimes(1);
  });

  it("leads the viewer on the blacklist back to the roster", () => {
    routed.replace.mockClear();
    const { wrapper } = render({ blacklisted: true });

    expect(wrapper.find("button.save").exists()).toBe(false);
    expect(wrapper.text()).toBe("");
    expect(routed.replace).toHaveBeenCalledWith({
      name: "game-characters",
      params: { id: "the-game" },
    });
  });

  it("leads a viewer who does not lead the game away from the NPC form", () => {
    routed.replace.mockClear();
    const { wrapper } = render({ npc: true, participation: ["Player"] });

    expect(wrapper.find("button.save").exists()).toBe(false);
    expect(wrapper.text()).toBe("");
    expect(routed.replace).toHaveBeenCalledWith({
      name: "game-characters",
      params: { id: "the-game" },
    });
  });

  // An invited player applies past a closed recruitment, so the form stays.
  it("says over the form who applies while the recruitment is closed", () => {
    routed.replace.mockClear();
    const { wrapper } = render({ recruiting: false });

    expect(wrapper.find("button.save").exists()).toBe(true);
    expect(wrapper.get(".intro").text()).toBe(
      "Набор игроков закрыт: заявку подают по приглашению мастера",
    );
    expect(routed.replace).not.toHaveBeenCalled();
  });

  it.each([
    ["an application", {}, "Новый персонаж"],
    [
      "a non-player character",
      { npc: true, participation: ["Owner"] },
      "Новый неигровой персонаж",
    ],
  ])("names the form of %s in the heading", (_, options, section) => {
    const { announced } = render(options);

    expect(announced.value).toBe(section);
  });

  it("offers the form to a reader who is not on the blacklist", () => {
    routed.replace.mockClear();
    const { wrapper } = render();

    expect(wrapper.find("button.save").exists()).toBe(true);
    expect(routed.replace).not.toHaveBeenCalled();
  });
});

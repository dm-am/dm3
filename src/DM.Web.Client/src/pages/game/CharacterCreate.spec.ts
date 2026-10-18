/**
 * @vitest-environment jsdom
 */

/**
 * Creating a character answered with a plate in the corner until 20 September
 * 2026 — "Заявка отправлена" for an application, "NPC создан" for an NPC —
 * while the page was already leaving for the game's roster, where the new sheet
 * is the first card of its section (the list comes back newest first) and the
 * heading above it says more than the plate did: "На рассмотрении" for an
 * application the master has yet to answer, "Неигровые персонажи" for an NPC.
 *
 * What replaces the plate is the roster being true when the reader arrives, and
 * that is not free. The roster is shared state: the sidebar fills it on the way
 * into the game, and the roster page asks the endpoint for it only when it is
 * empty. A page that navigated without fetching it again would land the reader
 * on exactly the list that was loaded before their character existed — a screen
 * with every reason to be believed, and out of date.
 *
 * So both tests read the roster at the moment the page leaves. That is the only
 * moment that decides what the reader sees, and the assertion fails both ways:
 * if the fetch goes, the roster at departure is the stale one; if the plate
 * comes back, it is said out loud beside a screen that already answered.
 */
import { beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  useGameDetailsStore,
  type Character,
  type Game,
} from "@/entities/game";
import { useToast } from "@/shared/lib/composables/useToast";
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

/**
 * The sheet itself is not the subject: the form owns its own submit and its own
 * refusals (CharacterForm.spec). All this page sees of it is the "saved" event,
 * so the stub is the press that raises one.
 */
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

/** The roster as the endpoint would answer it once the character exists. */
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
  // The roster the sidebar already loaded, from before this character existed.
  store.characters = [character()];

  // The endpoint, as far as this page is concerned: it answers with the roster
  // the character is now part of.
  const loadCharacters = vi
    .spyOn(store, "loadCharacters")
    .mockImplementation(async () => {
      store.characters = [CREATED, character()];
    });

  // What the roster held at the moment the page left for it — the list the
  // reader is about to read, since the roster page does not fetch a roster it
  // already has.
  let rosterAtDeparture: string[] = [];
  routed.push.mockImplementation(() => {
    rosterAtDeparture = store.characters.map((c) => c.name);
  });

  const wrapper = mount(CharacterCreate, {
    global: {
      plugins: [pinia],
      stubs: { CharacterForm: CharacterFormStub },
    },
  });

  return {
    wrapper,
    store,
    loadCharacters,
    roster: () => rosterAtDeparture,
  };
}

describe("CharacterCreate", () => {
  beforeEach(() => {
    for (const shown of [...toast.toasts.value]) toast.dismiss(shown.id);
    routed.push.mockReset();
    vi.restoreAllMocks();
  });

  it("leaves an application on a roster that already carries it, silently", async () => {
    const { wrapper, loadCharacters, roster } = render();

    await wrapper.get("button.save").trigger("click");
    await flushPromises();

    expect(loadCharacters).toHaveBeenCalledWith("the-game");
    expect(routed.push).toHaveBeenCalledWith({
      name: "game-characters",
      params: { id: "the-game" },
    });
    // The card is on the screen the reader lands on, under "На рассмотрении".
    expect(roster()).toContain("Ронин");
    // And nothing says so a second time in the corner.
    expect(saidOutLoud()).toEqual([]);
  });

  it("leaves a new NPC on a roster that already carries it, silently", async () => {
    // NPC authoring is the master's; the page refuses the form to anyone else.
    const { wrapper, loadCharacters, roster } = render({
      npc: true,
      participation: ["Owner"],
    });

    await wrapper.get("button.save").trigger("click");
    await flushPromises();

    expect(loadCharacters).toHaveBeenCalledWith("the-game");
    expect(routed.push).toHaveBeenCalledWith({
      name: "game-characters",
      params: { id: "the-game" },
    });
    // Under "Неигровые персонажи" this time, and equally unannounced.
    expect(roster()).toContain("Ронин");
    expect(saidOutLoud()).toEqual([]);
  });

  /**
   * The fetch is awaited rather than fired alongside the navigation, so the
   * roster is right at its first paint. Fired alongside, the reader would meet
   * a list without their character and watch it correct itself — which is the
   * same silence the plate was papering over, only shorter.
   */
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

  /**
   * The game's blacklist closes the application whatever the recruitment
   * says, and nothing offers this page to the viewer on the list. An address
   * typed by hand gets neither the form nor a sentence about the list: it
   * leads back to the roster.
   */
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

  /**
   * "Создать NPC" is a row for the leads of the game alone, so nobody else
   * reaches the NPC form but by typing its address. He gets neither the form
   * nor a sentence saying who may use it: the address leads to the roster.
   */
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

  // An invited player applies whatever the recruitment says, and nothing tells
  // the page who is invited: the form stays, and the line over it says who
  // applies to such a game rather than what will not be accepted.
  it("says over the form who applies while the recruitment is closed", () => {
    routed.replace.mockClear();
    const { wrapper } = render({ recruiting: false });

    expect(wrapper.find("button.save").exists()).toBe(true);
    expect(wrapper.get(".intro").text()).toBe(
      "Набор игроков закрыт: заявку подают по приглашению мастера",
    );
    expect(routed.replace).not.toHaveBeenCalled();
  });

  it("offers the form to a reader who is not on the blacklist", () => {
    routed.replace.mockClear();
    const { wrapper } = render();

    expect(wrapper.find("button.save").exists()).toBe(true);
    expect(routed.replace).not.toHaveBeenCalled();
  });
});

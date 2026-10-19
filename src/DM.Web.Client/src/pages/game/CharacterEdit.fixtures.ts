// The page of a character; its specs mock vue-router with the doubles.
import { vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  characterApi,
  useGameDetailsStore,
  GameStatus,
  type Character,
  type Game,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import CharacterEdit from "./CharacterEdit.vue";
import { routed } from "./CharacterEdit.doubles";

// The confirmation is the real window; the sheet answers its own save.
const stubs = {
  CharacterForm: true,
  Teleport: true,
};

export const FLAGS = { ownsRefusal: true, ownsRateLimit: true };

const toast = useToast();
export const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

const VIEWER = "player-1";

/** The author of somebody else's character. */
export const OTHER = { id: "player-2", username: "другой", role: "Player" };

type CharacterOverrides = Partial<{
  id: string;
  name: string;
  status: string;
  isDead: boolean;
  isPlayerLeft: boolean;
  isPlayerExiled: boolean;
  isNpc: boolean;
  author: { id: string; username: string; role: string } | undefined;
  privacy: { isNpc: boolean; editByMaster: boolean; editPostByMaster: boolean };
}>;

export const character = (over: CharacterOverrides = {}): Character =>
  ({
    id: "char-1",
    name: "Ронин",
    status: "Active",
    isDead: false,
    isPlayerLeft: false,
    isPlayerExiled: false,
    isNpc: false,
    author: { id: VIEWER, username: "gamer", role: "Player" },
    attributes: [],
    privacy: { isNpc: false, editByMaster: false, editPostByMaster: false },
    totalPostsCount: 0,
    ...over,
  }) as unknown as Character;

export function render({
  participation = [],
  subject = character(),
  others = [],
  becomes,
  blacklisted = false,
  read,
}: {
  participation?: string[];
  blacklisted?: boolean;
  subject?: Character;
  others?: Character[];
  /** The character in the roster after the press; left out, a deletion. */
  becomes?: Character;
  /** The answer of the read the form is fed from; left out, the subject. */
  read?: { data: unknown; error: unknown };
} = {}) {
  const pinia = createPinia();
  setActivePinia(pinia);
  useAuthStore().user = { id: VIEWER } as never;

  const store = useGameDetailsStore();
  store.game = {
    id: "the-game-guid",
    publicId: "the-game",
    status: GameStatus.Active,
    participation,
    schema: { specifications: [] },
    viewerBlacklisted: blacklisted,
  } as unknown as Game;
  store.characters = [subject, ...others];

  vi.spyOn(characterApi, "getCharacterForEdit").mockResolvedValue(
    (read ?? { data: { resource: subject }, error: null }) as never,
  );

  const loadCharacters = vi
    .spyOn(store, "loadCharacters")
    .mockImplementation(async () => {
      store.characters = becomes
        ? [becomes, ...others]
        : store.characters.filter((c) => c.id !== subject.id);
    });

  // The roster page reuses a loaded roster: this is the list the reader sees.
  let rosterAtDeparture: string[] = [];
  let pageAtDeparture = { empty: false, status: false, window: false };
  routed.push.mockImplementation(() => {
    rosterAtDeparture = store.characters.map((c) => c.name);
    const section = wrapper.find(".status-section");
    pageAtDeparture = {
      empty: wrapper.find(".empty").exists(),
      status: section.exists() && section.isVisible(),
      window: wrapper.find(".dialog-container").exists(),
    };
  });

  const wrapper = mount(CharacterEdit, {
    global: { plugins: [pinia], stubs },
  });

  return {
    wrapper,
    store,
    loadCharacters,
    roster: () => rosterAtDeparture,
    page: () => pageAtDeparture,
  };
}

export function resetPage() {
  for (const shown of [...toast.toasts.value]) toast.dismiss(shown.id);
  routed.push.mockReset();
  routed.replace.mockReset();
  vi.restoreAllMocks();
}

/** Pressed through its confirmation where it asks one. */
export async function press(
  wrapper: ReturnType<typeof render>["wrapper"],
  label: string,
) {
  const button = wrapper.findAll(".status-btn").find((b) => b.text() === label);
  if (!button) throw new Error(`no lifecycle button reading "${label}"`);
  await button.trigger("click");
  const confirm = wrapper.find(".dialog-btn-submit");
  if (confirm.exists()) await confirm.trigger("click");
  await flushPromises();
}

export function statusInWords(
  wrapper: ReturnType<typeof render>["wrapper"],
): string | null {
  const line = wrapper.find(".status-value");
  return line.exists() ? line.text() : null;
}

export function changesStatus() {
  return vi
    .spyOn(characterApi, "changeCharacterStatus")
    .mockResolvedValue({ data: null, error: null } as never);
}

export const buttons = (wrapper: ReturnType<typeof render>["wrapper"]) =>
  wrapper.findAll(".status-btn").map((b) => b.text());

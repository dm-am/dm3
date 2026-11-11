// The page of a character, mounted on the real router at its address.
import { vi } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { createPinia, type Pinia } from "pinia";
import type { Router } from "vue-router";
import {
  characterApi,
  useGameDetailsStore,
  GameStatus,
  type Character,
  type GameParticipation,
} from "@/entities/game";
import {
  createMockCharacter,
  createMockGame,
} from "@/entities/game/model/store.fixtures";
import { createMockUser } from "@/entities/forum/model/store.fixtures";
import { useAuthStore } from "@/entities/user";
import { asServed } from "@/shared/api/models";
import { useToast } from "@/shared/lib/composables/useToast";
import { mountPage } from "@/shared/lib/testing/mountPage";
import CharacterEdit from "./CharacterEdit.vue";

// The confirmation is the real window; the sheet answers its own save.
const stubs = {
  CharacterForm: true,
  Teleport: true,
};

/** The sheet of char-1, where every test opens the page. */
export const SHEET = "/game/the-game/characters/char-1/edit";
export const ROSTER = "/game/the-game/characters";
const routes: [string, string][] = [
  ["game-character-edit", "/game/:id/characters/:characterId/edit"],
  ["game-characters", "/game/:id/characters"],
];

export const address = (router: Router) => router.currentRoute.value.fullPath;

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

export const character = (over: CharacterOverrides = {}): Character => ({
  ...createMockCharacter("char-1", "Ронин"),
  author: asServed(createMockUser(VIEWER, "gamer")),
  attributes: asServed([]),
  privacy: { isNpc: false, editByMaster: false, editPostByMaster: false },
  ...(over as Partial<Character>),
});

/** A running game the viewer takes part in, with the roster its page reads. */
function runningGame(
  pinia: Pinia,
  participation: GameParticipation[],
  blacklisted: boolean,
  roster: Character[],
) {
  useAuthStore(pinia).user = { id: VIEWER } as never;
  const store = useGameDetailsStore(pinia);
  store.game = {
    ...createMockGame("the-game-guid", "Игра"),
    publicId: asServed("the-game"),
    status: GameStatus.Active,
    participation: asServed(participation),
    schema: { specifications: [], version: 0 },
    viewerBlacklisted: asServed(blacklisted),
  };
  store.characters = roster;
  return store;
}

/** The read the form is fed from; held, it answers when the returned call is made. */
function stubRead(
  subject: Character,
  read: { data: unknown; error: unknown } | undefined,
  held: boolean,
) {
  const answer = (read ?? {
    data: { resource: subject },
    error: null,
  }) as never;
  let answerRead = () => {};
  vi.spyOn(characterApi, "getCharacterForEdit").mockImplementation(() =>
    held
      ? new Promise((resolve) => {
          answerRead = () => resolve(answer);
        })
      : Promise.resolve(answer),
  );
  return () => answerRead();
}

export async function render({
  participation = [],
  subject = character(),
  others = [],
  becomes,
  blacklisted = false,
  read,
  held = false,
}: {
  participation?: GameParticipation[];
  blacklisted?: boolean;
  subject?: Character;
  others?: Character[];
  /** The character in the roster after the press; left out, a deletion. */
  becomes?: Character;
  /** The answer of the read the form is fed from; left out, the subject. */
  read?: { data: unknown; error: unknown };
  /** The read waits for answerRead, so the first render is the wait. */
  held?: boolean;
} = {}) {
  const pinia = createPinia();
  const store = runningGame(pinia, participation, blacklisted, [
    subject,
    ...others,
  ]);
  const answerRead = stubRead(subject, read, held);

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
  // Read at the call: a guard runs turns later, after the window began to close.
  const departure = (router: Router) => {
    const push = router.push.bind(router);
    vi.spyOn(router, "push").mockImplementation((to) => {
      rosterAtDeparture = store.characters.map((c) => c.name);
      const section = wrapper.find(".status-section");
      pageAtDeparture = {
        empty: wrapper.find(".empty").exists(),
        status: section.exists() && section.isVisible(),
        window: wrapper.find(".dialog-container").exists(),
      };
      return push(to);
    });
  };

  const { wrapper, router } = await mountPage(CharacterEdit, SHEET, {
    routes,
    pinia,
    stubs,
    beforeMount: departure,
    // In the document: jsdom computes no style for a detached element, so the
    // section v-show hides would read as shown.
    attachTo: document.body.appendChild(document.createElement("div")),
  });

  return {
    wrapper,
    router,
    store,
    answerRead,
    loadCharacters,
    roster: () => rosterAtDeparture,
    page: () => pageAtDeparture,
  };
}

export function resetPage() {
  document.body.innerHTML = "";
  for (const shown of [...toast.toasts.value]) toast.dismiss(shown.id);
  vi.restoreAllMocks();
}

export type Page = Awaited<ReturnType<typeof render>>["wrapper"];

/** Pressed through its confirmation where it asks one. */
export async function press(wrapper: Page, label: string) {
  const button = wrapper.findAll(".status-btn").find((b) => b.text() === label);
  if (!button) throw new Error(`no lifecycle button reading "${label}"`);
  await button.trigger("click");
  const confirm = wrapper.find(".dialog-btn-submit");
  if (confirm.exists()) await confirm.trigger("click");
  await flushPromises();
}

export function statusInWords(wrapper: Page): string | null {
  const line = wrapper.find(".status-value");
  return line.exists() ? line.text() : null;
}

export function changesStatus() {
  return vi
    .spyOn(characterApi, "changeCharacterStatus")
    .mockResolvedValue({ data: null, error: null } as never);
}

export const buttons = (wrapper: Page) =>
  wrapper.findAll(".status-btn").map((b) => b.text());

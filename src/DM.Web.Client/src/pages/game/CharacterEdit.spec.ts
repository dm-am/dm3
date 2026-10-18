/**
 * @vitest-environment jsdom
 */

/**
 * What this screen says out loud, and what it shows instead.
 *
 * Deleting a character answered with a plate in the corner until 20 September
 * 2026 — "Персонаж удален", "NPC удален" — and it was the one answer on that
 * screen the reader did not need. The deletion is the whole of what is left to
 * look at: the roster comes back without the character (the endpoint never
 * returns a removed one), and the page the reader was standing on, the sheet of
 * the character that no longer exists, is left for that roster.
 *
 * The two deletions below read the roster at the moment the page leaves, and
 * check that nothing is said out loud beside it. Written that way because the
 * plate can only go if both halves hold: a deletion that left the reader on the
 * sheet, or dropped them on a list still carrying the card, would be a screen
 * that says nothing, and then the plate would be right.
 *
 * The status transitions had six plates of their own, and the screen they stood
 * over never wrote the status down: the section was titled "Статус персонажа"
 * and held buttons only, so a reader who looked up a minute later could tell an
 * accepted application from a dead character by which buttons were in the row
 * and by nothing else. The word is on the page now, in the caption the card and
 * the character's own sheet use, and the transitions that reach it are read
 * here before and after the press — before, because a line that prints the
 * right word only by accident would pass an after-check alone.
 *
 * Declining kept a plate one wave longer, for a reason that was true of the
 * page and not of the transition: a declined application is not a character
 * this page may edit, so the roster's answer dropped it from the selector and
 * the page moved on — to somebody else's sheet or to the empty text — with
 * nothing on it about the application. The page now holds on to the one it has
 * just declined until the reader switches away, so the seventh transition is
 * read the same way as the other six: on the same sheet, in the status line.
 * The two tests for it check both halves, that the sheet stayed and that
 * switching lets the application go.
 */
import { beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  gameApi,
  useGameDetailsStore,
  GameStatus,
  type Character,
  type Game,
} from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import CharacterEdit from "./CharacterEdit.vue";

/** The route this page reads, and the navigation the deletion makes. */
const routed = vi.hoisted(() => ({
  push: vi.fn(),
  replace: vi.fn(),
}));

vi.mock("vue-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-router")>()),
  useRoute: () => ({
    params: { id: "the-game", characterId: "char-1" },
    query: {},
  }),
  useRouter: () => ({ push: routed.push, replace: routed.replace }),
}));

/**
 * The confirmation is the real window, drawn in place: a dangerous transition
 * and a deletion are its request, and a refusal is printed in it. The sheet is
 * stubbed whole: it answers its own save with its own button, and none of that
 * is what a deletion is read by.
 */
const stubs = {
  CharacterForm: true,
  Teleport: true,
};

/** The flags of a request sent from the confirmation window. */
const FLAGS = { ownsRefusal: true, ownsRateLimit: true };

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

const VIEWER = "player-1";

/** Somebody else's character: the viewer is the game master, not the owner. */
const OTHER = { id: "player-2", username: "другой", role: "Player" };

/** The fields of a character these tests set; ids are branded on the wire. */
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

const character = (over: CharacterOverrides = {}): Character =>
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

function render({
  participation = [],
  subject = character(),
  others = [],
  becomes,
  blacklisted = false,
}: {
  participation?: string[];
  /** The viewer is on the blacklist of the game. */
  blacklisted?: boolean;
  subject?: Character;
  /** The rest of the roster, the same before the press and after it. */
  others?: Character[];
  /**
   * The character as the roster answers it after the press. Left out, the
   * roster answers without it at all — which is the deletion: the endpoint
   * never returns a removed character. A declined application it does return,
   * as Declined, the way the roster query keeps every character that is not
   * removed.
   */
  becomes?: Character;
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

  // The sheet the form is seeded from; the page asks for it on every selection.
  vi.spyOn(gameApi, "getCharacterForEdit").mockResolvedValue({
    data: { resource: subject },
    error: null,
  } as never);

  // The roster endpoint, as far as this page is concerned: it answers the
  // character in its new state, or not at all when the character is gone.
  const loadCharacters = vi
    .spyOn(store, "loadCharacters")
    .mockImplementation(async () => {
      store.characters = becomes
        ? [becomes, ...others]
        : store.characters.filter((c) => c.id !== subject.id);
    });

  // What the roster held at the moment the page left for it — the list the
  // reader is about to read, since the roster page does not fetch a roster it
  // already has.
  let rosterAtDeparture: string[] = [];
  routed.push.mockImplementation(() => {
    rosterAtDeparture = store.characters.map((c) => c.name);
  });

  const wrapper = mount(CharacterEdit, {
    global: { plugins: [pinia], stubs },
  });

  return { wrapper, store, loadCharacters, roster: () => rosterAtDeparture };
}

/**
 * The lifecycle button that carries this label, pressed through its confirm.
 *
 * Only the dangerous transitions ask for one; accepting an application and
 * reviving a character run straight off the click.
 */
async function press(
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

/** The status as the page writes it, or null when the page writes none. */
function statusInWords(
  wrapper: ReturnType<typeof render>["wrapper"],
): string | null {
  const line = wrapper.find(".status-value");
  return line.exists() ? line.text() : null;
}

/**
 * The names the selector offers. The page draws it only when there is more
 * than one character to choose from, so an empty list is also "no selector".
 */
function namesInSelector(
  wrapper: ReturnType<typeof render>["wrapper"],
): string[] {
  return wrapper
    .findAll("select#character-switch option")
    .map((option) => option.text());
}

/** The transition endpoint, accepting whatever it is asked for. */
function changesStatus() {
  return vi
    .spyOn(gameApi, "changeCharacterStatus")
    .mockResolvedValue({ data: null, error: null } as never);
}

describe("CharacterEdit", () => {
  beforeEach(() => {
    for (const shown of [...toast.toasts.value]) toast.dismiss(shown.id);
    routed.push.mockReset();
    routed.replace.mockReset();
    vi.restoreAllMocks();
  });

  it("leaves a deleted character for a roster without it, silently", async () => {
    const remove = vi
      .spyOn(gameApi, "deleteCharacter")
      .mockResolvedValue({ data: null, error: null } as never);

    const { wrapper, loadCharacters, roster } = render();
    await flushPromises();

    await press(wrapper, "Удалить персонажа");

    expect(remove).toHaveBeenCalledWith("char-1", FLAGS);
    expect(loadCharacters).toHaveBeenCalledWith("the-game");
    // The sheet of a character that no longer exists is not a screen to stay on.
    expect(routed.push).toHaveBeenCalledWith({
      name: "game-characters",
      params: { id: "the-game" },
    });
    // And the card is gone from the list the reader lands on.
    expect(roster()).not.toContain("Ронин");
    expect(saidOutLoud()).toEqual([]);
  });

  it("leaves a deleted NPC for a roster without it, silently", async () => {
    const remove = vi
      .spyOn(gameApi, "deleteCharacter")
      .mockResolvedValue({ data: null, error: null } as never);

    // An NPC has no author; the master deletes it.
    const { wrapper, loadCharacters, roster } = render({
      participation: ["Owner"],
      subject: character({
        name: "Трактирщик",
        isNpc: true,
        author: undefined,
        privacy: {
          isNpc: true,
          editByMaster: true,
          editPostByMaster: false,
        },
      }),
    });
    await flushPromises();

    await press(wrapper, "Удалить NPC");

    expect(remove).toHaveBeenCalledWith("char-1", FLAGS);
    expect(loadCharacters).toHaveBeenCalledWith("the-game");
    expect(routed.push).toHaveBeenCalledWith({
      name: "game-characters",
      params: { id: "the-game" },
    });
    expect(roster()).not.toContain("Трактирщик");
    expect(saidOutLoud()).toEqual([]);
  });

  /**
   * A refusal is the other rule: about failure the sheet is silent by itself -
   * it stands exactly as it stood, whether the server accepted the press or
   * refused it - and the confirmation that sent the request says it, in
   * itself, staying open over the sheet. This is also the canary for the two
   * tests above: it is what proves an empty list of plates there means nothing
   * was said, rather than that nothing could be heard.
   */
  it("says a refused deletion in the window it was asked in", async () => {
    vi.spyOn(gameApi, "deleteCharacter").mockResolvedValue({
      data: null,
      error: { status: 404, title: "Персонаж не найден" },
    } as never);

    const { wrapper, loadCharacters } = render();
    await flushPromises();

    await press(wrapper, "Удалить персонажа");

    expect(loadCharacters).not.toHaveBeenCalled();
    expect(routed.push).not.toHaveBeenCalled();
    expect(wrapper.get(".dialog-refusal").text()).toBe("Персонаж не найден");
    expect(wrapper.find(".dialog-container").exists()).toBe(true);
    expect(saidOutLoud()).toEqual([]);
  });

  it("writes an accepted application into the status, not into a plate", async () => {
    const change = changesStatus();

    const { wrapper } = render({
      participation: ["Owner"],
      subject: character({ status: "UnderReview", author: OTHER }),
      becomes: character({ status: "Active", author: OTHER }),
    });
    await flushPromises();

    expect(statusInWords(wrapper)).toBe("На рассмотрении");

    await press(wrapper, "Принять в игру");

    expect(change).toHaveBeenCalledWith("char-1", "Accept", undefined);
    expect(statusInWords(wrapper)).toBe("В игре");
    expect(saidOutLoud()).toEqual([]);
  });

  it("keeps the status legible when exiling leaves only the way back", async () => {
    const change = changesStatus();

    // The master exiles somebody else's character: after it the one move left
    // to him is taking the exile back, and no deletion either. The section is
    // what says where the character stands.
    const { wrapper } = render({
      participation: ["Owner"],
      subject: character({ author: OTHER }),
      becomes: character({
        author: OTHER,
        status: "Retired",
        isPlayerExiled: true,
      }),
    });
    await flushPromises();

    expect(statusInWords(wrapper)).toBe("В игре");

    await press(wrapper, "Изгнать из игры");

    expect(change).toHaveBeenCalledWith("char-1", "Exile", FLAGS);
    expect(wrapper.findAll(".status-btn").map((b) => b.text())).toEqual([
      "Вернуть в игру",
    ]);
    expect(statusInWords(wrapper)).toBe("Выведен из игры");
    expect(saidOutLoud()).toEqual([]);
  });

  it("writes a character out of the game into the status of their own sheet", async () => {
    const change = changesStatus();

    // The owner leaves the game: their own page, their own character.
    const { wrapper } = render({
      subject: character(),
      becomes: character({ status: "Retired", isPlayerLeft: true }),
    });
    await flushPromises();

    expect(statusInWords(wrapper)).toBe("В игре");

    await press(wrapper, "Покинуть игру");

    expect(change).toHaveBeenCalledWith("char-1", "Leave", FLAGS);
    expect(statusInWords(wrapper)).toBe("Покинул игру");
    expect(saidOutLoud()).toEqual([]);
  });

  /**
   * The transition the page used to be unable to show. A declined application
   * is not a character this page may edit, and the roster's answer dropped it:
   * with nothing else to edit the page fell to the empty text, and the status
   * line fell with it — the plate was the only thing left saying anything. The
   * page now keeps the application it has just declined, and the line answers
   * for it the way it answers for the other six.
   */
  it("writes a declined application into the status of the sheet it was declined on", async () => {
    const change = changesStatus();

    const { wrapper } = render({
      participation: ["Owner"],
      subject: character({ status: "UnderReview", author: OTHER }),
      becomes: character({ status: "Declined", author: OTHER }),
    });
    await flushPromises();

    expect(statusInWords(wrapper)).toBe("На рассмотрении");

    await press(wrapper, "Отклонить заявку");

    expect(change).toHaveBeenCalledWith("char-1", "Decline", FLAGS);
    // The sheet stayed: no empty text, no navigation, and the row of buttons
    // emptied out under a line that says why.
    expect(wrapper.text()).not.toContain(
      "Нет персонажей, доступных для редактирования",
    );
    expect(routed.push).not.toHaveBeenCalled();
    expect(wrapper.findAll(".status-btn")).toHaveLength(0);
    expect(statusInWords(wrapper)).toBe("Заявка отклонена");
    expect(saidOutLoud()).toEqual([]);
  });

  /**
   * The other half of keeping it: the page must not move to another sheet when
   * the roster answers, and the application must not stay in the selector
   * forever either. Switching away is what lets it go — a declined application
   * is drawn nowhere else on the site, and the selector after the switch is the
   * same one a reload would draw.
   */
  it("keeps the declined application in the selector until the reader switches away", async () => {
    changesStatus();

    const stranger = character({
      id: "char-2",
      name: "Странник",
      author: OTHER,
    });
    const { wrapper } = render({
      participation: ["Owner"],
      subject: character({ status: "UnderReview", author: OTHER }),
      others: [stranger],
      becomes: character({ status: "Declined", author: OTHER }),
    });
    await flushPromises();

    expect(namesInSelector(wrapper)).toEqual(["Ронин", "Странник"]);

    await press(wrapper, "Отклонить заявку");

    // Still the sheet of "Ронин": the page did not replace the route with
    // somebody else's, and the selector still offers the application by name.
    expect(routed.replace).not.toHaveBeenCalled();
    expect(namesInSelector(wrapper)).toEqual(["Ронин", "Странник"]);
    expect(statusInWords(wrapper)).toBe("Заявка отклонена");

    await wrapper.get("select#character-switch").setValue("char-2");
    await flushPromises();

    expect(routed.replace).toHaveBeenCalledWith({
      name: "game-character-edit",
      params: { id: "the-game", characterId: "char-2" },
    });
    // With one character left there is no selector to draw, and the line reads
    // the character the reader switched to.
    expect(namesInSelector(wrapper)).toEqual([]);
    expect(statusInWords(wrapper)).toBe("В игре");
    expect(saidOutLoud()).toEqual([]);
  });

  it("says a refused transition in the window it was asked in", async () => {
    vi.spyOn(gameApi, "changeCharacterStatus").mockResolvedValue({
      data: null,
      error: { status: 404, title: "Персонаж не найден" },
    } as never);

    const { wrapper, loadCharacters } = render({
      subject: character(),
      becomes: character({ status: "Retired", isPlayerLeft: true }),
    });
    await flushPromises();

    await press(wrapper, "Покинуть игру");

    // The roster is not asked again, so the status line stands where it stood.
    expect(loadCharacters).not.toHaveBeenCalled();
    expect(statusInWords(wrapper)).toBe("В игре");
    expect(wrapper.get(".dialog-refusal").text()).toBe("Персонаж не найден");
    expect(wrapper.find(".dialog-container").exists()).toBe(true);
    expect(saidOutLoud()).toEqual([]);
  });

  /**
   * A transition that runs straight off the click has no window to speak in,
   * so its refusal stands under the row of buttons, where the press was, and
   * the status line above still says the status the server keeps.
   */
  it("says a refused transition without a question under the buttons", async () => {
    vi.spyOn(gameApi, "changeCharacterStatus").mockResolvedValue({
      data: null,
      error: { status: 404, title: "Персонаж не найден" },
    } as never);

    const { wrapper, loadCharacters } = render({
      participation: ["Owner"],
      subject: character({ status: "UnderReview", author: OTHER }),
    });
    await flushPromises();

    await press(wrapper, "Принять в игру");

    expect(loadCharacters).not.toHaveBeenCalled();
    expect(wrapper.get(".status-section .action-result").text()).toBe(
      "Персонаж не найден",
    );
    expect(saidOutLoud()).toEqual([]);
  });

  /**
   * The master at a player's character: its status and the moves of it are
   * his, its sheet only where the player lets him edit it (editByMaster).
   * Where the player does not, the form goes and so does any word about it:
   * what the reader may not do is not on the page. It used to stand there as
   * "Анкету этого персонажа может редактировать только его владелец".
   */
  describe("a player's character, in front of the master", () => {
    it("has no form and no word about it where the player keeps the sheet", async () => {
      const { wrapper } = render({
        participation: ["Owner"],
        subject: character({ author: OTHER }),
      });
      await flushPromises();

      expect(wrapper.find("character-form-stub").exists()).toBe(false);
      expect(wrapper.text()).not.toContain("редактировать");
      expect(statusInWords(wrapper)).toBe("В игре");
      expect(wrapper.findAll(".status-btn").length).toBeGreaterThan(0);
    });

    it("has the form where the player lets the master edit the sheet", async () => {
      const { wrapper } = render({
        participation: ["Owner"],
        subject: character({
          author: OTHER,
          privacy: {
            isNpc: false,
            editByMaster: true,
            editPostByMaster: false,
          },
        }),
      });
      await flushPromises();

      expect(wrapper.find("character-form-stub").exists()).toBe(true);
    });
  });

  /**
   * The game's blacklist closes the owner's application: editing it is
   * applying again. The form goes without a word in its place, and not with
   * the line meant for somebody else's character either. The sheet of a
   * character in play stays the owner's, list or not.
   */
  describe("on the blacklist", () => {
    const buttons = (wrapper: ReturnType<typeof render>["wrapper"]) =>
      wrapper.findAll(".status-btn").map((b) => b.text());

    it("leaves the owner's application closed, without a word about it", async () => {
      const { wrapper } = render({
        subject: character({ status: "UnderReview" }),
        blacklisted: true,
      });
      await flushPromises();

      expect(wrapper.find("character-form-stub").exists()).toBe(false);
      expect(wrapper.text()).not.toContain("черном списке");
      expect(wrapper.text()).not.toContain("только его владелец");
      expect(buttons(wrapper)).toEqual(["Удалить персонажа"]);
    });

    it("leaves the sheet of a character in play open to the owner", async () => {
      const { wrapper } = render({ subject: character(), blacklisted: true });
      await flushPromises();

      expect(wrapper.find("character-form-stub").exists()).toBe(true);
    });
  });

  /**
   * A character its player took out of the game comes back by nobody's
   * move: the owner has no "Вернуться в игру", and the master has "Вернуть в
   * игру" only for the character he killed.
   */
  describe("after the player left", () => {
    const leftTheGame = character({ status: "Retired", isPlayerLeft: true });

    const buttons = (wrapper: ReturnType<typeof render>["wrapper"]) =>
      wrapper.findAll(".status-btn").map((b) => b.text());

    it("offers the owner no way back", async () => {
      const { wrapper } = render({ subject: leftTheGame });
      await flushPromises();

      expect(buttons(wrapper)).toEqual(["Удалить персонажа"]);
    });

    it("offers the master no way back either, and revives only the dead", async () => {
      const someoneElse = { id: "player-2", username: "other", role: "Player" };
      const left = render({
        participation: ["Owner"],
        subject: character({
          status: "Retired",
          isPlayerLeft: true,
          author: someoneElse,
        }),
      });
      await flushPromises();
      expect(buttons(left.wrapper)).not.toContain("Вернуть в игру");

      const dead = render({
        participation: ["Owner"],
        subject: character({
          status: "Retired",
          isDead: true,
          author: someoneElse,
        }),
      });
      await flushPromises();
      expect(buttons(dead.wrapper)).toContain("Вернуть в игру");
    });
  });

  /**
   * Exile is the lead's move, so the lead takes it back, by its own transition
   * and not by the one that revives the dead. The player who was thrown out
   * has no such button on his own sheet.
   */
  describe("after the master exiled the character", () => {
    const someoneElse = { id: "player-2", username: "other", role: "Player" };
    const buttons = (wrapper: ReturnType<typeof render>["wrapper"]) =>
      wrapper.findAll(".status-btn").map((b) => b.text());

    it("brings the character back for the master", async () => {
      const change = changesStatus();
      const { wrapper } = render({
        participation: ["Owner"],
        subject: character({
          status: "Retired",
          isPlayerExiled: true,
          author: someoneElse,
        }),
        becomes: character({ status: "Active", author: someoneElse }),
      });
      await flushPromises();

      expect(buttons(wrapper)).toEqual(["Вернуть в игру"]);

      await press(wrapper, "Вернуть в игру");

      expect(change).toHaveBeenCalledWith("char-1", "Readmit", undefined);
      expect(statusInWords(wrapper)).toBe("В игре");
    });

    it("offers the exiled player no way back", async () => {
      const { wrapper } = render({
        subject: character({ status: "Retired", isPlayerExiled: true }),
      });
      await flushPromises();

      expect(buttons(wrapper)).not.toContain("Вернуть в игру");
    });

    it("offers the master nothing when the player had left as well", async () => {
      const { wrapper } = render({
        participation: ["Owner"],
        subject: character({
          status: "Retired",
          isPlayerExiled: true,
          isPlayerLeft: true,
          author: someoneElse,
        }),
      });
      await flushPromises();

      expect(buttons(wrapper)).not.toContain("Вернуть в игру");
    });
  });
});

/**
 * The transitions stand on every width in one line (ButtonRow), as on the
 * full version: none goes into "…" and the row does not scroll sideways.
 */
describe("CharacterEdit row of transitions", () => {
  it("stands every transition in a row of buttons, none held back", async () => {
    const { wrapper } = render({
      participation: ["Owner"],
      subject: character({ author: OTHER }),
    });
    await flushPromises();

    const row = wrapper.get(".status-actions");
    expect(row.classes()).toContain("button-row");
    expect(row.attributes()).not.toHaveProperty("data-fit-row");
    expect(wrapper.find(".scroll-row").exists()).toBe(false);
    const transitions = wrapper
      .findAll(".status-btn")
      .map((b) => [b.text(), b.attributes("data-fit-keep") !== undefined]);
    expect(transitions).toEqual([
      ["Отметить погибшим", false],
      ["Изгнать из игры", false],
    ]);
  });
});

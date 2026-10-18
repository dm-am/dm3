/**
 * @vitest-environment jsdom
 */

/**
 * What the reader sees after inviting an assistant to the game.
 *
 * Nothing in the roster can answer for it: the invited person joins the list
 * of assistants when the invitation is accepted, which is somebody else's
 * doing, later. The section used to say it in the corner, and the one line it
 * had on screen came from the game itself — a single slot filled with
 * FirstOrDefault out of an unordered token read, so a master who invites a
 * second assistant while the first invitation is out was shown the other
 * person's name and nothing else changed. The block of invitations that are
 * out is now read from the invitation endpoint, which knows all of them and
 * when each was sent.
 *
 * What each test here catches:
 *  - the block fed from the game DTO again: the second invitation is invisible
 *    behind the first;
 *  - the block left out: an invitation that went out leaves no trace on the
 *    page that sent it;
 *  - the row taken from the reload alone: nothing to show when that read does
 *    not come back;
 *  - the toast put back beside the block: one event announced twice.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  gameApi,
  useGameDetailsStore,
  type Game,
  type Invitation,
} from "@/entities/game";
import { useToast } from "@/shared/lib/composables/useToast";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import type { GeneralError } from "@/shared/api/models/common";
import RolesSection from "./RolesSection.vue";

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

const invitation = (
  id: string,
  username: string,
  createdUtc: string,
  type = "assistant",
): Invitation =>
  ({
    id,
    gameId: "the-game-guid",
    gameTitle: "Игра",
    invitedUser: { id: `u-${username}`, username },
    inviterUsername: "the-master",
    type,
    createdUtc,
  }) as unknown as Invitation;

/** The invitation that was already out when the master sent the next one. */
const EARLIER = invitation("i-1", "Другой", "2026-09-02T10:00:00Z");
/** The one the test sends. */
const FRESH = invitation("i-2", "Кандидат", "2026-09-20T09:05:00Z");

const served = (resources: Invitation[]) => ({
  data: { resources },
  error: null,
});

const refused = {
  data: null,
  error: { status: 400, title: "", type: "", traceId: "t" } as GeneralError,
};

const stubs = {
  UserLink: {
    props: ["user"],
    template: '<span class="named">{{ user.username }}</span>',
  },
  UserAutocomplete: {
    props: ["modelValue"],
    emits: ["update:modelValue"],
    template:
      '<input class="invitee" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
  },
  // The confirmation of a removal is the real window, drawn in place.
  Teleport: true,
};

let list: ReturnType<typeof vi.fn>;
let inviteAssistant: ReturnType<typeof vi.fn>;

/**
 * The section over a game with one assistant and one invitation already out.
 *
 * The game carries that first invitation in its single pending slot, which is
 * the trap: the block has to name whoever the endpoint names, not whoever the
 * slot happened to catch.
 */
async function mountSection(initial: Invitation[] = []) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useGameDetailsStore();
  store.game = {
    id: "the-game-guid",
    publicId: "the-game",
    title: "Игра",
    fullAssistants: [{ id: "u-1", username: "Помощник" }],
    mentor: null,
    pendingAssistant: EARLIER.invitedUser,
  } as unknown as Game;
  vi.spyOn(store, "loadGame").mockResolvedValue({ ok: true });

  list = vi.fn().mockResolvedValue(served(initial));
  inviteAssistant = vi.fn().mockResolvedValue({ data: FRESH, error: null });
  vi.spyOn(gameApi, "getGameInvitations").mockImplementation(
    list as unknown as typeof gameApi.getGameInvitations,
  );
  vi.spyOn(gameApi, "inviteAssistant").mockImplementation(
    inviteAssistant as unknown as typeof gameApi.inviteAssistant,
  );

  const wrapper = mount(RolesSection, {
    props: { canManageRoles: true },
    global: { plugins: [pinia], stubs },
  });
  await flushPromises();
  return wrapper;
}

/**
 * The waiting block, as one line of text, or "" when it is not drawn.
 * The no-break space a date keeps after "в" stays.
 */
const waiting = (wrapper: VueWrapper) =>
  wrapper.find(".pending").exists()
    ? wrapper
        .find(".pending")
        .text()
        .replace(/[^\S\u00a0]+/g, " ")
        .trim()
    : "";

/** Name somebody and press "Пригласить". */
async function invite(wrapper: VueWrapper, username: string) {
  await wrapper.find("input.invitee").setValue(username);
  await wrapper.find(".invite-row button").trigger("click");
  await flushPromises();
}

describe("Game RolesSection assistant invitation", () => {
  beforeEach(() => {
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("names the second invited person, not the one the game keeps in its slot", async () => {
    const wrapper = await mountSection([EARLIER]);
    list.mockResolvedValue(served([EARLIER, FRESH]));

    await invite(wrapper, "Кандидат");

    expect(waiting(wrapper)).toContain("Кандидат");
    expect(waiting(wrapper)).toContain(
      `отправлено ${formatDateFull(FRESH.createdUtc)}`,
    );
    // The one that was already out keeps its own row and its own moment.
    expect(waiting(wrapper)).toContain("Другой");
    expect(waiting(wrapper)).toContain(
      `отправлено ${formatDateFull(EARLIER.createdUtc)}`,
    );
    // And neither of them is in the roster: accepting is what puts a name
    // there.
    const roster = wrapper.findAll(".staff-list")[0].text();
    expect(roster).toContain("Помощник");
    expect(roster).not.toContain("Кандидат");
    expect(roster).not.toContain("Другой");
  });

  it("draws nothing about waiting invitations while the endpoint names none", async () => {
    const wrapper = await mountSection();

    expect(waiting(wrapper)).toBe("");
  });

  it("leaves player and reader invitations to the invitations section", async () => {
    const wrapper = await mountSection([
      invitation("i-7", "Игрок", "2026-09-03T08:00:00Z", "player"),
      invitation("i-8", "Читающий", "2026-09-04T08:00:00Z", "reader"),
    ]);

    expect(waiting(wrapper)).toBe("");
  });

  it("says nothing in the corner about the block it has just filled", async () => {
    const wrapper = await mountSection();
    list.mockResolvedValue(served([FRESH]));

    await invite(wrapper, "Кандидат");

    expect(saidOutLoud()).toEqual([]);
    expect(
      (wrapper.find("input.invitee").element as HTMLInputElement).value,
    ).toBe("");
  });

  it("keeps the row when the reload that follows does not come back", async () => {
    const wrapper = await mountSection();
    list.mockResolvedValue(refused);

    await invite(wrapper, "Кандидат");

    expect(waiting(wrapper)).toContain("Кандидат");
  });

  it("speaks up when the invitation is refused, since nothing moved", async () => {
    const wrapper = await mountSection();
    inviteAssistant.mockResolvedValue(refused);

    await invite(wrapper, "Кандидат");

    expect(waiting(wrapper)).toBe("");
    // Where the press was, and in no plate (the owner's rule of 26 September
    // 2026, UI_STANDARDS "Всплывающие сообщения").
    expect(wrapper.get(".invite-row + .action-result").text()).toBe(
      "Не удалось пригласить ассистента",
    );
    expect(saidOutLoud()).toEqual([]);
  });
});

/**
 * Taking an assistant off. The confirmation runs the request itself: it waits
 * for the answer, prints a refusal in itself and closes on success alone, and
 * the request carries the flags that keep the interceptor quiet about it.
 */
describe("Game RolesSection assistant removal", () => {
  beforeEach(() => {
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  async function remove(answer: unknown) {
    const wrapper = await mountSection();
    const request = vi
      .spyOn(gameApi, "removeAssistant")
      .mockResolvedValue(answer as never);
    await wrapper.find(".staff-item .remove-btn").trigger("click");
    await wrapper.find(".dialog-btn-submit").trigger("click");
    await flushPromises();
    return { wrapper, request };
  }

  it("takes the assistant off with the window's flags and closes", async () => {
    const { wrapper, request } = await remove({ data: null, error: null });

    expect(request).toHaveBeenCalledWith("the-game-guid", "Помощник", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(wrapper.find(".dialog-container").exists()).toBe(false);
  });

  it("prints a refused removal in the window and stays open", async () => {
    const { wrapper } = await remove(refused);

    expect(wrapper.get(".dialog-refusal").text()).toBe(
      "Не удалось удалить ассистента",
    );
    expect(wrapper.find(".dialog-container").exists()).toBe(true);
    expect(saidOutLoud()).toEqual([]);
  });
});

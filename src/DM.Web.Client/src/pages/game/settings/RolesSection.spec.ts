/**
 * @vitest-environment jsdom
 */

/**
 * An invited assistant joins the roster only once he accepts, so the block of
 * pending invitations, read from the invitation endpoint and not from the
 * game's single pending slot, is the answer.
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

const EARLIER = invitation("i-1", "Другой", "2026-09-02T10:00:00Z");
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
  // The badge of a site role, unless the place hides it.
  UserLink: {
    props: { user: Object, hideBadge: Boolean },
    template:
      '<span class="named">{{ user.username }}<i v-if="!hideBadge" class="badge" /></span>',
  },
  UserAutocomplete: {
    props: ["modelValue"],
    emits: ["update:modelValue"],
    template:
      '<input class="invitee" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
  },
  Teleport: true,
};

let list: ReturnType<typeof vi.fn>;
let inviteAssistant: ReturnType<typeof vi.fn>;

// The game's single pending slot holds the first invitation: the trap.
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

// The no-break space a date keeps after "в" stays.
const waiting = (wrapper: VueWrapper) =>
  wrapper.find(".pending").exists()
    ? wrapper
        .find(".pending")
        .text()
        .replace(/[^\S\u00a0]+/g, " ")
        .trim()
    : "";

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
    expect(waiting(wrapper)).toContain("Другой");
    expect(waiting(wrapper)).toContain(
      `отправлено ${formatDateFull(EARLIER.createdUtc)}`,
    );
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
    // Where the press was, in no plate (UI_STANDARDS "Всплывающие сообщения").
    expect(wrapper.get(".invite-row + .action-result").text()).toBe(
      "Не удалось пригласить ассистента",
    );
    expect(saidOutLoud()).toEqual([]);
  });
});

// The confirmation runs the request and prints its own refusal.
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

// In the settings of a game the staff are its people, not the site's: no badge of a role.
describe("Game RolesSection names", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("names the assistants, the waiting and the mentor without a role", async () => {
    const wrapper = await mountSection([EARLIER]);
    useGameDetailsStore().game!.mentor = {
      id: "u-m",
      username: "Наставник",
    } as never;
    await flushPromises();

    expect(wrapper.findAll(".named").map((n) => n.text())).toEqual([
      "Помощник",
      "Другой",
      "Наставник",
    ]);
    expect(wrapper.find(".badge").exists()).toBe(false);
  });
});

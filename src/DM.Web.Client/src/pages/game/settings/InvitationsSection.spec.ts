/**
 * @vitest-environment jsdom
 */

/**
 * A sent invitation is answered by its row: the endpoint serves no order, a
 * second invitation to the same seat replaces the first, and the reload after
 * it may fail.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { gameApi, useGameDetailsStore, type Invitation } from "@/entities/game";
import { createMockGame } from "@/entities/game/model/store.fixtures";
import { asServed } from "@/shared/api/models";
import { useToast } from "@/shared/lib/composables/useToast";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import type { GeneralError } from "@/shared/api/models/common";
import InvitationsSection from "./InvitationsSection.vue";
import { expectFirstReadStates } from "@/shared/lib/testing/firstRead";

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

const invitation = (
  id: string,
  username: string,
  createdUtc: string,
  type = "player",
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

const OLD = invitation("i-1", "Старожил", "2026-09-01T10:00:00Z");
const FRESH = invitation("i-2", "Новичок", "2026-09-20T09:05:00Z");
const FRESH_READER = invitation(
  "i-3",
  "Зритель",
  "2026-09-20T09:07:00Z",
  "reader",
);

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
    template: '<span class="invited">{{ user.username }}</span>',
  },
  UserAutocomplete: {
    props: ["modelValue"],
    emits: ["update:modelValue"],
    template:
      '<input class="invitee" :value="modelValue" @input="$emit(\'update:modelValue\', $event.target.value)" />',
  },
};

let list: ReturnType<typeof vi.fn>;
let invitePlayer: ReturnType<typeof vi.fn>;
let inviteReader: ReturnType<typeof vi.fn>;

/** `first`, when given, answers the read the section opens with. */
async function mountSection(
  initial: Invitation[] = [OLD],
  first?: Promise<unknown>,
) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useGameDetailsStore();
  store.game = {
    ...createMockGame("the-game-guid", "Игра"),
    publicId: asServed("the-game"),
  };

  list = vi.fn().mockResolvedValue(served(initial));
  if (first) list.mockReturnValueOnce(first);
  invitePlayer = vi.fn().mockResolvedValue({ data: FRESH, error: null });
  inviteReader = vi.fn().mockResolvedValue({ data: FRESH_READER, error: null });
  vi.spyOn(gameApi, "getGameInvitations").mockImplementation(
    list as unknown as typeof gameApi.getGameInvitations,
  );
  vi.spyOn(gameApi, "invitePlayer").mockImplementation(
    invitePlayer as unknown as typeof gameApi.invitePlayer,
  );
  vi.spyOn(gameApi, "inviteReader").mockImplementation(
    inviteReader as unknown as typeof gameApi.inviteReader,
  );

  const wrapper = mount(InvitationsSection, {
    props: { canManage: true },
    global: { plugins: [pinia], stubs },
  });
  await flushPromises();
  return wrapper;
}

// The no-break space a date keeps after "в" stays.
const rows = (wrapper: VueWrapper) =>
  wrapper.findAll(".inv-item").map((r) =>
    r
      .text()
      .replace(/[^\S\u00a0]+/g, " ")
      .trim(),
  );

async function invite(wrapper: VueWrapper, username: string) {
  await wrapper.find("input.invitee").setValue(username);
  await wrapper.find(".invite-row button").trigger("click");
  await flushPromises();
}

describe("Game InvitationsSection", () => {
  beforeEach(() => {
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("shows the invitation just sent as the last row, next to the controls", async () => {
    const wrapper = await mountSection();
    // The order the database happened to keep, the new one first.
    list.mockResolvedValue(served([FRESH, OLD]));

    await invite(wrapper, "Новичок");

    expect(rows(wrapper)).toHaveLength(2);
    expect(rows(wrapper).at(-1)).toContain("Новичок");
    expect(rows(wrapper).at(-1)).toContain(
      `отправлено ${formatDateFull(FRESH.createdUtc)}`,
    );
    expect(rows(wrapper).at(-1)).not.toContain(formatDateFull(OLD.createdUtc));
  });

  it("says nothing in the corner about the row it has just shown", async () => {
    const wrapper = await mountSection();
    list.mockResolvedValue(served([FRESH, OLD]));

    await invite(wrapper, "Новичок");

    expect(saidOutLoud()).toEqual([]);
    expect(
      (wrapper.find("input.invitee").element as HTMLInputElement).value,
    ).toBe("");
  });

  it("answers a reader invitation with its own row and no line in the corner", async () => {
    const wrapper = await mountSection();
    list.mockResolvedValue(served([OLD, FRESH_READER]));

    await wrapper.find("select").setValue("reader");
    await invite(wrapper, "Зритель");

    expect(inviteReader).toHaveBeenCalledTimes(1);
    expect(invitePlayer).not.toHaveBeenCalled();
    expect(rows(wrapper).at(-1)).toContain("Зритель");
    expect(rows(wrapper).at(-1)).toContain("Читатель");
    expect(saidOutLoud()).toEqual([]);
  });

  it("keeps the row when the reload that follows does not come back", async () => {
    const wrapper = await mountSection();
    list.mockResolvedValue(refused);

    await invite(wrapper, "Новичок");

    expect(rows(wrapper).at(-1)).toContain("Новичок");
    expect(wrapper.text()).not.toContain("Активных приглашений нет");
  });

  it("replaces the invitation of a person invited to the same seat again", async () => {
    const earlier = invitation("i-0", "Новичок", "2026-09-10T08:00:00Z");
    const wrapper = await mountSection([earlier]);
    list.mockResolvedValue(refused);

    await invite(wrapper, "Новичок");

    expect(rows(wrapper)).toHaveLength(1);
    expect(rows(wrapper)[0]).toContain(
      `отправлено ${formatDateFull(FRESH.createdUtc)}`,
    );
  });

  it("speaks up when the invitation is refused, since nothing moved", async () => {
    const wrapper = await mountSection();
    invitePlayer.mockResolvedValue(refused);

    await invite(wrapper, "Новичок");

    expect(rows(wrapper)).toHaveLength(1);
    // Where the press was, in no plate (UI_STANDARDS "Всплывающие сообщения").
    expect(wrapper.get(".invite-row + .action-result").text()).toBe(
      "Не удалось отправить приглашение",
    );
    expect(saidOutLoud()).toEqual([]);
  });
});

describe("Game InvitationsSection line that invites", () => {
  const css = componentCss(
    join(
      dirname(fileURLToPath(import.meta.url)),
      "../../../features/module-staff/ui/InvitationsEditor.vue",
    ),
  );

  it("is one line of the field, the list and the button at every width", () => {
    const rules = styleRules(css);
    const wraps = rules.filter(
      ({ selector, declarations }) =>
        selector.includes("invite-row") && "flex-wrap" in declarations,
    );
    expect(wraps).toEqual([]);
    const row = rules.filter((rule) => rule.selector === ".invite-row--kind");
    expect(
      row.map(({ declarations }) => declarations["grid-template-columns"]),
    ).toEqual(["minmax(0, 1fr) auto auto"]);
    expect(css).not.toContain("@container");
  });
});

// The list waits in its shape, a failed first read says so, and emptiness is
// said only once the answer came.
// The last block of the file: the spies it leaves are nobody else's.
describe("Game InvitationsSection before the list is read", () => {
  it("waits in its shape and says a failed first read, not an empty list", () =>
    expectFirstReadStates((first) => mountSection([], first), refused, {
      placeholder: ".inv-waiting",
      failed: "Не удалось загрузить приглашения",
      empty: "Активных приглашений нет",
    }));
});

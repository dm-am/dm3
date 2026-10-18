/**
 * @vitest-environment jsdom
 */

/**
 * Rooms have no list page: a game draws a page per room, and every room
 * setting lives in one section of this page. That makes this page the only
 * door to room management, and the door has to be shut — the flat
 * /game/:id/rooms list it replaced asked nothing at all about who was
 * reading it, and named every room of the game to anyone who typed the URL.
 *
 * The page has no single gate, because the server has no single gate behind
 * it. Four widths meet here and this spec pins each one to the intention that
 * enforces it:
 *  - GameIntention.EditSettings (the information form, the invitation list,
 *    and admission to the page itself): leads, the mentor curating THIS game,
 *    senior moderation;
 *  - GameIntention.Edit (rooms, the character sheet of the game, the blacklist
 *    including its read): leads and senior moderation, no curator;
 *  - InvitePlayer / InviteReader / CancelInvitation (the invitation writes):
 *    the game roles alone, without a rank clause, so a senior moderator reads
 *    that list without writing it;
 *  - InviteAssistant / RemoveUser (the roster writes): the master alone, while
 *    the roster itself costs a plain Read, so that section is drawn for
 *    everyone the page admits and only its controls narrow.
 *
 * Every flag is read from the sources the neighbouring management screens read
 * (useGameDetailsStore.isMaster / isAssistant / isMentor plus the viewer's site
 * role) — never a check invented here.
 *
 * A viewer the page admits to nothing has no page: the address opens the
 * site's own "not found", decided once the session is settled and the game is
 * not being read again for a new viewer.
 */
import { beforeEach, describe, expect, it, vi } from "vitest";
import { defineComponent, h } from "vue";
import { flushPromises, mount, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  GameParticipation,
  useGameDetailsStore,
  type Game,
} from "@/entities/game";
import { useAuthStore, UserRole, type User } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import type { GeneralError } from "@/shared/api/models/common";
import GameSettings from "./GameSettings.vue";

const { push, replace, session } = vi.hoisted(() => ({
  push: vi.fn(),
  replace: vi.fn(),
  session: { settled: Promise.resolve() as Promise<void> },
}));

// Only the route and the navigations the page holds: the delete action and
// the way out for a viewer it refuses. The rest of the router stays real, as in
// the sibling roster spec.
vi.mock("vue-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-router")>()),
  useRoute: () => ({
    params: { id: "the-game" },
    path: "/game/the-game/settings",
    query: {},
    hash: "",
  }),
  useRouter: () => ({ push, replace }),
}));

// The answer about the session the start of the page asks for, held by the
// test: settled at once unless a test keeps it out.
vi.mock("@/entities/user", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/user")>()),
  restoreSession: () => session.settled,
}));

const NOT_FOUND = {
  name: "not-found",
  params: { pathMatch: ["game", "the-game", "settings"] },
  query: {},
  hash: "",
};

/**
 * A section stands in as one marker div: this spec asks only whether it is
 * drawn. Render functions, not `template` strings, so the stubs hold whatever
 * Vue build the test runs against.
 */
const marker = (className: string) => ({
  setup: () => () => h("div", { class: className }),
});

/**
 * The invitations section is drawn for a wider audience than it accepts writes
 * from, so its stub also records the flag the page hands it.
 */
const invitationsStub = defineComponent({
  props: { canManage: { type: Boolean, default: false } },
  setup: (props) => () =>
    h("div", {
      class: "invitations-section-stub",
      "data-can-manage": String(props.canManage),
    }),
});

/**
 * The roster is the same shape: everyone the page admits reads it, only the
 * master gets its two controls, so the stub records the flag rather than its
 * own presence alone.
 */
const rolesStub = defineComponent({
  props: { canManageRoles: { type: Boolean, default: false } },
  setup: (props) => () =>
    h("div", {
      class: "roles-section-stub",
      "data-can-manage-roles": String(props.canManageRoles),
    }),
});

const stubs = {
  GameInfoSection: marker("info-section-stub"),
  AttributeSchemaSection: marker("schema-section-stub"),
  RoomsSection: marker("rooms-section-stub"),
  RolesSection: rolesStub,
  BlacklistSection: marker("blacklist-section-stub"),
  InvitationsSection: invitationsStub,
  // The confirmation of the delete is the real window, drawn in place.
  Teleport: true,
  RouterLink: { template: "<a><slot /></a>" },
};

const THE_VIEWER = "the-viewer";

function render(participation: GameParticipation[], viewer?: UserRole) {
  const pinia = createPinia();
  setActivePinia(pinia);
  // The viewer's session: participation carries the game roles, the site role
  // rides on the auth store user. No role at all is a guest.
  useAuthStore().user = viewer
    ? ({ username: THE_VIEWER, role: viewer } as User)
    : null;
  const store = useGameDetailsStore();
  store.game = {
    id: "the-game-guid",
    publicId: "the-game",
    participation,
  } as unknown as Game;
  return mount(GameSettings, { global: { plugins: [pinia], stubs } });
}

/** What the viewer actually sees, section by section. */
const sectionsOf = (wrapper: VueWrapper) => {
  const invitations = wrapper.find(".invitations-section-stub");
  const roles = wrapper.find(".roles-section-stub");
  return {
    info: wrapper.find(".info-section-stub").exists(),
    schema: wrapper.find(".schema-section-stub").exists(),
    rooms: wrapper.find(".rooms-section-stub").exists(),
    roles: roles.exists(),
    roleWrites:
      roles.exists() && roles.attributes("data-can-manage-roles") === "true",
    blacklist: wrapper.find(".blacklist-section-stub").exists(),
    invitations: invitations.exists(),
    invitationWrites:
      invitations.exists() &&
      invitations.attributes("data-can-manage") === "true",
    danger: wrapper.find(".danger-zone").exists(),
  };
};

const seenBy = (
  participation: GameParticipation[],
  viewer: UserRole = UserRole.RegularUser,
) => sectionsOf(render(participation, viewer));

describe("GameSettings admission", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    session.settled = Promise.resolve();
  });

  it("keeps the page away from a player of the game", () => {
    expect(seenBy([GameParticipation.Player]).info).toBe(false);
  });

  it("keeps it away from a subscribed reader", () => {
    expect(seenBy([GameParticipation.Reader]).info).toBe(false);
  });

  it("keeps it away from a passer-by", () => {
    expect(seenBy([]).info).toBe(false);
  });

  it("keeps it away from a junior moderator, whom EditSettings does not admit", () => {
    expect(seenBy([], UserRole.Moderator).info).toBe(false);
  });

  it("keeps it away from a site mentor who does not curate this game", () => {
    // The resolver arm reads the game roles, not the site rank: the curator of
    // some other game is a stranger here.
    expect(seenBy([], UserRole.Mentor).info).toBe(false);
  });

  it("opens the site's not found for a viewer it refuses, with no refusal drawn", async () => {
    const wrapper = render([GameParticipation.Player], UserRole.RegularUser);
    await flushPromises();

    expect(replace).toHaveBeenCalledWith(NOT_FOUND);
    expect(wrapper.text()).toBe("");
  });

  it("opens it for a guest too, with no invitation to sign in", async () => {
    const wrapper = render([]);
    await flushPromises();

    expect(replace).toHaveBeenCalledWith(NOT_FOUND);
    expect(wrapper.find(".login-prompt").exists()).toBe(false);
    expect(wrapper.text()).toBe("");
  });

  it("opens it for a guest whatever role his read of the game carries", async () => {
    // No arm of EditSettings admits a guest, so a role the guest's read of the
    // game names is no admission.
    const wrapper = render([GameParticipation.Moderator]);
    await flushPromises();

    expect(replace).toHaveBeenCalledWith(NOT_FOUND);
    expect(sectionsOf(wrapper).info).toBe(false);
  });

  it("keeps the master on his page", async () => {
    render([GameParticipation.Owner], UserRole.RegularUser);
    await flushPromises();

    expect(replace).not.toHaveBeenCalled();
  });

  it("waits for the answer about the session before sending anybody away", async () => {
    // A session the browser kept no copy of: the store holds nobody until the
    // server names the viewer.
    let answer = () => {};
    session.settled = new Promise((resolve) => (answer = resolve));
    render([]);
    await flushPromises();

    expect(replace).not.toHaveBeenCalled();

    answer();
    await flushPromises();
    expect(replace).toHaveBeenCalledWith(NOT_FOUND);
  });

  it("waits for the game read again for a new viewer", async () => {
    // The server names the master only now: the game was read for a guest, and
    // the shell reads it again for him. Until that answer lands the roles are
    // the guest's.
    let answer = () => {};
    session.settled = new Promise((resolve) => (answer = resolve));
    render([]);
    useAuthStore().user = { username: THE_VIEWER } as User;
    const store = useGameDetailsStore();
    store.gameLoading = true;
    answer();
    await flushPromises();

    expect(replace).not.toHaveBeenCalled();

    store.game = {
      ...store.game,
      participation: [GameParticipation.Owner],
    } as Game;
    store.gameLoading = false;
    await flushPromises();
    expect(replace).not.toHaveBeenCalled();
  });
});

describe("GameSettings sections", () => {
  it("gives the master every section", () => {
    expect(seenBy([GameParticipation.Owner])).toEqual({
      info: true,
      schema: true,
      rooms: true,
      roles: true,
      roleWrites: true,
      blacklist: true,
      invitations: true,
      invitationWrites: true,
      danger: true,
    });
  });

  it("gives the assistant the roster to read but not its controls", () => {
    // InviteAssistant and RemoveUser ask for GameRole.Master and admit nobody
    // else, so the section is drawn and its two controls are not.
    expect(seenBy([GameParticipation.Authority])).toEqual({
      info: true,
      schema: true,
      rooms: true,
      roles: true,
      roleWrites: false,
      blacklist: true,
      invitations: true,
      invitationWrites: true,
      danger: false,
    });
  });

  it("gives the curating mentor the settings the server lets him save", () => {
    // Rooms and the blacklist answer to Edit; the blacklist READ demands Edit
    // too, so that section goes away entirely rather than turning read-only.
    // The roster is a plain Read on the game, so the curator keeps it.
    expect(seenBy([GameParticipation.Moderator])).toEqual({
      info: true,
      schema: false,
      rooms: false,
      roles: true,
      roleWrites: false,
      blacklist: false,
      invitations: true,
      invitationWrites: false,
      danger: false,
    });
  });

  it("gives a senior moderator the Edit sections but no roster or invitation writes", () => {
    expect(seenBy([], UserRole.SeniorModerator)).toEqual({
      info: true,
      schema: true,
      rooms: true,
      roles: true,
      roleWrites: false,
      blacklist: true,
      invitations: true,
      invitationWrites: false,
      danger: false,
    });
  });
});

/**
 * Deleting the game. The confirmation runs the request itself: it waits for
 * the answer, prints a refusal in itself and closes on success alone, and the
 * request carries the flags that keep the interceptor quiet about it.
 */
describe("GameSettings — deleting the game", () => {
  const toast = useToast();
  const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

  beforeEach(() => {
    vi.clearAllMocks();
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  async function deleteGame(answer: GeneralError | null) {
    const wrapper = render([GameParticipation.Owner], UserRole.RegularUser);
    const remove = vi
      .spyOn(useGameDetailsStore(), "deleteGame")
      .mockResolvedValue(answer);
    await wrapper.find(".delete-game").trigger("click");
    await wrapper.find(".dialog-btn-submit").trigger("click");
    await flushPromises();
    return { wrapper, remove };
  }

  it("leaves for the list of games once the server agreed", async () => {
    const { wrapper, remove } = await deleteGame(null);

    expect(remove).toHaveBeenCalledWith({
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(push).toHaveBeenCalledWith({ name: "games" });
    expect(wrapper.find(".dialog-container").exists()).toBe(false);
  });

  it("keeps the master on the settings and names the refusal in the window", async () => {
    const { wrapper } = await deleteGame({
      status: 400,
      title: "",
    } as GeneralError);

    expect(push).not.toHaveBeenCalled();
    expect(wrapper.get(".dialog-refusal").text()).toBe(
      "Не удалось удалить игру",
    );
    expect(wrapper.find(".dialog-container").exists()).toBe(true);
    expect(saidOutLoud()).toEqual([]);
  });
});

/**
 * @vitest-environment jsdom
 */

/**
 * Each section of the settings stands for the viewers whose saves the server
 * accepts: EditSettings, Edit, the invitation writes and the roster writes. A
 * viewer admitted to none gets "not found", once the session is settled.
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

// Settled at once unless a test holds it.
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

// Render functions, not templates: they hold whatever Vue build the test runs.
const marker = (className: string) => ({
  setup: () => () => h("div", { class: className }),
});

const invitationsStub = defineComponent({
  props: { canManage: { type: Boolean, default: false } },
  setup: (props) => () =>
    h("div", {
      class: "invitations-section-stub",
      "data-can-manage": String(props.canManage),
    }),
});

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
  Teleport: true,
  RouterLink: { template: "<a><slot /></a>" },
};

const THE_VIEWER = "the-viewer";

function render(participation: GameParticipation[], viewer?: UserRole) {
  const pinia = createPinia();
  setActivePinia(pinia);
  // No site role at all is a guest.
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

  it.each([
    ["a player of the game", [GameParticipation.Player], undefined],
    ["a reader of the game", [GameParticipation.Reader], undefined],
    ["a passer-by", [], undefined],
    ["a junior moderator", [], UserRole.Moderator],
    // The curator of some other game is a stranger here.
    ["a site mentor who does not curate this game", [], UserRole.Mentor],
  ])("keeps the page away from %s", (_, participation, viewer) => {
    expect(seenBy(participation, viewer).info).toBe(false);
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
    // Read for a guest, the game is read again for the master the server named.
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

// The confirmation runs the request and prints its own refusal.
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

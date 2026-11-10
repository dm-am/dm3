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
import { flushPromises, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { GameParticipation, useGameDetailsStore } from "@/entities/game";
import { createMockGame } from "@/entities/game/model/store.fixtures";
import { asServed } from "@/shared/api/models";
import { mountPage } from "@/shared/lib/testing/mountPage";
import { useAuthStore, UserRole, type User } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import type { GeneralError } from "@/shared/api/models/common";
import GameSettings from "./GameSettings.vue";

const { session } = vi.hoisted(() => ({
  session: { settled: Promise.resolve() as Promise<void> },
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
};

const SETTINGS = "/game/the-game/settings";

const THE_VIEWER = "the-viewer";

/** The page, and the parts of the address it is at that NOT_FOUND names. */
async function render(participation: GameParticipation[], viewer?: UserRole) {
  const pinia = createPinia();
  setActivePinia(pinia);
  // No site role at all is a guest.
  useAuthStore().user = viewer
    ? ({ username: THE_VIEWER, role: viewer } as User)
    : null;
  const store = useGameDetailsStore();
  store.game = {
    ...createMockGame("the-game-guid", "Игра"),
    publicId: asServed("the-game"),
    participation: asServed(participation),
  };
  const { wrapper, router } = await mountPage(GameSettings, SETTINGS, {
    routes: [
      ["game-settings", "/game/:id/settings"],
      ["games", "/games"],
      ["not-found", "/:pathMatch(.*)*"],
    ],
    pinia,
    stubs,
  });
  const landed = () => {
    const { name, params, query, hash } = router.currentRoute.value;
    return { name, params, query, hash };
  };
  // Not found keeps the full address, so a stay is told by the route's name too.
  const stayed = () => {
    const { name, fullPath } = router.currentRoute.value;
    return { name, fullPath };
  };
  return {
    wrapper,
    landed,
    stayed,
    address: () => router.currentRoute.value.fullPath,
  };
}

const STAYED = { name: "game-settings", fullPath: SETTINGS };

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

const seenBy = async (
  participation: GameParticipation[],
  viewer: UserRole = UserRole.RegularUser,
) => sectionsOf((await render(participation, viewer)).wrapper);

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
  ])("keeps the page away from %s", async (_, participation, viewer) => {
    expect((await seenBy(participation, viewer)).info).toBe(false);
  });

  it("opens the site's not found for a viewer it refuses, with no refusal drawn", async () => {
    const { wrapper, landed } = await render(
      [GameParticipation.Player],
      UserRole.RegularUser,
    );
    await flushPromises();

    expect(landed()).toEqual(NOT_FOUND);
    expect(wrapper.text()).toBe("");
  });

  it("opens it for a guest too, with no invitation to sign in", async () => {
    const { wrapper, landed } = await render([]);
    await flushPromises();

    expect(landed()).toEqual(NOT_FOUND);
    expect(wrapper.find(".login-prompt").exists()).toBe(false);
    expect(wrapper.text()).toBe("");
  });

  it("opens it for a guest whatever role his read of the game carries", async () => {
    const { wrapper, landed } = await render([GameParticipation.Moderator]);
    await flushPromises();

    expect(landed()).toEqual(NOT_FOUND);
    expect(sectionsOf(wrapper).info).toBe(false);
  });

  it("keeps the master on his page", async () => {
    const { stayed } = await render(
      [GameParticipation.Owner],
      UserRole.RegularUser,
    );
    await flushPromises();

    expect(stayed()).toEqual(STAYED);
  });

  it("waits for the answer about the session before sending anybody away", async () => {
    let answer = () => {};
    session.settled = new Promise((resolve) => (answer = resolve));
    const { landed } = await render([]);
    await flushPromises();

    expect(landed().name).toBe("game-settings");

    answer();
    await flushPromises();
    expect(landed()).toEqual(NOT_FOUND);
  });

  it("waits for the game read again for a new viewer", async () => {
    // Read for a guest, the game is read again for the master the server named.
    let answer = () => {};
    session.settled = new Promise((resolve) => (answer = resolve));
    const { stayed } = await render([]);
    useAuthStore().user = { username: THE_VIEWER } as User;
    const store = useGameDetailsStore();
    store.gameLoading = true;
    answer();
    await flushPromises();

    expect(stayed()).toEqual(STAYED);

    store.game = {
      ...store.game!,
      participation: asServed([GameParticipation.Owner]),
    };
    store.gameLoading = false;
    await flushPromises();
    expect(stayed()).toEqual(STAYED);
  });
});

describe("GameSettings sections", () => {
  it("gives the master every section", async () => {
    expect(await seenBy([GameParticipation.Owner])).toEqual({
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

  it("gives the assistant the roster to read but not its controls", async () => {
    expect(await seenBy([GameParticipation.Authority])).toEqual({
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

  it("gives the curating mentor the settings the server lets him save", async () => {
    expect(await seenBy([GameParticipation.Moderator])).toEqual({
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

  it("gives a senior moderator the Edit sections but no roster or invitation writes", async () => {
    expect(await seenBy([], UserRole.SeniorModerator)).toEqual({
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
    const { wrapper, address } = await render(
      [GameParticipation.Owner],
      UserRole.RegularUser,
    );
    const remove = vi
      .spyOn(useGameDetailsStore(), "deleteGame")
      .mockResolvedValue(answer);
    await wrapper.find(".delete-game").trigger("click");
    await wrapper.find(".dialog-btn-submit").trigger("click");
    await flushPromises();
    return { wrapper, remove, address };
  }

  it("leaves for the list of games once the server agreed", async () => {
    const { wrapper, remove, address } = await deleteGame(null);

    expect(remove).toHaveBeenCalledWith({
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(address()).toBe("/games");
    expect(wrapper.find(".dialog-container").exists()).toBe(false);
  });

  it("keeps the master on the settings and names the refusal in the window", async () => {
    const { wrapper, address } = await deleteGame({
      status: 400,
      title: "",
    } as GeneralError);

    expect(address()).toBe(SETTINGS);
    expect(wrapper.get(".dialog-refusal").text()).toBe(
      "Не удалось удалить игру",
    );
    expect(wrapper.find(".dialog-container").exists()).toBe(true);
    expect(saidOutLoud()).toEqual([]);
  });
});

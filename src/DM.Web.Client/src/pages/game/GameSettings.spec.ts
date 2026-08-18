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
 *  - GameIntention.Edit (rooms, the blacklist including its read): leads and
 *    senior moderation, no curator;
 *  - InvitePlayer / InviteReader / CancelInvitation (the invitation writes):
 *    the game roles alone, without a rank clause, so a senior moderator reads
 *    that list without writing it;
 *  - InviteAssistant / RemoveUser (the roster writes): the master alone, while
 *    the roster itself costs a plain Read, so that section is drawn for
 *    everyone the page admits and only its controls narrow;
 *  - AttributeSchemaIntention.Edit: the schema's author and nobody else,
 *    because one public schema backs many games. A game with no schema gets no
 *    editor at all: nothing on this page can attach one.
 *
 * Every flag is read from the sources the neighbouring management screens read
 * (useGameDetailsStore.isMaster / isAssistant / isMentor plus the viewer's site
 * role) — never a check invented here.
 */
import { describe, expect, it, vi } from "vitest";
import { defineComponent, h } from "vue";
import { mount, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  GameParticipation,
  useGameDetailsStore,
  type AttributeSchema,
  type Character,
  type Game,
} from "@/entities/game";
import { useAuthStore, UserRole, type User } from "@/entities/user";
import GameSettings from "./GameSettings.vue";

// Only the route param and the navigation the delete action holds; the rest
// of the router stays real, as in the sibling roster spec.
vi.mock("vue-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-router")>()),
  useRoute: () => ({ params: { id: "the-game" } }),
  useRouter: () => ({ push: vi.fn() }),
}));

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
  AttributeSchemaEditor: marker("schema-editor-stub"),
  RoomsSection: marker("rooms-section-stub"),
  RolesSection: rolesStub,
  BlacklistSection: marker("blacklist-section-stub"),
  InvitationsSection: invitationsStub,
  UserLink: { template: "<span class='user-link-stub'><slot /></span>" },
  ConfirmDialog: true,
  RouterLink: { template: "<a><slot /></a>" },
};

const THE_VIEWER = "the-viewer";

/** A saved schema authored by `author`; null id means the game has none yet. */
const schemaBy = (author: string): AttributeSchema =>
  ({
    id: "the-schema",
    title: "Базовая",
    author: { username: author },
    specifications: [],
  }) as unknown as AttributeSchema;

function render(
  participation: GameParticipation[],
  viewer?: UserRole,
  schema: AttributeSchema | null = null,
) {
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
    schema,
  } as unknown as Game;
  // Seeded, so mounting the page does not reach for the endpoint.
  store.characters = [
    { id: "char-1", name: "Ронин", status: "Active" },
  ] as unknown as Character[];

  return mount(GameSettings, { global: { plugins: [pinia], stubs } });
}

/** What the viewer actually sees, section by section. */
const sectionsOf = (wrapper: VueWrapper) => {
  const invitations = wrapper.find(".invitations-section-stub");
  const roles = wrapper.find(".roles-section-stub");
  return {
    info: wrapper.find(".info-section-stub").exists(),
    schemaEditor: wrapper.find(".schema-editor-stub").exists(),
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
  schema: AttributeSchema | null = null,
) => sectionsOf(render(participation, viewer, schema));

describe("GameSettings admission", () => {
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

  it("names everyone who may enter when it refuses", () => {
    expect(
      render([GameParticipation.Player], UserRole.RegularUser).text(),
    ).toBe("Настройки игры доступны мастеру, ассистенту и наставнику игры.");
  });

  it("invites a guest to sign in instead of refusing an identity it never saw", () => {
    const wrapper = render([]);

    expect(wrapper.find(".login-prompt").exists()).toBe(true);
    expect(sectionsOf(wrapper).rooms).toBe(false);
  });
});

describe("GameSettings sections", () => {
  // Each viewer below authors the game's schema, so the `schemaEditor` cell
  // reports the page's own gate rather than the absence of a schema (that case
  // has its own test further down).
  const withOwnSchema = (
    participation: GameParticipation[],
    viewer: UserRole = UserRole.RegularUser,
  ) => seenBy(participation, viewer, schemaBy(THE_VIEWER));

  it("gives the master every section", () => {
    expect(withOwnSchema([GameParticipation.Owner])).toEqual({
      info: true,
      schemaEditor: true,
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
    expect(withOwnSchema([GameParticipation.Authority])).toEqual({
      info: true,
      schemaEditor: true,
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
    expect(withOwnSchema([GameParticipation.Moderator])).toEqual({
      info: true,
      schemaEditor: true,
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
    expect(withOwnSchema([], UserRole.SeniorModerator)).toEqual({
      info: true,
      schemaEditor: true,
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

describe("GameSettings attribute schema", () => {
  it("gives the editor to the schema's author", () => {
    expect(
      seenBy(
        [GameParticipation.Owner],
        UserRole.RegularUser,
        schemaBy(THE_VIEWER),
      ).schemaEditor,
    ).toBe(true);
  });

  it("withholds it from a master who is not the author", () => {
    // AttributeSchemaIntention.Edit knows nothing of game roles: one public
    // schema backs many games, so its author is the only editor.
    expect(
      seenBy(
        [GameParticipation.Owner],
        UserRole.RegularUser,
        schemaBy("someone-else"),
      ).schemaEditor,
    ).toBe(false);
  });

  it("says whose the schema is instead of drawing a form that would 403", () => {
    const wrapper = render(
      [GameParticipation.Owner],
      UserRole.RegularUser,
      schemaBy("someone-else"),
    );

    expect(wrapper.text()).toContain("Систему атрибутов правит ее автор");
    expect(wrapper.find(".user-link-stub").exists()).toBe(true);
  });

  it("withholds the editor from the master of a game that has no schema", () => {
    // POST /v1/schemas would be accepted, and nothing would attach the result
    // to this game: the update contract carries no schema field. Offering the
    // form here reported a save that changed nothing.
    expect(seenBy([GameParticipation.Owner]).schemaEditor).toBe(false);
  });

  it("says a schema-less game has none instead of offering a form that saves nothing", () => {
    const wrapper = render([GameParticipation.Owner], UserRole.RegularUser);

    expect(wrapper.text()).toContain("У игры нет системы атрибутов");
    expect(wrapper.find(".user-link-stub").exists()).toBe(false);
  });
});

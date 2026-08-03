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
 * The gate is participation, the single source the neighbouring management
 * screens read (useGameDetailsStore.isMaster / isAssistant, the pair behind
 * the sidebar's "Управление игрой" block) — never a check invented here. The
 * game mentor is deliberately outside it: canManage covers him for the
 * notepad and the roster, editing rooms it does not.
 */
import { describe, expect, it, vi } from "vitest";
import { h } from "vue";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  GameParticipation,
  useGameDetailsStore,
  type Character,
  type Game,
} from "@/entities/game";
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

const stubs = {
  GameInfoSection: marker("info-section-stub"),
  AttributeSchemaEditor: marker("schema-editor-stub"),
  RoomsSection: marker("rooms-section-stub"),
  RolesSection: marker("roles-section-stub"),
  BlacklistSection: marker("blacklist-section-stub"),
  InvitationsSection: marker("invitations-section-stub"),
  ConfirmDialog: true,
};

function render(participation: GameParticipation[]) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useGameDetailsStore();
  store.game = {
    id: "the-game-guid",
    publicId: "the-game",
    participation,
  } as unknown as Game;
  // Seeded, so mounting the page does not reach for the endpoint.
  store.characters = [
    { id: "char-1", name: "Ронин", status: "Active" },
  ] as unknown as Character[];

  return mount(GameSettings, { global: { plugins: [pinia], stubs } });
}

const hasRoomSettings = (participation: GameParticipation[]) =>
  render(participation).find(".rooms-section-stub").exists();

describe("GameSettings room management", () => {
  it("keeps room settings away from a player of the game", () => {
    expect(hasRoomSettings([GameParticipation.Player])).toBe(false);
  });

  it("keeps them away from a subscribed reader", () => {
    expect(hasRoomSettings([GameParticipation.Reader])).toBe(false);
  });

  it("keeps them away from a passer-by", () => {
    expect(hasRoomSettings([])).toBe(false);
  });

  it("keeps them away from the game mentor, who watches and does not edit", () => {
    expect(hasRoomSettings([GameParticipation.Moderator])).toBe(false);
  });

  it("says why the page is empty for everyone outside the gate", () => {
    expect(render([GameParticipation.Player]).text()).toContain(
      "Настройки игры доступны мастеру и ассистенту.",
    );
  });

  it("gives room settings to the master", () => {
    expect(hasRoomSettings([GameParticipation.Owner])).toBe(true);
  });

  it("gives them to the assistant", () => {
    expect(hasRoomSettings([GameParticipation.Authority])).toBe(true);
  });
});

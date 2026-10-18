/**
 * @vitest-environment jsdom
 */

/**
 * The game's part of its discussion: who may write in it, and what is said
 * under it to the one who may read and not write. The
 * section itself is shared and has its own spec; what is held here is what
 * this page hands it.
 *
 * Who may write is the game's answer about the viewer (viewer.canComment),
 * asked on the server by the code that refuses the comment: the mode with the
 * roles, the blacklist and the ban at once. The page offers the composer by it
 * and by nothing it works out itself, so a banned reader is no longer given a
 * form the server refuses. The blacklist flag still reaches every comment,
 * where it takes "Редактировать" off the viewer's own.
 *
 * Everybody who sees the game reads its discussion, whatever the mode. The
 * read-only mode is the server's (GameIntentionResolver): written in by
 * anybody with a role in the game and read by everybody. A discussion the
 * viewer may read and not write in stays whole, empty or not, with the plaque
 * of a refusal where the form would be, saying who writes there.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { defineComponent, h } from "vue";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  GameParticipation,
  useGameDetailsStore,
  type Game,
} from "@/entities/game";
import GameComments from "./GameComments.vue";

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { id: "the-game" }, query: {} }),
}));

/** The section, down to the props this page hands it. */
const handed = vi.hoisted(() => ({
  props: {} as Record<string, unknown>,
}));
vi.mock("@/widgets/discussion", () => ({
  DiscussionSection: defineComponent({
    props: ["canComment", "closedHint", "viewerBlacklisted"],
    setup(props) {
      return () => {
        handed.props = { ...props };
        return h("div", { class: "discussion-stub" });
      };
    },
  }),
}));

const READ_ONLY = "Писать в обсуждении могут только участники игры";
const BLACKLISTED = "Вы в черном списке этой игры: писать в обсуждении нельзя";

function render(game: Partial<Record<string, unknown>> = {}) {
  setActivePinia(createPinia());
  const store = useGameDetailsStore();
  store.game = {
    id: "the-game-guid",
    publicId: "the-game",
    participation: [],
    privacySettings: { commentariesAccess: "Public" },
    viewer: { canComment: true },
    ...game,
  } as unknown as Game;
  return mount(GameComments);
}

describe("GameComments", () => {
  beforeEach(() => {
    localStorage.clear();
    // Signed in: a guest is never on a blacklist and gets the sign-in prompt.
    localStorage.setItem("user", JSON.stringify({ username: "Reader" }));
    handed.props = {};
  });

  it("offers the composer to the reader the game takes a comment from", () => {
    render();

    expect(handed.props).toEqual({
      canComment: true,
      closedHint: undefined,
      viewerBlacklisted: false,
    });
  });

  // A ban is nothing the page can see in the game, and the mode is public:
  // the answer of the server is the only thing that says no, and it is
  // enough. The page used to offer the form here and print the refusal under
  // it after the text was typed.
  it("offers nothing where the game takes no comment from the reader", () => {
    render({ viewer: { canComment: false } });

    expect(handed.props).toEqual({
      canComment: false,
      closedHint: undefined,
      viewerBlacklisted: false,
    });
  });

  // An answer that did not come is no "yes".
  it("offers nothing on a game that says nothing about the viewer", () => {
    render({ viewer: undefined });

    expect(handed.props.canComment).toBe(false);
  });

  // The one on the list reads and never writes: the discussion stays, empty
  // or not, and the line under it says why there is no form.
  it("says the blacklist under the discussion and tells the comments", () => {
    render({ viewerBlacklisted: true, viewer: { canComment: false } });

    expect(handed.props).toEqual({
      canComment: false,
      closedHint: BLACKLISTED,
      viewerBlacklisted: true,
    });
  });

  // Read-only is closed to the writing of those outside the game, and the
  // section is told so: no composer, the plaque in its place.
  it("says a read-only discussion is read-only to somebody outside the game", () => {
    render({
      privacySettings: { commentariesAccess: "Readonly" },
      viewer: { canComment: false },
    });

    expect(handed.props).toEqual({
      canComment: false,
      closedHint: READ_ONLY,
      viewerBlacklisted: false,
    });
  });

  // The mode closes it to everybody outside the game, the list or not: one
  // line, the one that holds for everybody.
  it("names the read-only mode before the blacklist", () => {
    render({
      privacySettings: { commentariesAccess: "Readonly" },
      viewerBlacklisted: true,
      viewer: { canComment: false },
    });

    expect(handed.props.closedHint).toBe(READ_ONLY);
  });

  // The server takes a comment in a read-only game from anybody with a role
  // in it, and the page offered none: it closed the mode to everyone. An
  // assistant is a role too, the one the page used to forget.
  it.each([
    ["a player", [GameParticipation.Player]],
    ["a reader", [GameParticipation.Reader]],
    ["an assistant", [GameParticipation.Authority]],
    ["the mentor", [GameParticipation.Moderator]],
    ["the master", [GameParticipation.Owner, GameParticipation.Authority]],
  ])("does not close a read-only discussion to %s", (_, participation) => {
    render({
      participation,
      privacySettings: { commentariesAccess: "Readonly" },
    });

    expect(handed.props.canComment).toBe(true);
    expect(handed.props.closedHint).toBeUndefined();
  });

  it("does not take a guest for somebody on the blacklist", () => {
    localStorage.clear();
    render({ viewerBlacklisted: true, viewer: { canComment: false } });

    expect(handed.props.viewerBlacklisted).toBe(false);
    expect(handed.props.closedHint).toBeUndefined();
  });

  // The answer is for whoever held the session when the game was read. Until
  // the shell reads the game again, a viewer whose session has just ended is
  // a guest and is offered no form on somebody else's "yes".
  it("offers no composer to a guest on an answer given to a session that ended", () => {
    localStorage.clear();
    render({ viewer: { canComment: true } });

    expect(handed.props.canComment).toBe(false);
    expect(handed.props.closedHint).toBeUndefined();
  });

  // A guest's roles are none, whatever the game was read with last, and the
  // line goes before the sign-in prompt: signing in would not open the form
  // to most of those who read it.
  it("says a read-only discussion is read-only to a guest", () => {
    localStorage.clear();
    render({
      participation: [GameParticipation.Player],
      privacySettings: { commentariesAccess: "Readonly" },
    });

    expect(handed.props.canComment).toBe(false);
    expect(handed.props.closedHint).toBe(READ_ONLY);
  });

  // No mode closes the reading: somebody outside the game reads its
  // discussion in either, and a guest is not told anything in place of it.
  it.each(["Public", "Readonly"])(
    "draws the discussion for somebody outside a game in the %s mode",
    (mode) => {
      const wrapper = render({
        privacySettings: { commentariesAccess: mode },
        viewer: { canComment: false },
      });
      expect(wrapper.find(".discussion-stub").exists()).toBe(true);
      expect(wrapper.text()).not.toContain("участникам игры");

      localStorage.clear();
      const asGuest = render({ privacySettings: { commentariesAccess: mode } });
      expect(asGuest.find(".discussion-stub").exists()).toBe(true);
    },
  );
});

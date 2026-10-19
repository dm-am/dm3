/**
 * @vitest-environment jsdom
 */

// What the game's page hands the shared discussion: who writes, and why not.
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

  // A ban the page cannot see: the server's answer alone says no.
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

  it("says the blacklist under the discussion and tells the comments", () => {
    render({ viewerBlacklisted: true, viewer: { canComment: false } });

    expect(handed.props).toEqual({
      canComment: false,
      closedHint: BLACKLISTED,
      viewerBlacklisted: true,
    });
  });

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

  it("names the read-only mode before the blacklist", () => {
    render({
      privacySettings: { commentariesAccess: "Readonly" },
      viewerBlacklisted: true,
      viewer: { canComment: false },
    });

    expect(handed.props.closedHint).toBe(READ_ONLY);
  });

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

  // The answer was given to a session that has since ended.
  it("offers no composer to a guest on an answer given to a session that ended", () => {
    localStorage.clear();
    render({ viewer: { canComment: true } });

    expect(handed.props.canComment).toBe(false);
    expect(handed.props.closedHint).toBeUndefined();
  });

  it("says a read-only discussion is read-only to a guest", () => {
    localStorage.clear();
    render({
      participation: [GameParticipation.Player],
      privacySettings: { commentariesAccess: "Readonly" },
    });

    expect(handed.props.canComment).toBe(false);
    expect(handed.props.closedHint).toBe(READ_ONLY);
  });

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

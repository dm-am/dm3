/**
 * @vitest-environment jsdom
 */

// What the game's page hands the shared discussion: who writes, and why not.
import { describe, it, expect, vi, beforeEach } from "vitest";
import { defineComponent, h } from "vue";
import { createPinia, setActivePinia } from "pinia";
import {
  CommentariesAccessMode,
  GameParticipation,
  useGameDetailsStore,
} from "@/entities/game";
import { createMockGame } from "@/entities/game/model/store.fixtures";
import { asServed } from "@/shared/api/models";
import { mountPage } from "@/shared/lib/testing/mountPage";
import GameComments from "./GameComments.vue";

/** The section, down to the props this page hands it. */
const handed = vi.hoisted(() => ({
  props: {} as Record<string, unknown>,
  recordId: undefined as unknown,
}));
vi.mock("@/widgets/discussion", () => ({
  DiscussionSection: defineComponent({
    props: ["canComment", "closedHint", "viewerBlacklisted", "recordId"],
    setup(props) {
      return () => {
        const { recordId, ...rest } = props;
        handed.props = rest;
        handed.recordId = recordId;
        return h("div", { class: "discussion-stub" });
      };
    },
  }),
}));

const READ_ONLY = "Писать в обсуждении могут только участники игры";
const BLACKLISTED = "Вы в черном списке этой игры: писать в обсуждении нельзя";

/** What the server says of the game and its viewer; a null viewer is left unsaid. */
interface Answer {
  participation?: GameParticipation[];
  commentariesAccess?: CommentariesAccessMode;
  viewer?: { canComment: boolean } | null;
  viewerBlacklisted?: boolean;
}

async function render({
  participation = [],
  commentariesAccess = CommentariesAccessMode.Public,
  viewer = { canComment: true },
  viewerBlacklisted,
}: Answer = {}) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const game = createMockGame("the-game-guid", "Игра");
  useGameDetailsStore().game = {
    ...game,
    publicId: asServed("the-game"),
    participation: asServed(participation),
    privacySettings: { ...game.privacySettings, commentariesAccess },
    viewer: viewer ? asServed({ ...viewer, canReview: false }) : undefined,
    viewerBlacklisted:
      viewerBlacklisted === undefined ? undefined : asServed(viewerBlacklisted),
  };
  const { wrapper } = await mountPage(GameComments, "/game/the-game/comments", {
    routes: [["game-comments", "/game/:id/comments"]],
    pinia,
  });
  return wrapper;
}

describe("GameComments", () => {
  beforeEach(() => {
    localStorage.clear();
    // Signed in: a guest is never on a blacklist and gets the sign-in prompt.
    localStorage.setItem("user", JSON.stringify({ username: "Reader" }));
    handed.props = {};
  });

  it("offers the composer to the reader the game takes a comment from", async () => {
    await render();

    expect(handed.props).toEqual({
      canComment: true,
      closedHint: undefined,
      viewerBlacklisted: false,
    });
    // The discussion of the game the address names.
    expect(handed.recordId).toBe("the-game");
  });

  // A ban the page cannot see: the server's answer alone says no.
  it("offers nothing where the game takes no comment from the reader", async () => {
    await render({ viewer: { canComment: false } });

    expect(handed.props).toEqual({
      canComment: false,
      closedHint: undefined,
      viewerBlacklisted: false,
    });
  });

  // An answer that did not come is no "yes".
  it("offers nothing on a game that says nothing about the viewer", async () => {
    await render({ viewer: null });

    expect(handed.props.canComment).toBe(false);
  });

  it("says the blacklist under the discussion and tells the comments", async () => {
    await render({ viewerBlacklisted: true, viewer: { canComment: false } });

    expect(handed.props).toEqual({
      canComment: false,
      closedHint: BLACKLISTED,
      viewerBlacklisted: true,
    });
  });

  it("says a read-only discussion is read-only to somebody outside the game", async () => {
    await render({
      commentariesAccess: CommentariesAccessMode.Readonly,
      viewer: { canComment: false },
    });

    expect(handed.props).toEqual({
      canComment: false,
      closedHint: READ_ONLY,
      viewerBlacklisted: false,
    });
  });

  it("names the read-only mode before the blacklist", async () => {
    await render({
      commentariesAccess: CommentariesAccessMode.Readonly,
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
  ])(
    "does not close a read-only discussion to %s",
    async (_, participation) => {
      await render({
        participation,
        commentariesAccess: CommentariesAccessMode.Readonly,
      });

      expect(handed.props.canComment).toBe(true);
      expect(handed.props.closedHint).toBeUndefined();
    },
  );

  it("does not take a guest for somebody on the blacklist", async () => {
    localStorage.clear();
    await render({ viewerBlacklisted: true, viewer: { canComment: false } });

    expect(handed.props.viewerBlacklisted).toBe(false);
    expect(handed.props.closedHint).toBeUndefined();
  });

  // The answer was given to a session that has since ended.
  it("offers no composer to a guest on an answer given to a session that ended", async () => {
    localStorage.clear();
    await render({ viewer: { canComment: true } });

    expect(handed.props.canComment).toBe(false);
    expect(handed.props.closedHint).toBeUndefined();
  });

  it("says a read-only discussion is read-only to a guest", async () => {
    localStorage.clear();
    await render({
      participation: [GameParticipation.Player],
      commentariesAccess: CommentariesAccessMode.Readonly,
    });

    expect(handed.props.canComment).toBe(false);
    expect(handed.props.closedHint).toBe(READ_ONLY);
  });

  it.each([CommentariesAccessMode.Public, CommentariesAccessMode.Readonly])(
    "draws the discussion for somebody outside a game in the %s mode",
    async (mode) => {
      const wrapper = await render({
        commentariesAccess: mode,
        viewer: { canComment: false },
      });
      expect(wrapper.find(".discussion-stub").exists()).toBe(true);
      expect(wrapper.text()).not.toContain("участникам игры");

      localStorage.clear();
      const asGuest = await render({ commentariesAccess: mode });
      expect(asGuest.find(".discussion-stub").exists()).toBe(true);
    },
  );
});

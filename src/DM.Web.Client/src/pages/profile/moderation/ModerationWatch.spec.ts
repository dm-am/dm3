/**
 * @vitest-environment jsdom
 */

/**
 * The moderation watch is the one thing on a profile that decides what happens
 * to content the user has not created yet: while it is on, every game and blog
 * they start is born in premoderation.
 *
 * Two rules are pinned here, and they are the two the client can get wrong on
 * its own. The block is drawn only for the rank the endpoint accepts
 * (ModerationIntention.SetModerationWatch is Moderator+, while the panel around
 * this block opens to a wider audience) — a switch the server answers 403 to is
 * worse than no switch. And the button asks for the opposite of the state it is
 * looking at, because a toggle that sends its own current value silently does
 * nothing.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount } from "@vue/test-utils";
import type { ModerationPermissions } from "@/shared/api/models/moderation";
import ModerationWatch from "./ModerationWatch.vue";

const setModerationWatch = vi.fn<
  (username: string, underWatch: boolean) => Promise<{ error: unknown }>
>(() => Promise.resolve({ error: null }));

vi.mock("@/entities/moderation", () => ({
  moderationApi: {
    setModerationWatch: (username: string, underWatch: boolean) =>
      setModerationWatch(username, underWatch),
  },
}));

const notifyFailure = vi.fn();
vi.mock("@/shared/lib/errors", () => ({
  notifyFailure: (error: unknown, fallback: string) =>
    notifyFailure(error, fallback),
}));

function permissions(canSetModerationWatch: boolean): ModerationPermissions {
  return {
    canViewEmail: true,
    canViewIpAddresses: true,
    canViewLoginHistory: true,
    canViewLinkedProfiles: true,
    canViewModNotes: true,
    canCreateModNote: true,
    canIssueWarning: true,
    canIssueBan: true,
    canLiftBan: true,
    canSetModerationWatch,
  };
}

function mountBlock(underWatch: boolean, canSet = true) {
  return mount(ModerationWatch, {
    props: {
      underWatch,
      permissions: permissions(canSet),
      targetUsername: "Новичок",
    },
  });
}

describe("ModerationWatch", () => {
  beforeEach(() => {
    setModerationWatch.mockClear();
    notifyFailure.mockClear();
  });

  it("draws nothing for a viewer the endpoint would refuse", () => {
    expect(mountBlock(false, false).find(".mod-section").exists()).toBe(false);
  });

  it("offers to switch the watch on when it is off", () => {
    const wrapper = mountBlock(false);

    expect(wrapper.text()).toContain("Пользователь не под наблюдением");
    expect(wrapper.find("button").text()).toBe("Взять под наблюдение");
  });

  it("offers to clear the watch when it is on", () => {
    const wrapper = mountBlock(true);

    expect(wrapper.text()).toContain("под наблюдением");
    expect(wrapper.find("button").text()).toBe("Снять наблюдение");
  });

  // The word the domain uses for this audience stays in the domain.
  it("keeps the domain's word for the audience out of the interface", () => {
    expect(mountBlock(true).text()).not.toContain("рецидив");
  });

  it.each([
    [false, true],
    [true, false],
  ])("sends the opposite of the state it shows (%s)", async (state, sent) => {
    const wrapper = mountBlock(state);
    await wrapper.find("button").trigger("click");

    expect(setModerationWatch).toHaveBeenCalledWith("Новичок", sent);
  });

  it("asks the parent to refetch once the switch lands", async () => {
    const wrapper = mountBlock(false);
    await wrapper.find("button").trigger("click");
    await Promise.resolve();

    expect(wrapper.emitted("updated")).toHaveLength(1);
  });

  it("says what the server said and leaves the panel alone on failure", async () => {
    setModerationWatch.mockResolvedValueOnce({
      error: { message: "нет" },
    } as unknown as { error: null });
    const wrapper = mountBlock(false);
    await wrapper.find("button").trigger("click");
    await Promise.resolve();

    expect(notifyFailure).toHaveBeenCalled();
    expect(wrapper.emitted("updated")).toBeUndefined();
  });
});

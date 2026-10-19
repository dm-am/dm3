/**
 * @vitest-environment jsdom
 */

/**
 * The moderation watch: drawn only for the rank the endpoint accepts, a box
 * that sends the opposite of the state it shows, saved on the press.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
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
    // A box fires its change only while it stands in a document.
    attachTo: document.body,
  });
}

enableAutoUnmount(afterEach);

/** Presses the box the way a reader does: a click, which toggles it. */
async function press(wrapper: ReturnType<typeof mountBlock>) {
  (wrapper.get("input").element as HTMLInputElement).click();
  await flushPromises();
}

const box = (wrapper: ReturnType<typeof mountBlock>) =>
  wrapper.get("input").element as HTMLInputElement;

describe("ModerationWatch", () => {
  beforeEach(() => {
    setModerationWatch.mockClear();
  });

  it("draws nothing for a viewer the endpoint would refuse", () => {
    expect(mountBlock(false, false).find("section").exists()).toBe(false);
  });

  // A switch, so a box: its tick is the state, its caption what the state does.
  it("is one box that says what the watch does, ticked while it is on", () => {
    const off = mountBlock(false);
    expect(off.get("h2").text()).toBe("Наблюдение");
    expect(off.get("label").text()).toBe(
      "Новые игры и блоги пользователя начинают с премодерации",
    );
    expect(box(off).checked).toBe(false);
    expect(off.find("button").exists()).toBe(false);

    expect(box(mountBlock(true)).checked).toBe(true);
  });

  it.each([
    [false, true],
    [true, false],
  ])("sends the opposite of the state it shows (%s)", async (state, sent) => {
    const wrapper = mountBlock(state);
    await press(wrapper);

    expect(setModerationWatch).toHaveBeenCalledWith("Новичок", sent);
    expect(box(wrapper).checked).toBe(sent);
  });

  it("asks the parent to refetch once the switch lands", async () => {
    const wrapper = mountBlock(false);
    await press(wrapper);

    expect(wrapper.emitted("updated")).toHaveLength(1);
  });

  /**
   * While the save is out the box stays drawn as pressed, and a second press
   * is held: it neither flips the box back nor becomes a second request.
   */
  it("holds a press made while the save is out", async () => {
    let answer!: (value: { error: null }) => void;
    setModerationWatch.mockReturnValueOnce(
      new Promise((yes) => (answer = yes)),
    );
    const wrapper = mountBlock(false);
    await press(wrapper);
    await press(wrapper);

    expect(setModerationWatch).toHaveBeenCalledTimes(1);
    expect(box(wrapper).checked).toBe(true);
    answer({ error: null });
    await flushPromises();
  });

  // Under the box, where the press was.
  it("puts the box back and says why under it when the server refuses", async () => {
    setModerationWatch.mockResolvedValueOnce({
      error: { type: "", title: "", status: 404, traceId: "t" },
    } as unknown as { error: null });
    const wrapper = mountBlock(false);
    await press(wrapper);

    expect(box(wrapper).checked).toBe(false);
    expect(wrapper.get(".action-result").text()).toBe(
      "Не удалось взять пользователя под наблюдение",
    );
    expect(wrapper.emitted("updated")).toBeUndefined();
  });
});

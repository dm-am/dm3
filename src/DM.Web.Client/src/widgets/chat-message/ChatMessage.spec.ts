/**
 * @vitest-environment jsdom
 */

/** The time leads its group in every layout and state of a message. */
import { describe, it, expect } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import ChatMessage from "./ChatMessage.vue";

/**
 * The time groups a row draws, each as the classes of its parts in order, for
 * a message in one of its states: removed (a moderator sees it folded or
 * opened), a continuation of the one above, or a whole one.
 */
function timeGroupsOf(
  state: "removed" | "opened" | "continuation" | "whole",
  isCompact: boolean,
): string[][] {
  const wrapper = mount(ChatMessage, {
    props: {
      message: {
        id: "m1",
        createdUtc: "2026-10-05T07:38:00Z",
        author: { username: "anna" },
        text: "<p>Привет</p>",
        isContinuation: state === "continuation",
        isRemoved: state === "removed" || state === "opened",
        edits: [],
        likes: [],
      } as never,
      compact: isCompact,
      maxHeight: 500,
      isModerator: true,
      isDeletedExpanded: state === "opened",
    },
    global: { stubs: { RouterLink: RouterLinkStub } },
  });
  const groups = wrapper
    .findAll(".msg-time-group")
    .map((group) => [...group.element.children].map((part) => part.className));
  wrapper.unmount();
  return groups;
}

describe("the compact chat row starts where the full row starts", () => {
  it.each([
    ["removed", true, 1],
    ["opened", true, 1],
    ["continuation", true, 1],
    ["whole", true, 1],
    // The full layout folds a removed message into a line of its own.
    ["removed", false, 0],
    ["opened", false, 1],
    ["continuation", false, 1],
    ["whole", false, 1],
  ] as const)(
    "leads the time group of a %s message with the time itself, compact %s",
    (state, isCompact, drawn) => {
      const groups = timeGroupsOf(state, isCompact);

      expect(groups).toHaveLength(drawn);
      for (const parts of groups) expect(parts[0]).toBe("msg-time");
    },
  );
});

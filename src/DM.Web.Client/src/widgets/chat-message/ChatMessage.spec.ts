/**
 * @vitest-environment jsdom
 */

/**
 * The compact layout used to start each row 46px in from the left edge while
 * the full layout starts its avatar at zero: the 80px time gutter was
 * right-aligned and led with a 16px icon slot, so everything in it was pressed
 * against the right edge and the hole stayed on the left, in the chat, in the
 * messenger and in game rooms alike.
 *
 * The cure is one order and one alignment: the time comes first in its group
 * everywhere — as it always did in the full layout — and the compact gutter is
 * left-aligned, which puts the time on the exact x where the full layout puts
 * the avatar. The gutter stays 80px and the content margin stays 88px, so the
 * column the two layouts share does not move.
 *
 * The order is read off the row as it is drawn, in each layout a message has.
 * The alignment is read from the scoped style block, which no rendering here
 * lays out.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { parse } from "vue/compiler-sfc";
import { mount, RouterLinkStub } from "@vue/test-utils";
import ChatMessage from "./ChatMessage.vue";

const HERE = dirname(fileURLToPath(import.meta.url));
const FILE = join(HERE, "ChatMessage.vue");

const source = readFileSync(FILE, "utf8");
const { descriptor } = parse(source, { filename: "ChatMessage.vue" });

/**
 * One rule of an indented stylesheet: every line indented under the selector,
 * up to the next line that starts at column zero.
 */
function ruleBody(style: string, selector: string): string {
  const lines = style.split("\n");
  const start = lines.findIndex((line) => line.trim() === selector);
  if (start < 0) return "";
  const body: string[] = [];
  for (let i = start + 1; i < lines.length; i++) {
    if (lines[i].trim() === "") continue;
    if (!/^\s/.test(lines[i])) break;
    body.push(lines[i]);
  }
  return body.join("\n");
}

const compact = ruleBody(
  descriptor.styles[0]?.content ?? "",
  ".chat-message.compact",
);

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
  // A selector line of its own: the block also names the group in a comment,
  // which a rule taken out would leave behind.
  it("reads the compact rule of the time group", () => {
    expect(compact).toMatch(/^\s+\.msg-time-group\s*$/m);
  });

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

  it("aligns the compact gutter to the left, never to the right", () => {
    expect(compact).toMatch(/text-align:\s*left/);
    expect(compact).not.toMatch(/text-align:\s*right/);
  });
});

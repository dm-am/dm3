/**
 * @vitest-environment node
 */

/**
 * The global chat and a private correspondence are drawn with one feed,
 * widgets/chat-feed, and neither keeps a copy of a part of it.
 *
 * Both pages draw ChatFeed, with ChatComposer under its frame, their line in
 * ChatStrip at its top (the events, who the correspondence is with) and
 * ChatSearchRow (above the global chat's frame, under the correspondence's
 * line), and neither draws a part of a feed of its own: no rows, no list, no
 * toolbar, no composer, no search row, no calendar, no fold height, no rule on
 * a feed's class. A copy of a part grows apart from the original one edit at a
 * time, in places a reader sees: the paddings, the date separators, the
 * height a long message folds at, what stands there while the feed loads.
 * Which part of the history the feed shows (the day in the address, the way
 * back to the latest, the line over a window nobody asked for) is
 * useChatHistoryWindow's.
 *
 * Read off the sources: a second feed is a matter of what a page imports and
 * writes, and the screen is only where it shows, much later.
 */
import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { parse } from "vue/compiler-sfc";

const CLIENT_SRC = dirname(fileURLToPath(import.meta.url));

/** The pages drawn with the feed. */
const PAGES = [
  "pages/global-chat/GlobalChatPage.vue",
  "pages/messenger/ChatView.vue",
];

/** The lines at the top of a frame, and their dress. */
const LINES = [
  "pages/global-chat/ChatEventsPanel.vue",
  "pages/messenger/ConversationLine.vue",
];
const STRIP = "widgets/chat-feed/ChatStrip.vue";

/** The feed. */
const FEED = "widgets/chat-feed/ChatFeed.vue";

/**
 * What only the feed draws or holds. A page that reaches for one of these is
 * drawing a part of a feed of its own.
 */
const FEED_PARTS = [
  "@/widgets/chat-message",
  "useVirtualScroll",
  "useMessageToolbar",
  "useChatComposer",
  "@/shared/ui/BBCodeEditor",
  "@/shared/ui/DashSeparator",
  "@/shared/ui/Skeleton",
  "MessageToolbar",
  "@/features/message-search",
  "@/features/chat-date-jump",
];

/** A rule on a class of a feed, the feed's own or one a page used to keep. */
const FEED_SELECTOR =
  /\.(chat-feed|chat-composer|msg-|toolbar-btn|scroll-to-latest|date-separator|pm-message|globalChat-|messages-container|input-wrapper)/;

function partsOf(file: string) {
  const source = readFileSync(resolve(CLIENT_SRC, file), "utf8");
  const { descriptor } = parse(source, { filename: file });
  return {
    script: [descriptor.script?.content, descriptor.scriptSetup?.content]
      .filter(Boolean)
      .join("\n"),
    template: descriptor.template?.content ?? "",
    style: descriptor.styles.map((block) => block.content).join("\n"),
  };
}

/**
 * The top-level rules of an indented-Sass block that keep their text on one
 * line, by selector.
 */
function nowrapBlocks(style: string): string[] {
  const found: string[] = [];
  let selector = "";
  for (const line of style.split("\n")) {
    if (/^[.#&\w]/.test(line)) selector = line.trim();
    else if (/white-space:\s*nowrap/.test(line) && !found.includes(selector))
      found.push(selector);
  }
  return found;
}

describe("the feed of the two chats", () => {
  it.each(PAGES)("%s is drawn with the one feed", (page) => {
    const { script, template } = partsOf(page);
    expect(script).toMatch(/from "@\/widgets\/chat-feed"/);
    expect(template).toMatch(/<ChatFeed\b/);
  });

  it.each(PAGES)(
    "%s stands under the one search row and the one history window",
    (page) => {
      const { script, template } = partsOf(page);
      expect(template).toMatch(/<ChatSearchRow\b/);
      expect(script).toMatch(/\buseChatHistoryWindow\(/);
      // The day in the address is the history window's to read and to move.
      expect(script).not.toMatch(/route\.query\.date/);
    },
  );

  it.each(PAGES)("%s draws no part of a feed of its own", (page) => {
    const { script, style } = partsOf(page);
    const own = FEED_PARTS.filter(
      (part) => script.includes(part) || style.includes(part),
    );
    expect(own).toEqual([]);
  });

  it.each(PAGES)("%s puts no rule on a feed's class", (page) => {
    const rules = partsOf(page)
      .style.split("\n")
      .filter((line) => FEED_SELECTOR.test(line));
    expect(rules).toEqual([]);
  });

  it.each(PAGES)("%s keeps no fold height of its own", (page) => {
    const { script, template } = partsOf(page);
    expect(script).not.toMatch(/MAX_MESSAGE_HEIGHT|maxHeight/);
    expect(template).not.toMatch(/max-height/);
  });

  it("folds every message of the feed at one height", () => {
    expect(partsOf(FEED).template).toMatch(/:max-height="MESSAGE_FOLD_HEIGHT"/);
  });

  /**
   * A line at the top of the frame says every word on every width. Where the
   * frame is narrower than it, it wraps like any text: neither the line nor
   * its dress keeps it on one line, and nothing of it is cut under "…" or
   * scrolled sideways. Its surface and dashed hand-over are ChatStrip's.
   */
  it.each(LINES)(
    "%s draws its line in ChatStrip, wrapping like text",
    (file) => {
      const { script, template, style } = partsOf(file);
      expect(script).toMatch(/from "@\/widgets\/chat-feed"/);
      expect(template).toMatch(/<ChatStrip\b/);
      expect(template).not.toMatch(/<FitRow\b|<ScrollRow\b/);
      expect(template).not.toMatch(/data-fit-/);
      expect(style).not.toMatch(/border-bottom:\s*1px dashed/);
      // A link of the line ("описание", "поиск") is a button and
      // keeps its words together; the line around the links wraps.
      expect(nowrapBlocks(style)).toEqual([".strip-item"]);
    },
  );

  it("dresses the strip without holding it to one line", () => {
    const { style } = partsOf(STRIP);
    expect(style).toMatch(/border-bottom:\s*1px dashed/);
    expect(style).not.toMatch(/white-space:\s*nowrap/);
  });

  it("draws the composer only for a reader who may write", () => {
    expect(partsOf(FEED).template).toMatch(
      /<ChatComposer\s[^>]*v-if="canWrite"/,
    );
  });
});

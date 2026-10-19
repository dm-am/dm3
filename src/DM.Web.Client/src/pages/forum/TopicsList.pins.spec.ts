/**
 * @vitest-environment jsdom
 */

// The pin before every title is the moderator's toggle, as a like is; a
// refused pin is said under its row. The order is pinnedOrder.spec's.
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { mockUpdateTopic, mockReorderPinnedTopics } from "./TopicsList.doubles";
import {
  refusal,
  topic,
  RULES,
  NEWS,
  PLAIN,
  pinned,
  unpinned,
  answerBoard,
  freshPage,
  signIn,
  messages,
  openBoard,
  rows,
  rowIds,
  pointer,
  layRows,
  moderatedBoard,
  pinOf,
  click,
  tipOf,
} from "./TopicsList.fixtures";

// The network only: the router, the store, the table and the tooltips are the real ones.
vi.mock("@/entities/forum/api/forumApi", async () =>
  (await import("./TopicsList.doubles")).forumApi(),
);

enableAutoUnmount(afterEach);

describe("TopicsList moderator actions", () => {
  beforeEach(() => {
    moderatedBoard();
    mockUpdateTopic.mockResolvedValue({ data: null, error: null });
  });

  // Under the row whose pin was pressed, not in a plate.
  it("names the pin action the server refused under its row, in the direction it was going", async () => {
    mockUpdateTopic.mockResolvedValue({ data: null, error: refusal(409) });
    const wrapper = await openBoard();
    const refusalUnder = () =>
      wrapper
        .findAll("tr.table-details-row")
        .map((row) => [
          row.element.previousElementSibling
            ?.querySelector("[data-pin-toggle]")
            ?.getAttribute("data-pin-toggle"),
          row.get(".pin-refusal").text(),
        ]);

    // The two pinned rows lead, the regular one follows.
    await wrapper.findAll(".pin-toggle")[0].trigger("click");
    await flushPromises();
    expect(refusalUnder()).toEqual([["t-rules", "Не удалось открепить топик"]]);

    await wrapper.findAll(".pin-toggle")[2].trigger("click");
    await flushPromises();
    expect(refusalUnder()).toEqual([["t-plain", "Не удалось закрепить топик"]]);
    expect(messages()).toEqual([]);
  });

  // Every title has its pin, named by its action with its state in aria-pressed;
  // on a pinned topic also the handle of the order.
  it("puts a pin before every title", async () => {
    const wrapper = await openBoard();

    const pins = [];
    for (const row of rows(wrapper)) {
      const pin = row.get(".col-title button.pin-toggle");
      pins.push([
        pin.attributes("data-pin-toggle"),
        pin.attributes("aria-label"),
        pin.attributes("aria-pressed"),
        pin.classes("pinned"),
        pin.attributes("data-pin-handle") ?? null,
        await tipOf(pin),
      ]);
    }
    expect(pins).toEqual([
      [
        "t-rules",
        "Закрепить топик Правила",
        "true",
        true,
        "t-rules",
        "Открепить топик. Перетащите, чтобы изменить порядок",
      ],
      [
        "t-news",
        "Закрепить топик Объявления",
        "true",
        true,
        "t-news",
        "Открепить топик. Перетащите, чтобы изменить порядок",
      ],
      [
        "t-plain",
        "Закрепить топик Болталка",
        "false",
        false,
        null,
        "Закрепить топик",
      ],
    ]);
    // The pin is the mark: the link of a title holds no second one.
    for (const row of rows(wrapper)) {
      expect(row.findAll(".col-title .topic-icon")).toHaveLength(1);
      expect(row.find(".col-title .topic-link .topic-icon").exists()).toBe(
        false,
      );
    }
    // The columns every reader has, and no other.
    expect(
      rows(wrapper)[0]
        .findAll("td")
        .map((cell) => cell.classes().find((name) => name.startsWith("col-"))),
    ).toEqual([
      "col-title",
      "col-author",
      "col-comments",
      "col-likes",
      "col-created",
      "col-lastActivity",
    ]);
  });

  it("pins a topic with a press on its pale pin", async () => {
    const wrapper = await openBoard();
    // The server's listing once the pin is taken: the topic last in the block.
    answerBoard([RULES, NEWS, pinned(PLAIN)], []);

    click(pinOf(wrapper, "t-plain").element);
    await flushPromises();

    expect(mockUpdateTopic).toHaveBeenCalledTimes(1);
    expect(mockUpdateTopic).toHaveBeenCalledWith("t-plain", {
      isAttached: true,
    });
    // Read again: the topic stands last in the pinned block, its pin dark and
    // a handle of the order now.
    expect(rowIds(wrapper)).toEqual(["t-rules", "t-news", "t-plain"]);
    expect(pinOf(wrapper, "t-plain").attributes("aria-pressed")).toBe("true");
    expect(pinOf(wrapper, "t-plain").classes()).toContain("pinned");
    expect(pinOf(wrapper, "t-plain").attributes("data-pin-handle")).toBe(
      "t-plain",
    );
    expect(mockReorderPinnedTopics).not.toHaveBeenCalled();
  });

  it("unpins a topic with a press on its dark pin, a stir of a pixel or two included", async () => {
    const wrapper = await openBoard();
    layRows(wrapper);
    // The server's listing once the pin is off: the topic among the others.
    answerBoard([NEWS], [unpinned(RULES), PLAIN]);
    const rules = pinOf(wrapper, "t-rules").element;

    // A press as the browser sends it: down, a hand that shakes by two
    // pixels, up, and the click.
    pointer(rules, "pointerdown", 20);
    pointer(rules, "pointermove", 22);
    pointer(rules, "pointerup", 22);
    click(rules);
    await flushPromises();

    expect(mockUpdateTopic).toHaveBeenCalledTimes(1);
    expect(mockUpdateTopic).toHaveBeenCalledWith("t-rules", {
      isAttached: false,
    });
    expect(mockReorderPinnedTopics).not.toHaveBeenCalled();
    // Out of the block, back among the topics of the board.
    expect(rowIds(wrapper)).toEqual(["t-news", "t-rules", "t-plain"]);
    expect(pinOf(wrapper, "t-rules").attributes("aria-pressed")).toBe("false");
    expect(pinOf(wrapper, "t-rules").classes()).not.toContain("pinned");
  });

  // The row moves into the pinned block, and focus on a node moved in the page
  // falls to <body>: the caret is put back on the pin it was on.
  it("keeps the caret on a pin pressed from the keyboard", async () => {
    const OTHER = { ...topic("t-other", "Флуд", false), topicNumber: 3 };
    answerBoard([RULES], [unpinned(NEWS), PLAIN, OTHER]);
    const wrapper = await openBoard();
    expect(rowIds(wrapper)).toEqual([
      "t-rules",
      "t-news",
      "t-plain",
      "t-other",
    ]);
    answerBoard([RULES, pinned(OTHER)], [unpinned(NEWS), PLAIN]);

    const pin = pinOf(wrapper, "t-other");
    (pin.element as HTMLElement).focus();
    // A click from the keyboard carries no count of presses.
    pin.element.dispatchEvent(
      new MouseEvent("click", { bubbles: true, cancelable: true, detail: 0 }),
    );
    await flushPromises();

    expect(rowIds(wrapper)).toEqual([
      "t-rules",
      "t-other",
      "t-news",
      "t-plain",
    ]);
    expect(document.activeElement).toBe(pinOf(wrapper, "t-other").element);
  });
});

// The server lets an administrator and the board's moderators pin
// (BoardIntentionResolver.AdministratesTopics), not a site moderator elsewhere.
describe("TopicsList pins, by who may pin", () => {
  beforeEach(() => {
    freshPage();
    answerBoard([RULES, NEWS], [PLAIN]);
  });

  it("gives an administrator the pins of a board he does not moderate", async () => {
    signIn("Admin1", "Admin");
    const wrapper = await openBoard();

    expect(wrapper.findAll(".col-title button.pin-toggle")).toHaveLength(3);
    expect(wrapper.findAll("[data-pin-handle]")).toHaveLength(2);
  });

  it.each([
    ["a moderator", "Moderator"],
    ["a senior moderator", "SeniorModerator"],
  ])(
    "shows %s of the site who does not moderate the board the listing everybody reads",
    async (_who, role) => {
      signIn("GlobalMod", role);
      const wrapper = await openBoard();

      expect(wrapper.find(".col-title button").exists()).toBe(false);
      expect(
        rows(wrapper).map(
          (row) => row.findAll(".col-title .topic-link .topic-icon").length,
        ),
      ).toEqual([1, 1, 0]);
    },
  );
});

describe("TopicsList pinned topics for a reader", () => {
  beforeEach(() => {
    freshPage();
    signIn("reader");
    answerBoard([RULES, NEWS], [PLAIN]);
  });

  // For anyone who cannot order the block the pin is the mark inside the link.
  it("shows the pin as a mark in the title, not as a handle", async () => {
    const wrapper = await openBoard();

    expect(wrapper.find("[data-pin-handle]").exists()).toBe(false);
    expect(wrapper.find(".col-title button").exists()).toBe(false);
    expect(
      rows(wrapper).map(
        (row) => row.findAll(".col-title .topic-link .topic-icon").length,
      ),
    ).toEqual([1, 1, 0]);
  });
});

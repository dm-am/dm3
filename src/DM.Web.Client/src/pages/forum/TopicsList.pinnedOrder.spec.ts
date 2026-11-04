/**
 * @vitest-environment jsdom
 */

// The pinned order, changed by drag or arrows: sent on release, one save at a
// time; a refusal puts the block back and is said on a plate, having no button
// to stand beside. The click after a drag is not a press.
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { useBoardsStore } from "@/entities/forum";
import { mockUpdateTopic, mockReorderPinnedTopics } from "./TopicsList.doubles";
import {
  refusal,
  RULES,
  NEWS,
  PLAIN,
  unpinned,
  answerBoard,
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

/** The server's block once it took the order: the topics in the order sent. */
const reordered = () => answerBoard([NEWS, RULES], [PLAIN]);

describe("TopicsList moderator actions", () => {
  beforeEach(() => {
    moderatedBoard();
    mockUpdateTopic.mockResolvedValue({ data: null, error: null });
    mockReorderPinnedTopics.mockResolvedValue({ data: null, error: null });
  });

  // A drag ends in a click on its pin, which must not unpin the topic.
  it("does not unpin a topic at the end of a drag of its pin", async () => {
    const wrapper = await openBoard();
    layRows(wrapper);
    const handle = pinOf(wrapper, "t-rules").element;

    // Held and let go within its own row.
    pointer(handle, "pointerdown", 20);
    pointer(handle, "pointermove", 35);
    pointer(handle, "pointerup", 35);
    click(handle);
    await flushPromises();

    // Held, carried under the second, and let go there.
    reordered();
    pointer(handle, "pointerdown", 20);
    pointer(handle, "pointermove", 65);
    pointer(handle, "pointerup", 65);
    click(handle);
    await flushPromises();

    expect(mockUpdateTopic).not.toHaveBeenCalled();
    expect(mockReorderPinnedTopics).toHaveBeenCalledTimes(1);
    expect(rowIds(wrapper)).toEqual(["t-news", "t-rules", "t-plain"]);
    expect(pinOf(wrapper, "t-rules").attributes("aria-pressed")).toBe("true");
  });

  it("does not unpin a topic whose drag was taken back with Escape", async () => {
    const wrapper = await openBoard();
    layRows(wrapper);
    const handle = pinOf(wrapper, "t-rules");

    pointer(handle.element, "pointerdown", 20);
    pointer(handle.element, "pointermove", 65);
    await handle.trigger("keydown", { key: "Escape" });
    expect(wrapper.find(".held").exists()).toBe(false);
    pointer(handle.element, "pointerup", 65);
    click(handle.element);
    await flushPromises();

    expect(mockUpdateTopic).not.toHaveBeenCalled();
    expect(mockReorderPinnedTopics).not.toHaveBeenCalled();
    expect(rowIds(wrapper)).toEqual(["t-rules", "t-news", "t-plain"]);
  });

  // The click of a drag comes in the release's task; one that never came takes no press.
  it("takes the press after a drag for a press", async () => {
    const wrapper = await openBoard();
    layRows(wrapper);
    const handle = pinOf(wrapper, "t-rules").element;

    pointer(handle, "pointerdown", 20);
    pointer(handle, "pointermove", 35);
    pointer(handle, "pointerup", 35);
    await new Promise((resolve) => setTimeout(resolve));
    // From the keyboard: no pointer before the click.
    await pinOf(wrapper, "t-rules").trigger("click");
    await flushPromises();

    expect(mockUpdateTopic).toHaveBeenCalledWith("t-rules", {
      isAttached: false,
    });
  });

  it("moves a pinned topic where it is let go and sends the order once", async () => {
    const wrapper = await openBoard();
    layRows(wrapper);
    const handle = wrapper.findAll("[data-pin-handle]")[0];

    pointer(handle.element, "pointerdown", 20);
    // Below the middle of the second pinned row.
    pointer(handle.element, "pointermove", 65);
    await flushPromises();

    // Held: the row stays where it was, faded, and a line stands under the
    // last pinned row, where the topic would go.
    expect(rowIds(wrapper)).toEqual(["t-rules", "t-news", "t-plain"]);
    expect(handle.classes()).toContain("held");
    expect(
      wrapper
        .findAll("[data-pin-handle]")
        .map((one) => one.classes().filter((name) => name.startsWith("drop-"))),
    ).toEqual([[], ["drop-after"]]);
    // One line: the first row under the block, a pin too, draws none of its own.
    expect(wrapper.findAll('[class*="drop-"]')).toHaveLength(1);
    expect(mockReorderPinnedTopics).not.toHaveBeenCalled();

    reordered();
    pointer(handle.element, "pointerup", 65);
    await flushPromises();

    expect(rowIds(wrapper)).toEqual(["t-news", "t-rules", "t-plain"]);
    expect(mockReorderPinnedTopics).toHaveBeenCalledTimes(1);
    expect(mockReorderPinnedTopics).toHaveBeenCalledWith("main", [
      "t-news",
      "t-rules",
    ]);
    expect(wrapper.find(".held").exists()).toBe(false);
    expect(wrapper.find('[class*="drop-"]').exists()).toBe(false);
    // The order on screen is the answer: no line, no plate.
    expect(messages()).toEqual([]);
  });

  // A pale pin moves nothing: only the block is reordered.
  it("drags nothing by the pin of a topic that is not pinned", async () => {
    const wrapper = await openBoard();
    layRows(wrapper);
    const plain = pinOf(wrapper, "t-plain").element;

    pointer(plain, "pointerdown", 100);
    pointer(plain, "pointermove", 10);
    await flushPromises();
    expect(wrapper.find('[class*="drop-"]').exists()).toBe(false);
    pointer(plain, "pointerup", 10);
    await flushPromises();

    expect(mockReorderPinnedTopics).not.toHaveBeenCalled();
    expect(rowIds(wrapper)).toEqual(["t-rules", "t-news", "t-plain"]);
  });

  it("sends nothing for a press that does not travel, or a topic let go where it stood", async () => {
    const wrapper = await openBoard();
    layRows(wrapper);
    const handle = wrapper.findAll("[data-pin-handle]")[0].element;

    // A press that stirs by a pixel or two is a press, not a drag: nothing
    // is held.
    pointer(handle, "pointerdown", 20);
    pointer(handle, "pointermove", 22);
    await flushPromises();
    expect(wrapper.find(".held").exists()).toBe(false);
    pointer(handle, "pointerup", 22);
    // Dragged within its own row, whose two edges leave the order as it is:
    // held, with no line, since letting it go there changes nothing.
    pointer(handle, "pointerdown", 20);
    pointer(handle, "pointermove", 35);
    await flushPromises();
    expect(wrapper.find(".held").exists()).toBe(true);
    expect(wrapper.find('[class*="drop-"]').exists()).toBe(false);
    pointer(handle, "pointerup", 35);
    await flushPromises();

    expect(mockReorderPinnedTopics).not.toHaveBeenCalled();
    expect(rowIds(wrapper)).toEqual(["t-rules", "t-news", "t-plain"]);
  });

  it("puts the block back in the server's order and says the refusal on a plate", async () => {
    mockReorderPinnedTopics.mockResolvedValue({
      data: null,
      error: {
        ...refusal(409),
        title: "Список закрепленных топиков изменился",
      },
    });
    const wrapper = await openBoard();
    layRows(wrapper);
    const handle = wrapper.findAll("[data-pin-handle]")[0].element;

    pointer(handle, "pointerdown", 20);
    pointer(handle, "pointermove", 65);
    pointer(handle, "pointerup", 65);
    await flushPromises();

    expect(mockReorderPinnedTopics).toHaveBeenCalledTimes(1);
    expect(rowIds(wrapper)).toEqual(["t-rules", "t-news", "t-plain"]);
    // No button of this action has a place for a line.
    expect(messages()).toEqual(["Список закрепленных топиков изменился"]);
  });

  it("sends one order at a time, and the last one made waits for the one out", async () => {
    let answer: (value: { data: null; error: null }) => void = () => {};
    mockReorderPinnedTopics.mockImplementationOnce(
      () => new Promise((resolve) => (answer = resolve)),
    );
    const wrapper = await openBoard();
    layRows(wrapper);

    // Moved down, then back up while the first save is out.
    await wrapper.findAll("[data-pin-handle]")[0].trigger("keydown", {
      key: "ArrowDown",
    });
    await flushPromises();
    expect(rowIds(wrapper)).toEqual(["t-news", "t-rules", "t-plain"]);
    await wrapper.findAll("[data-pin-handle]")[1].trigger("keydown", {
      key: "ArrowUp",
    });
    await flushPromises();

    expect(rowIds(wrapper)).toEqual(["t-rules", "t-news", "t-plain"]);
    expect(mockReorderPinnedTopics).toHaveBeenCalledTimes(1);

    answer({ data: null, error: null });
    await flushPromises();

    expect(mockReorderPinnedTopics.mock.calls.map((call) => call[1])).toEqual([
      ["t-news", "t-rules"],
      ["t-rules", "t-news"],
    ]);
    expect(rowIds(wrapper)).toEqual(["t-rules", "t-news", "t-plain"]);
  });

  it("moves a pinned topic a place with the arrow keys and keeps the caret on it", async () => {
    const wrapper = await openBoard();

    // Up from the top of the block goes nowhere.
    await wrapper.findAll("[data-pin-handle]")[0].trigger("keydown", {
      key: "ArrowUp",
    });
    await flushPromises();
    expect(mockReorderPinnedTopics).not.toHaveBeenCalled();

    reordered();
    const rules = wrapper.findAll("[data-pin-handle]")[0];
    (rules.element as HTMLElement).focus();
    await rules.trigger("keydown", { key: "ArrowDown" });
    await flushPromises();

    expect(rowIds(wrapper)).toEqual(["t-news", "t-rules", "t-plain"]);
    expect(mockReorderPinnedTopics).toHaveBeenCalledTimes(1);
    expect(mockReorderPinnedTopics).toHaveBeenCalledWith("main", [
      "t-news",
      "t-rules",
    ]);
    expect(document.activeElement).toBe(
      wrapper.get('[data-pin-handle="t-rules"]').element,
    );
  });

  it("offers no handle where the pinned block does not stand on its own", async () => {
    const wrapper = await openBoard();

    // A filter mixes the pinned topics into the results, where a pinned one
    // keeps its pin, still the toggle, but moves nowhere.
    const store = useBoardsStore();
    store.attachedTopics = null;
    store.topics = { resources: [RULES, PLAIN], paging: null } as never;
    await flushPromises();

    expect(rowIds(wrapper)).toEqual(["t-rules", "t-plain"]);
    expect(wrapper.find("[data-pin-handle]").exists()).toBe(false);
    expect(rows(wrapper)[0].findAll(".col-title .topic-icon")).toHaveLength(1);
    expect(pinOf(wrapper, "t-rules").attributes("aria-pressed")).toBe("true");
    expect(await tipOf(pinOf(wrapper, "t-rules"))).toBe("Открепить топик");
  });

  it("offers no handle when one topic is pinned", async () => {
    answerBoard([RULES], [unpinned(NEWS), PLAIN]);
    const wrapper = await openBoard();

    expect(wrapper.find("[data-pin-handle]").exists()).toBe(false);
    const read = [];
    for (const row of rows(wrapper)) {
      const pin = row.get(".col-title .pin-toggle");
      read.push([
        pin.attributes("data-pin-toggle"),
        row.findAll(".col-title .topic-icon").length,
        pin.attributes("aria-pressed"),
        await tipOf(pin),
      ]);
    }
    expect(read).toEqual([
      ["t-rules", 1, "true", "Открепить топик"],
      ["t-news", 1, "false", "Закрепить топик"],
      ["t-plain", 1, "false", "Закрепить топик"],
    ]);
  });
});

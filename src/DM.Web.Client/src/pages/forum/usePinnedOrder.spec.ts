/**
 * @vitest-environment jsdom
 */

/**
 * The order of the pinned topics, changed on the listing: the order shown at
 * once, the saves that never run two at a time, a refusal that puts the block
 * back, and the arrow keys.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { nextTick } from "vue";
import {
  board,
  closeScope,
  heldSends,
  key,
  openScope,
  topic,
} from "./usePinnedOrder.fixtures";

beforeEach(openScope);

afterEach(closeScope);

const REFUSAL = { status: 403, title: "Нельзя" };

describe("usePinnedOrder: the saves", () => {
  it("shows the new order at once, sends it whole, and says where the topic stands", () => {
    const { send } = heldSends();
    const { pins, shown } = board(send);

    expect(pins.move(0, 2)).toBe(true);

    expect(shown()).toEqual(["b", "c", "a"]);
    expect(send).toHaveBeenCalledWith(["b", "c", "a"]);
    expect(pins.saving.value).toBe(true);
    expect(pins.announcement.value).toBe("Топик a: место 3 из 3");
  });

  it("sends only the last order that waited for the save before it", async () => {
    const { send, answer } = heldSends();
    const { pins, shown } = board(send);

    pins.move(0, 1);
    pins.move(1, 2);
    pins.move(0, 1);
    expect(shown()).toEqual(["c", "b", "a"]);
    expect(send).toHaveBeenCalledOnce();

    answer();
    await nextTick();
    expect(send).toHaveBeenCalledTimes(2);
    expect(send).toHaveBeenLastCalledWith(["c", "b", "a"]);

    answer();
    await vi.waitFor(() => expect(pins.saving.value).toBe(false));
    expect(send).toHaveBeenCalledTimes(2);
  });

  it("hands the block back to the server's order once the last save is in", async () => {
    const { send, answer } = heldSends();
    const { pins, shown } = board(send);
    pins.move(0, 2);

    answer();
    await vi.waitFor(() => expect(pins.saving.value).toBe(false));

    // The board reads the block again after a save; until then it shows what it holds.
    expect(shown()).toEqual(["a", "b", "c"]);
  });

  it("puts the block back and drops what waited when the server refuses", async () => {
    const { send, answer } = heldSends();
    const { pins, refused, shown } = board(send);
    pins.move(0, 2);
    pins.move(0, 1);

    answer({ error: REFUSAL });
    await vi.waitFor(() => expect(pins.saving.value).toBe(false));

    expect(refused).toHaveBeenCalledWith(REFUSAL);
    expect(send).toHaveBeenCalledOnce();
    expect(shown()).toEqual(["a", "b", "c"]);
  });

  it("does not send an order that waited while the block changed under it", async () => {
    const { send, answer } = heldSends();
    const { pins, pinned } = board(send);
    pins.move(0, 1);
    pins.move(1, 2);

    pinned.value = [topic("a"), topic("b")];
    answer();
    await vi.waitFor(() => expect(pins.saving.value).toBe(false));

    expect(send).toHaveBeenCalledOnce();
  });

  it.each([
    [1, 1],
    [-1, 0],
    [0, 3],
    [3, 0],
  ])("does not move from %i to %i", (from, to) => {
    const { send } = heldSends();
    const { pins, shown } = board(send);

    expect(pins.move(from, to)).toBe(false);
    expect(send).not.toHaveBeenCalled();
    expect(shown()).toEqual(["a", "b", "c"]);
  });
});

describe("usePinnedOrder: the arrow keys on a pin", () => {
  it("move the topic a place and name it for the caret to stay on", () => {
    const { pins, shown } = board();

    expect(pins.onKeydown(0, key("ArrowDown"))).toBe("a");
    expect(shown()).toEqual(["b", "a", "c"]);
    expect(pins.onKeydown(1, key("ArrowUp"))).toBe("a");
    expect(shown()).toEqual(["a", "b", "c"]);
  });

  it("do nothing past the ends, for other keys, or where ordering is off", () => {
    const { pins, enabled, shown } = board();
    const other = key("Enter");

    expect(pins.onKeydown(0, key("ArrowUp"))).toBeNull();
    expect(pins.onKeydown(2, key("ArrowDown"))).toBeNull();
    expect(pins.onKeydown(0, other)).toBeNull();
    expect(other.preventDefault).not.toHaveBeenCalled();
    enabled.value = false;
    expect(pins.onKeydown(0, key("ArrowDown"))).toBeNull();
    expect(shown()).toEqual(["a", "b", "c"]);
  });
});

/**
 * @vitest-environment jsdom
 */

/**
 * The drag of a pinned topic by its pin, with the click that follows its
 * release, as usePinnedOrder answers it.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { nextTick } from "vue";
import {
  board,
  closeScope,
  heldSends,
  key,
  openScope,
} from "./usePinnedOrder.fixtures";

beforeEach(openScope);

afterEach(closeScope);

/** Rows 40 high, the first one's top at 100. */
const ROWS = () =>
  [0, 1, 2].map((at) => ({
    getBoundingClientRect: () => ({ top: 100 + at * 40, height: 40 }),
  })) as unknown as HTMLElement[];

const pointer = (clientY: number, over: Record<string, unknown> = {}) =>
  ({
    button: 0,
    isPrimary: true,
    pointerId: 1,
    clientY,
    preventDefault: vi.fn(),
    currentTarget: null,
    ...over,
  }) as unknown as PointerEvent;

describe("usePinnedOrder: the drag by the pin", () => {
  it("holds the topic past a few pixels, aims between the rows and stands it there", () => {
    const { send } = heldSends();
    const { pins, shown } = board(send);

    pins.onPointerDown(0, pointer(110), ROWS);
    pins.onPointerMove(pointer(112));
    expect(pins.held.value).toBeNull();

    pins.onPointerMove(pointer(130));
    expect(pins.held.value).toBe(0);
    expect(pins.target.value).toBeNull();

    pins.onPointerMove(pointer(170));
    expect(pins.target.value).toBe(2);
    pins.onPointerMove(pointer(210));
    expect(pins.target.value).toBe(3);

    pins.onPointerUp(pointer(210));
    expect(shown()).toEqual(["b", "c", "a"]);
    expect(send).toHaveBeenCalledWith(["b", "c", "a"]);
    expect(pins.held.value).toBeNull();
  });

  it("eats the one click that follows the release of a drag, and only it", () => {
    const { pins } = board();
    pins.onPointerDown(0, pointer(110), ROWS);
    pins.onPointerMove(pointer(190));
    pins.onPointerUp(pointer(190));

    expect(pins.isDragClick()).toBe(true);
    expect(pins.isDragClick()).toBe(false);
  });

  it("leaves the click of a press that did not travel to the pin", () => {
    const { pins } = board();
    pins.onPointerDown(1, pointer(150), ROWS);
    pins.onPointerUp(pointer(151));

    expect(pins.isDragClick()).toBe(false);
  });

  it("lets the held topic go where it was on Escape, and still eats the click", () => {
    const { send } = heldSends();
    const { pins, shown } = board(send);
    pins.onPointerDown(0, pointer(110), ROWS);
    pins.onPointerMove(pointer(190));

    expect(pins.onKeydown(0, key("Escape"))).toBeNull();
    expect(pins.held.value).toBeNull();
    pins.onPointerMove(pointer(200));
    pins.onPointerUp(pointer(200));

    expect(shown()).toEqual(["a", "b", "c"]);
    expect(send).not.toHaveBeenCalled();
    expect(pins.isDragClick()).toBe(true);
  });

  it("takes no press while ordering is off", () => {
    const { pins, enabled } = board();
    enabled.value = false;
    pins.onPointerDown(0, pointer(110), ROWS);
    pins.onPointerMove(pointer(190));
    expect(pins.held.value).toBeNull();
  });

  it("ends a drag in hand and drops a waiting order when ordering goes off", async () => {
    const { send, answer } = heldSends();
    const { pins, enabled } = board(send);
    pins.move(0, 1);
    pins.move(1, 2);
    pins.onPointerDown(0, pointer(110), ROWS);
    pins.onPointerMove(pointer(190));

    enabled.value = false;
    await nextTick();
    expect(pins.held.value).toBeNull();

    answer();
    await vi.waitFor(() => expect(pins.saving.value).toBe(false));
    expect(send).toHaveBeenCalledOnce();
  });
});

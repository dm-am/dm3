/**
 * @vitest-environment jsdom
 */

/**
 * The order of the pinned topics, changed on the listing: the order shown at
 * once, the saves that never run two at a time, a refusal that puts the block
 * back, the arrow keys, and the drag by the pin with the click that follows
 * its release.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { effectScope, nextTick, ref, type EffectScope } from "vue";
import type { Topic } from "@/entities/forum";
import { usePinnedOrder } from "./usePinnedOrder";

const topic = (id: string) => ({ id, title: `Тема ${id}` }) as unknown as Topic;
const REFUSAL = { status: 403, title: "Нельзя" };

/** A request the test answers, one at a time in the order they went out. */
function heldSends() {
  const answers: Array<(value: { error?: unknown }) => void> = [];
  const send = vi.fn(
    (ids: string[]) =>
      new Promise<{ error?: unknown }>((resolve) => {
        void ids;
        answers.push(resolve);
      }),
  );
  return {
    send,
    answer: (value: { error?: unknown } = {}) => answers.shift()!(value),
  };
}

let scope: EffectScope;

function board(send = heldSends().send) {
  const pinned = ref<Topic[] | null>([topic("a"), topic("b"), topic("c")]);
  const enabled = ref(true);
  const refused = vi.fn();
  const pins = scope.run(() =>
    usePinnedOrder({
      pinned,
      enabled,
      send: send as never,
      refused,
    }),
  )!;
  const shown = () => pins.order.value.map((t) => String(t.id));
  return { pins, pinned, enabled, refused, shown };
}

beforeEach(() => {
  scope = effectScope();
});

afterEach(() => {
  scope.stop();
});

describe("usePinnedOrder: the saves", () => {
  it("shows the new order at once, sends it whole, and says where the topic stands", () => {
    const { send } = heldSends();
    const { pins, shown } = board(send);

    expect(pins.move(0, 2)).toBe(true);

    expect(shown()).toEqual(["b", "c", "a"]);
    expect(send).toHaveBeenCalledWith(["b", "c", "a"]);
    expect(pins.saving.value).toBe(true);
    expect(pins.announcement.value).toBe("Тема a: место 3 из 3");
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

    // The board reads the block again after a save; until then it shows what
    // it holds.
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

const key = (name: string) =>
  ({ key: name, preventDefault: vi.fn() }) as unknown as KeyboardEvent;

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

  it("takes no press while ordering is off, and no second finger", () => {
    const { pins, enabled } = board();
    pins.onPointerDown(0, pointer(110, { isPrimary: false }), ROWS);
    pins.onPointerMove(pointer(190));
    expect(pins.held.value).toBeNull();

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

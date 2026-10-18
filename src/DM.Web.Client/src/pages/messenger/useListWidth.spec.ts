/**
 * @vitest-environment jsdom
 */

/**
 * The list of conversations beside a correspondence is as wide as the reader
 * drags it: from the width of the right column down to the least width a list
 * with words still reads at, and below that the narrow state, the pictures and
 * the counts alone. The width is the reader's own and outlives the tab; a
 * browser that refuses the storage only loses that.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  LIST_FULL_WIDTH,
  LIST_KEY_STEP,
  LIST_LEAST_WIDTH,
  LIST_NARROW_WIDTH,
  LIST_WIDTH_KEY,
  settleWidth,
  useListWidth,
} from "./useListWidth";

/** A pointer event of the edge, with the element that captures it. */
function pointer(clientX: number, button = 0) {
  const target = {
    setPointerCapture: vi.fn(),
    releasePointerCapture: vi.fn(),
  };
  return {
    event: {
      clientX,
      button,
      pointerId: 1,
      currentTarget: target,
      preventDefault: vi.fn(),
    } as unknown as PointerEvent,
    target,
  };
}

function key(name: string) {
  return { key: name, preventDefault: vi.fn() } as unknown as KeyboardEvent;
}

beforeEach(() => localStorage.clear());
afterEach(() => vi.restoreAllMocks());

describe("the width a list may stand at", () => {
  it("is the narrow state below the middle of the narrow and the least", () => {
    expect(settleWidth(LIST_NARROW_WIDTH)).toBe(LIST_NARROW_WIDTH);
    expect(settleWidth(120)).toBe(LIST_NARROW_WIDTH);
    expect(settleWidth(140)).toBe(LIST_LEAST_WIDTH);
    expect(settleWidth(250)).toBe(250);
    expect(settleWidth(5000)).toBe(LIST_FULL_WIDTH);
    expect(settleWidth(Number.NaN)).toBe(LIST_FULL_WIDTH);
  });
});

describe("useListWidth", () => {
  it("starts at the full width for a reader who never dragged it", () => {
    const { width, narrow } = useListWidth();
    expect(width.value).toBe(LIST_FULL_WIDTH);
    expect(narrow.value).toBe(false);
  });

  it("starts at the width the reader left it at", () => {
    localStorage.setItem(LIST_WIDTH_KEY, String(LIST_NARROW_WIDTH));
    expect(useListWidth().narrow.value).toBe(true);

    localStorage.setItem(LIST_WIDTH_KEY, "240");
    expect(useListWidth().width.value).toBe(240);

    // Whatever else is stored there is settled, not trusted.
    localStorage.setItem(LIST_WIDTH_KEY, "nonsense");
    expect(useListWidth().width.value).toBe(LIST_FULL_WIDTH);
  });

  it("works where the browser refuses the storage", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("denied");
    });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("denied");
    });
    const list = useListWidth();
    expect(list.width.value).toBe(LIST_FULL_WIDTH);

    list.onPointerDown(pointer(500).event);
    list.onPointerMove(pointer(550).event);
    expect(list.width.value).toBe(LIST_FULL_WIDTH - 50);
  });

  it("narrows as the edge is dragged right, to the narrow state, and keeps it", () => {
    const list = useListWidth();
    const down = pointer(500);
    list.onPointerDown(down.event);
    expect(list.dragging.value).toBe(true);
    expect(down.target.setPointerCapture).toHaveBeenCalledWith(1);

    list.onPointerMove(pointer(560).event);
    expect(list.width.value).toBe(LIST_FULL_WIDTH - 60);
    expect(localStorage.getItem(LIST_WIDTH_KEY)).toBe(
      String(LIST_FULL_WIDTH - 60),
    );

    list.onPointerMove(pointer(750).event);
    expect(list.narrow.value).toBe(true);
    expect(localStorage.getItem(LIST_WIDTH_KEY)).toBe(
      String(LIST_NARROW_WIDTH),
    );

    const up = pointer(750);
    list.onPointerUp(up.event);
    expect(list.dragging.value).toBe(false);
    expect(up.target.releasePointerCapture).toHaveBeenCalledWith(1);

    // A move after the drag is over moves nothing.
    list.onPointerMove(pointer(300).event);
    expect(list.narrow.value).toBe(true);
  });

  it("does not drag on a button other than the main one", () => {
    const list = useListWidth();
    list.onPointerDown(pointer(500, 2).event);
    list.onPointerMove(pointer(700).event);
    expect(list.width.value).toBe(LIST_FULL_WIDTH);
  });

  it("widens back to the width the list last had its words at", () => {
    const list = useListWidth();
    list.onPointerDown(pointer(500).event);
    list.onPointerMove(pointer(550).event);
    list.onPointerMove(pointer(800).event);
    list.onPointerUp(pointer(800).event);
    expect(list.narrow.value).toBe(true);

    list.widen();
    expect(list.width.value).toBe(LIST_FULL_WIDTH - 50);
  });

  it("answers the arrows of the keyboard a step at a time", () => {
    localStorage.setItem(LIST_WIDTH_KEY, String(LIST_LEAST_WIDTH + 8));
    const list = useListWidth();

    list.onKeydown(key("ArrowRight"));
    // Past the least width it goes narrow rather than a list too narrow
    // for its words.
    expect(list.narrow.value).toBe(true);

    list.onKeydown(key("ArrowLeft"));
    expect(list.width.value).toBe(LIST_LEAST_WIDTH);
    list.onKeydown(key("ArrowLeft"));
    expect(list.width.value).toBe(LIST_LEAST_WIDTH + LIST_KEY_STEP);

    const other = key("Enter");
    list.onKeydown(other);
    expect(other.preventDefault).not.toHaveBeenCalled();
    expect(list.width.value).toBe(LIST_LEAST_WIDTH + LIST_KEY_STEP);
  });
});

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { ref } from "vue";
import { useMessageToolbar } from "./useMessageToolbar";

/** A message row at a known place on screen. */
function row(rect: Partial<DOMRect>): HTMLElement {
  const el = document.createElement("div");
  el.getBoundingClientRect = () => ({ top: 0, right: 0, ...rect }) as DOMRect;
  return el;
}

describe("useMessageToolbar", () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  const toolbar = ref<HTMLElement | null>(null);
  const still = () => false;

  it("positions against the container when there is one", () => {
    const container = ref(row({ top: 100, right: 800 }));
    const bar = useMessageToolbar({ container, toolbar, isScrolling: still });

    bar.showFor(row({ top: 150, right: 700 }), "m1");

    // Coordinates are relative to the container box, because the toolbar is
    // absolutely positioned inside it.
    expect(bar.toolbarPosition.value).toEqual({ top: 34, right: 108 });
    expect(bar.hoveredMessageId.value).toBe("m1");
  });

  it("positions against the viewport when there is no container", () => {
    const bar = useMessageToolbar({ toolbar, isScrolling: still });
    Object.defineProperty(window, "innerWidth", {
      value: 1000,
      writable: true,
    });

    bar.showFor(row({ top: 150, right: 700 }), "m1");

    expect(bar.toolbarPosition.value).toEqual({ top: 134, right: 308 });
  });

  it("ignores hover while the list is scrolling", () => {
    const bar = useMessageToolbar({ toolbar, isScrolling: () => true });

    bar.handleMessageMouseEnter(
      { currentTarget: row({}) } as unknown as MouseEvent,
      "m1",
    );

    // Otherwise the toolbar chases the cursor as messages slide past it.
    expect(bar.hoveredMessageId.value).toBeNull();
  });

  it("still opens on focus while the list is scrolling", () => {
    const bar = useMessageToolbar({ toolbar, isScrolling: () => true });

    bar.handleMessageFocusIn(
      { currentTarget: row({}) } as unknown as FocusEvent,
      "m1",
    );

    // Focus does not arrive by accident, so the hover guard does not apply.
    expect(bar.hoveredMessageId.value).toBe("m1");
  });

  it("keeps the toolbar open while the pointer travels onto it", () => {
    const bar = useMessageToolbar({ toolbar, isScrolling: still });
    bar.showFor(row({}), "m1");

    bar.handleMessageMouseLeave();
    bar.handleToolbarMouseEnter();
    vi.advanceTimersByTime(1000);

    expect(bar.hoveredMessageId.value).toBe("m1");
  });

  it("keeps the toolbar open while focus travels into it", () => {
    const buttons = document.createElement("div");
    const button = document.createElement("button");
    buttons.appendChild(button);
    const bar = useMessageToolbar({
      toolbar: ref(buttons),
      isScrolling: still,
    });
    bar.showFor(row({}), "m1");

    // The toolbar is a sibling of the message row, not a descendant, so
    // focus-within cannot answer this — relatedTarget is what does.
    bar.handleMessageFocusOut({
      relatedTarget: button,
    } as unknown as FocusEvent);
    vi.advanceTimersByTime(1000);

    expect(bar.hoveredMessageId.value).toBe("m1");
  });

  it("closes when focus leaves the message for anywhere else", () => {
    const bar = useMessageToolbar({
      toolbar: ref(document.createElement("div")),
      isScrolling: still,
    });
    bar.showFor(row({}), "m1");

    bar.handleMessageFocusOut({
      relatedTarget: document.createElement("a"),
    } as unknown as FocusEvent);
    vi.advanceTimersByTime(1000);

    expect(bar.hoveredMessageId.value).toBeNull();
  });

  it("drops a half-finished delete confirmation when it closes", () => {
    const bar = useMessageToolbar({ toolbar, isScrolling: still });
    bar.showFor(row({}), "m1");
    bar.confirmingDeleteId.value = "m1";

    bar.handleMessageMouseLeave();
    vi.advanceTimersByTime(1000);

    // A confirmation left armed on a toolbar nobody can see is a click away
    // from deleting something the user has stopped looking at.
    expect(bar.confirmingDeleteId.value).toBeNull();
  });

  it("takes the toolbar away when the list starts scrolling", () => {
    const bar = useMessageToolbar({ toolbar, isScrolling: still });
    bar.showFor(row({}), "m1");
    bar.handleToolbarMouseEnter();

    bar.handleScrollStart();

    expect(bar.hoveredMessageId.value).toBeNull();
    expect(bar.isToolbarHovered.value).toBe(false);
  });
});

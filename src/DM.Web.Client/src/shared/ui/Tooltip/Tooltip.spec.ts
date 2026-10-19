/**
 * @vitest-environment jsdom
 */

/**
 * The tooltip's arrow has to read as part of the bubble, and for a long time
 * it did not: it stood three pixels clear of it, and in the dark theme, where
 * the bubble grows an outline, the outline stopped where the arrow began. Both
 * were one-line consequences of how the arrow was drawn — a transparent-border
 * triangle offset from the bubble's PADDING box, when the bubble ends at its
 * BORDER box, and a triangle has no border of its own to carry.
 *
 * Neither defect is visible to an ordinary mount: jsdom applies no scoped
 * styles and vitest never compiles a <style> block, so every assertion about
 * the arrow was an assertion about markup, and markup was not where it went
 * wrong. So the stylesheet is compiled here and read as the browser reads it,
 * and the relations are checked rather than the numbers: the arrow's offset
 * against the bubble's own border width, the arrow's surface against the
 * bubble's own surface. A repaint of either that forgets the other goes red.
 *
 * The second half is behaviour, not paint: a bubble pushed off centre by the
 * viewport edge slides its arrow back towards the trigger, and that offset
 * reaches the arrow as an inline transform. It is the one thing a rotation
 * written into the arrow's own transform would quietly overwrite.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { mount, config } from "@vue/test-utils";
import { nextTick } from "vue";
import Tooltip from "./Tooltip.vue";
import { VIEWPORT_EDGE } from "@/shared/lib/constants/geometry";

// Rendered in place, not into <body>: the same convention the dialog specs use.
config.global.stubs = { Teleport: true };

const rect = (part: Partial<DOMRect>): DOMRect =>
  ({
    x: 0,
    y: 0,
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    width: 0,
    height: 0,
    toJSON: () => ({}),
    ...part,
  }) as DOMRect;

const TRIGGER = { left: 1000, right: 1020, top: 200, bottom: 220 };

/** The reader moves the focus with the keyboard (inputSource.ts listens). */
const keyboard = () =>
  document.dispatchEvent(new KeyboardEvent("keydown", { key: "Tab" }));
const BUBBLE_WIDTH = 300;

describe("Tooltip arrow, when the bubble is pushed off centre", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("slides the arrow back towards the trigger", async () => {
    vi.spyOn(Element.prototype, "getBoundingClientRect").mockImplementation(
      function (this: Element) {
        if (this.classList.contains("tooltip-trigger")) {
          return rect({ ...TRIGGER, width: 20, height: 20 });
        }
        if (this.classList.contains("tooltip")) {
          return rect({ width: BUBBLE_WIDTH, height: 40 });
        }
        return rect({});
      },
    );

    const wrapper = mount(Tooltip, {
      props: { text: "Непрочитанных уведомлений: 5" },
      slots: { default: "<button type='button'>5</button>" },
    });

    // focusin from the keyboard shows without the hover delay, so no timer
    // has to be faked.
    keyboard();
    await wrapper.find(".tooltip-trigger").trigger("focusin");
    // Render, then the measure/position chain the visibility watcher runs.
    for (let i = 0; i < 6; i += 1) await nextTick();

    const ideal = TRIGGER.left + (20 - BUBBLE_WIDTH) / 2;
    const clamped = Math.min(
      ideal,
      window.innerWidth - BUBBLE_WIDTH - VIEWPORT_EDGE,
    );
    expect(ideal - clamped).toBeGreaterThan(0);

    expect(wrapper.find(".tooltip-arrow").attributes("style")).toContain(
      `translateX(calc(-50% + ${ideal - clamped}px))`,
    );

    wrapper.unmount();
  });

  it("leaves the arrow centred when the bubble is not clamped", async () => {
    vi.spyOn(Element.prototype, "getBoundingClientRect").mockImplementation(
      function (this: Element) {
        if (this.classList.contains("tooltip-trigger")) {
          return rect({
            left: 400,
            right: 420,
            top: 200,
            bottom: 220,
            width: 20,
            height: 20,
          });
        }
        if (this.classList.contains("tooltip")) {
          return rect({ width: 100, height: 40 });
        }
        return rect({});
      },
    );

    const wrapper = mount(Tooltip, {
      props: { text: "Жирный (Ctrl+B)" },
      slots: { default: "<button type='button'>b</button>" },
    });

    keyboard();
    await wrapper.find(".tooltip-trigger").trigger("focusin");
    for (let i = 0; i < 6; i += 1) await nextTick();

    // No inline transform at all: the stylesheet's own translate centres it.
    expect(wrapper.find(".tooltip-arrow").attributes("style")).toBeUndefined();

    wrapper.unmount();
  });
});

/**
 * The bubble opens on hover after a delay and on a focus from the keyboard
 * (inputSource.ts tells a keyboard's focus from the one a click gives). jsdom
 * has no PointerEvent: the events a browser sends are built here by hand.
 */

const HERE_POINT = { x: 10, y: 10 };

function send(type: string, target: EventTarget, kind?: "mouse"): void {
  const event = new MouseEvent(type, {
    bubbles: true,
    cancelable: true,
    clientX: HERE_POINT.x,
    clientY: HERE_POINT.y,
  });
  if (kind) Object.defineProperty(event, "pointerType", { value: kind });
  target.dispatchEvent(event);
}

const bubble = (): HTMLElement | null =>
  document.querySelector<HTMLElement>(".tooltip");

describe("Tooltip by the mouse and the keyboard", () => {
  const wrappers: Array<{ unmount(): void }> = [];

  function mountTooltip(slot: string, props: Record<string, unknown> = {}) {
    const wrapper = mount(Tooltip, {
      props: { text: "Мастер: Вася", ...props },
      slots: { default: slot },
      attachTo: document.body,
    });
    wrappers.push(wrapper);
    return wrapper;
  }

  afterEach(() => {
    while (wrappers.length) wrappers.pop()!.unmount();
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("does not open for the focus a click gives the trigger", async () => {
    const wrapper = mountTooltip(
      "<button type='button' class='btn'>v</button>",
    );
    const button = wrapper.find(".btn").element;

    send("pointerdown", button, "mouse");
    button.dispatchEvent(new FocusEvent("focusin", { bubbles: true }));
    await nextTick();

    expect(bubble()).toBeNull();
  });

  it("closes on a press anywhere else", async () => {
    vi.useFakeTimers();
    const wrapper = mountTooltip("<span class='cell'>[+2]</span>");
    const trigger = wrapper.find(".tooltip-trigger").element;
    trigger.dispatchEvent(new MouseEvent("mouseenter"));
    await vi.advanceTimersByTimeAsync(200);
    expect(bubble()).not.toBeNull();

    send("pointerdown", document.body, "mouse");
    await nextTick();

    expect(bubble()).toBeNull();
  });

  it("still opens at once on focus from the keyboard, and closes as it leaves", async () => {
    const wrapper = mountTooltip(
      "<button type='button' class='btn'>v</button>",
    );
    const button = wrapper.find(".btn").element;

    keyboard();
    button.dispatchEvent(new FocusEvent("focusin", { bubbles: true }));
    await nextTick();
    expect(bubble()).not.toBeNull();

    button.dispatchEvent(
      new FocusEvent("focusout", { bubbles: true, relatedTarget: null }),
    );
    await nextTick();
    expect(bubble()).toBeNull();
  });

  it("opens for a focus the browser calls visible, whatever was pressed last", async () => {
    // A screen reader moves the focus without a key reaching the page; the
    // browser still marks that focus :focus-visible.
    const own = Element.prototype.matches;
    vi.spyOn(Element.prototype, "matches").mockImplementation(function (
      this: Element,
      selector: string,
    ) {
      return selector === ":focus-visible" ? true : own.call(this, selector);
    });
    const wrapper = mountTooltip(
      "<button type='button' class='btn'>v</button>",
    );
    const button = wrapper.find(".btn").element;

    send("pointerdown", document.body, "mouse");
    button.dispatchEvent(new FocusEvent("focusin", { bubbles: true }));
    await nextTick();

    expect(bubble()).not.toBeNull();
  });

  it("still opens on hover after the delay and closes after the pointer leaves", async () => {
    vi.useFakeTimers();
    const wrapper = mountTooltip("<span class='cell'>[+2]</span>");
    const trigger = wrapper.find(".tooltip-trigger").element;

    send("pointerover", trigger, "mouse");
    trigger.dispatchEvent(new MouseEvent("mouseenter"));
    await vi.advanceTimersByTimeAsync(200);
    expect(bubble()).not.toBeNull();

    // A click of the mouse on the trigger does not close the bubble.
    send("click", trigger, "mouse");
    await vi.advanceTimersByTimeAsync(3000);
    expect(bubble()).not.toBeNull();

    trigger.dispatchEvent(new MouseEvent("mouseleave"));
    await vi.advanceTimersByTimeAsync(200);
    expect(bubble()).toBeNull();
  });
});

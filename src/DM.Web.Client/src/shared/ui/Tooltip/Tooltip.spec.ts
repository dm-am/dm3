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
import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { mount, config } from "@vue/test-utils";
import { h, nextTick } from "vue";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import Tooltip from "./Tooltip.vue";
import TooltipContent from "./TooltipContent.vue";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";
import { VIEWPORT_EDGE } from "@/shared/lib/constants/geometry";

const HERE = dirname(fileURLToPath(import.meta.url));

// Rendered in place, not into <body>: the same convention the dialog specs use.
config.global.stubs = { Teleport: true };

// ============================================================================
// THE STYLESHEET, AS THE BROWSER GETS IT
// ============================================================================

const px = (value: string | undefined): number => {
  const parsed = Number.parseFloat(value ?? "");
  if (Number.isNaN(parsed)) throw new Error(`not a length: ${value}`);
  return parsed;
};

/** Width and colour out of a `border` shorthand. */
function border(shorthand: string | undefined): {
  width: number;
  colour: string;
} {
  const [width, , ...colour] = (shorthand ?? "").split(/\s+/);
  return { width: px(width), colour: colour.join(" ") };
}

/** Placement class, and the side the arrow is pushed out towards. */
const PLACEMENTS = [
  { placement: "top", offset: "bottom" },
  { placement: "bottom", offset: "top" },
  { placement: "left", offset: "right" },
  { placement: "right", offset: "left" },
] as const;

const SIDES = ["top", "right", "bottom", "left"] as const;

describe("Tooltip arrow, as it is drawn", () => {
  // The bubble is drawn by TooltipBubble.vue, for <Tooltip> and for the hidden
  // text of a post alike.
  const css = ruleMap(componentCss(resolve(HERE, "TooltipBubble.vue")));
  const bubble = css.get(".tooltip");
  const arrow = css.get(".tooltip-arrow::before");

  it("reads a stylesheet that has the bubble and the arrow in it", () => {
    // A parse that found nothing would satisfy every loop below by skipping.
    expect(bubble, ".tooltip").toBeDefined();
    expect(arrow, ".tooltip-arrow::before").toBeDefined();
  });

  it("gives the arrow the bubble's own surface and the bubble's own outline", () => {
    expect(arrow!["background-color"]).toBe(bubble!["background-color"]);
    expect(border(arrow!.border).colour).toBe(border(bubble!.border).colour);
  });

  for (const { placement, offset } of PLACEMENTS) {
    const box = css.get(`.tooltip--${placement} .tooltip-arrow`);
    const tip = css.get(`.tooltip--${placement} .tooltip-arrow::before`);

    it(`seats the ${placement} arrow on the bubble's border box`, () => {
      expect(box, `.tooltip--${placement} .tooltip-arrow`).toBeDefined();
      // How far the arrow sticks out: the short side of its box.
      const depth = Math.min(px(box!.width), px(box!.height));
      expect(px(box![offset])).toBe(-(depth + border(bubble!.border).width));
    });

    it(`keeps the ${placement} arrow's own transform free for the clamp offset`, () => {
      // arrowStyle writes an inline transform on the host whenever the bubble
      // is pushed off centre, and an inline transform replaces the whole
      // property: a rotation written beside the host's translate would vanish
      // at the viewport edge and the arrow would flatten. It belongs on the
      // pseudo-element, which nothing overwrites.
      expect(box!.transform).not.toMatch(/rotate/);
      expect(tip!.transform).toMatch(/rotate\(45deg\)/);
    });

    it(`carries the bubble's outline along the ${placement} arrow's two visible sides`, () => {
      expect(
        tip,
        `.tooltip--${placement} .tooltip-arrow::before`,
      ).toBeDefined();
      const drawn = SIDES.filter((side) => tip![`border-${side}-width`]);
      // Two sides show and two hide behind the bubble; an outline on the
      // hidden pair would draw a line across the bubble's own surface.
      expect(drawn).toHaveLength(2);
      for (const side of drawn) {
        expect(px(tip![`border-${side}-width`])).toBe(
          border(bubble!.border).width,
        );
      }
    });
  }
});

// ============================================================================
// THE CLAMP OFFSET STILL REACHES THE ARROW
// ============================================================================

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

/** The reader moves the focus with the keyboard (touchRule.ts listens). */
const keyboard = () =>
  document.dispatchEvent(new KeyboardEvent("keydown", { key: "Tab" }));
const BUBBLE_WIDTH = 300;

describe("Tooltip arrow, when the bubble is pushed off centre", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("slides the arrow back towards the trigger", async () => {
    // Canvas is what the shrink-to-content measurement uses, and jsdom has
    // none; a null context is the path the component already handles.
    vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(null);
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
    vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(null);
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

// ============================================================================
// THE TOUCH RULE
// ============================================================================

/**
 * A finger used to open a tooltip the moment it touched the screen, on
 * touchstart, before anything could tell a tap from a scroll or a long press.
 * A scroll begun on a table cell left a bubble hanging over the table, a tap on
 * "Вниз" left the button's name over "Наверх" so that the next tap landed in
 * the bubble, and a tap on a link flashed a bubble on the way to the next page.
 * The rule now (touchRule.ts): a tap is the click the browser sends for it; it
 * opens the tooltip of text and of a disabled control, follows a link, presses
 * a button; a scroll and a long press open nothing.
 *
 * jsdom has no PointerEvent, no layout and no gestures. The events a browser
 * sends are built here by hand, in the browser's order, touchstart included,
 * so the specs prove the decisions and not the finger; the finger is proved on
 * the stand with touch emulation.
 */

type PointerKind = "touch" | "mouse";

interface Point {
  x: number;
  y: number;
}

const HERE_POINT: Point = { x: 10, y: 10 };

function send(
  type: string,
  target: EventTarget,
  kind?: PointerKind,
  at: Point = HERE_POINT,
  time?: number,
): void {
  const event = new MouseEvent(type, {
    bubbles: true,
    cancelable: true,
    clientX: at.x,
    clientY: at.y,
  });
  if (kind) Object.defineProperty(event, "pointerType", { value: kind });
  if (time !== undefined) {
    Object.defineProperty(event, "timeStamp", { value: time });
  }
  target.dispatchEvent(event);
}

/** What the browser sends when a finger goes down on `target`. */
function fingerDown(target: Element, at: Point = HERE_POINT, time = 1000) {
  send("pointerdown", target, "touch", at, time);
  target.dispatchEvent(new Event("touchstart", { bubbles: true }));
}

/**
 * A tap: the finger down and up, the mouse events the browser makes up after
 * it (mouseenter reaches the trigger the finger landed in), then the click.
 */
async function tap(target: Element, time = 1000): Promise<void> {
  fingerDown(target, HERE_POINT, time);
  send("pointerup", target, "touch", HERE_POINT, time + 80);
  target.dispatchEvent(new Event("touchend", { bubbles: true }));
  target
    .closest(".tooltip-trigger")
    ?.dispatchEvent(new MouseEvent("mouseenter"));
  send("mousedown", target);
  send("mouseup", target);
  send("click", target, "touch", HERE_POINT, time + 90);
  await nextTick();
}

const bubble = (): HTMLElement | null =>
  document.querySelector<HTMLElement>(".tooltip");

describe("Tooltip on touch", () => {
  const wrappers: Array<{ unmount(): void }> = [];

  function mountTooltip(
    slot: string,
    props: Record<string, unknown> = {},
    content?: string | (() => unknown),
  ) {
    const wrapper = mount(Tooltip, {
      props: { text: "Мастер: Вася", ...props },
      slots: { default: slot, ...(content ? { content } : {}) },
      attachTo: document.body,
    });
    wrappers.push(wrapper);
    return wrapper;
  }

  // No canvas in jsdom: the shrink-to-words measurement takes the null
  // context path it already has.
  beforeEach(() => {
    vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(null);
  });

  afterEach(() => {
    while (wrappers.length) wrappers.pop()!.unmount();
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("opens on a short tap on its text", async () => {
    const wrapper = mountTooltip("<span class='cell'>[+2]</span>");

    await tap(wrapper.find(".cell").element);

    expect(bubble()?.textContent).toContain("Мастер: Вася");
  });

  it("opens on the click WebKit sends for a tap, which calls itself a mouse", async () => {
    const wrapper = mountTooltip("<span class='cell'>[+2]</span>");
    const cell = wrapper.find(".cell").element;

    // The finger went down as a finger; the click after it says "mouse".
    fingerDown(cell);
    send("pointerup", cell, "touch", HERE_POINT, 1080);
    send("click", cell, "mouse", HERE_POINT, 1090);
    await nextTick();

    expect(bubble()?.textContent).toContain("Мастер: Вася");
  });

  it("opens nothing when the finger starts a scroll on it", async () => {
    vi.useFakeTimers();
    const wrapper = mountTooltip("<span class='cell'>[+2]</span>");
    const cell = wrapper.find(".cell").element;

    // The browser takes the gesture for a scroll: no click follows.
    fingerDown(cell);
    cell.dispatchEvent(new Event("touchmove", { bubbles: true }));
    send("pointercancel", cell, "touch");
    cell.dispatchEvent(new Event("touchend", { bubbles: true }));
    await vi.advanceTimersByTimeAsync(1000);

    expect(bubble()).toBeNull();
  });

  it("opens nothing on a long press, which stays the phone's", async () => {
    vi.useFakeTimers();
    const wrapper = mountTooltip("<span class='cell'>[+2]</span>");
    const cell = wrapper.find(".cell").element;

    fingerDown(cell);
    await vi.advanceTimersByTimeAsync(700);
    cell.dispatchEvent(new Event("contextmenu", { bubbles: true }));
    send("pointerup", cell, "touch", HERE_POINT, 1700);
    // A phone sends no click after a long press; a browser that does still
    // gets no bubble for it.
    send("click", cell, "touch", HERE_POINT, 1710);
    await vi.advanceTimersByTimeAsync(1000);

    expect(bubble()).toBeNull();
  });

  it("does not take the mouse events made up after a tap for hover", async () => {
    vi.useFakeTimers();
    const wrapper = mountTooltip("<a href='/games' class='link'>5</a>");
    const link = wrapper.find(".link").element;
    link.addEventListener("click", (event) => event.preventDefault());

    await tap(link);
    // Past the hover delay the made-up mouseenter would have started.
    await vi.advanceTimersByTimeAsync(1000);

    expect(bubble()).toBeNull();
  });

  it("follows a link and flashes no bubble on the way", async () => {
    const wrapper = mountTooltip("<a href='/games' class='link'>5</a>");
    const link = wrapper.find(".link").element;
    let followed = false;
    link.addEventListener("click", (event) => {
      followed = !event.defaultPrevented;
      event.preventDefault();
    });

    await tap(link);

    expect(followed).toBe(true);
    expect(bubble()).toBeNull();
  });

  it("presses a button and keeps the name of the button shut", async () => {
    const wrapper = mountTooltip(
      "<button type='button' class='btn'>v</button>",
      { text: "Вниз" },
    );

    await tap(wrapper.find(".btn").element);

    expect(bubble()).toBeNull();
  });

  it("shows the data of a button that leaves the reader on the page", async () => {
    const wrapper = mountTooltip(
      "<button type='button' class='btn'>ERP</button>",
      { text: "Эротический отыгрыш", showOnTap: true },
    );
    const button = wrapper.find(".btn").element;

    await tap(button);
    expect(bubble()?.textContent).toContain("Эротический отыгрыш");

    // A second tap presses the button again and leaves its data on screen.
    await tap(button, 3000);
    expect(bubble()).not.toBeNull();
  });

  describe("on a disabled button, which sends no click", () => {
    const disabled = () =>
      mountTooltip("<button type='button' class='btn' disabled>OK</button>", {
        text: "Сначала выберите фильтр",
      }).find(".btn").element;

    it("tells why it is disabled on a tap", async () => {
      const button = disabled();

      fingerDown(button);
      send("pointerup", button, "touch", HERE_POINT, 1080);
      await nextTick();

      expect(bubble()?.textContent).toContain("Сначала выберите фильтр");
    });

    it("opens nothing for a finger that slid off or held on", async () => {
      const button = disabled();

      fingerDown(button);
      send("pointerup", button, "touch", { x: 10, y: 40 }, 1080);
      await nextTick();
      expect(bubble()).toBeNull();

      fingerDown(button, HERE_POINT, 2000);
      send("pointerup", button, "touch", HERE_POINT, 2700);
      await nextTick();
      expect(bubble()).toBeNull();
    });
  });

  it("closes on a second tap of its text", async () => {
    const wrapper = mountTooltip("<span class='cell'>[+2]</span>");
    const cell = wrapper.find(".cell").element;

    await tap(cell);
    expect(bubble()).not.toBeNull();

    await tap(cell, 3000);
    expect(bubble()).toBeNull();
  });

  it("closes on a tap anywhere else", async () => {
    const wrapper = mountTooltip("<span class='cell'>[+2]</span>");
    await tap(wrapper.find(".cell").element);

    const elsewhere = document.createElement("p");
    document.body.appendChild(elsewhere);
    fingerDown(elsewhere);
    await nextTick();

    expect(bubble()).toBeNull();
    elsewhere.remove();
  });

  it("gets out of the way on a tap on a bubble of plain words", async () => {
    const wrapper = mountTooltip("<span class='cell'>[+2]</span>");
    await tap(wrapper.find(".cell").element);

    fingerDown(bubble()!);
    await nextTick();

    expect(bubble()).toBeNull();
  });

  it("stays for a tap on a link inside the bubble", async () => {
    const wrapper = mountTooltip(
      "<span class='cell'>Ачивки</span>",
      {},
      "<a href='/profile/vasya' class='inside'>Вася</a>",
    );
    await tap(wrapper.find(".cell").element);

    fingerDown(bubble()!.querySelector(".inside")!);
    await nextTick();

    expect(bubble()).not.toBeNull();
  });

  describe("closes by itself", () => {
    it("after 2.5 s when it is one line of plain words", async () => {
      vi.useFakeTimers();
      const wrapper = mountTooltip("<span class='cell'>[+2]</span>");
      await tap(wrapper.find(".cell").element);

      await vi.advanceTimersByTimeAsync(2400);
      expect(bubble()).not.toBeNull();
      await vi.advanceTimersByTimeAsync(200);
      expect(bubble()).toBeNull();
    });

    it("not when it runs to more lines than one", async () => {
      vi.useFakeTimers();
      const own = Element.prototype.getClientRects;
      vi.spyOn(Element.prototype, "getClientRects").mockImplementation(
        function (this: Element) {
          // Two line boxes of the words: a list of names read in two lines.
          if (this.classList.contains("tooltip-text")) {
            return [rect({}), rect({})] as unknown as DOMRectList;
          }
          return own.call(this);
        },
      );
      const wrapper = mountTooltip("<span class='cell'>[+2]</span>");
      await tap(wrapper.find(".cell").element);

      await vi.advanceTimersByTimeAsync(5000);
      expect(bubble()).not.toBeNull();
    });

    it("not when it holds a link to reach", async () => {
      vi.useFakeTimers();
      const wrapper = mountTooltip(
        "<span class='cell'>Ачивки</span>",
        {},
        "<a href='/profile/vasya'>Вася</a>",
      );
      await tap(wrapper.find(".cell").element);

      await vi.advanceTimersByTimeAsync(5000);
      expect(bubble()).not.toBeNull();
    });
  });

  it("closes on a scroll, instead of riding along with its trigger", async () => {
    const wrapper = mountTooltip("<span class='cell'>[+2]</span>");
    await tap(wrapper.find(".cell").element);

    window.dispatchEvent(new Event("scroll"));
    await nextTick();

    expect(bubble()).toBeNull();
  });

  it("does not open for the focus a tap gives the trigger, nor stay for it", async () => {
    vi.useFakeTimers();
    const wrapper = mountTooltip("<span class='cell'>12.03.2020</span>", {
      focusable: true,
    });
    const trigger = wrapper.find(".tooltip-trigger").element;

    // The finger goes down on a focusable trigger: the focus comes before
    // the click does.
    fingerDown(trigger);
    trigger.dispatchEvent(new FocusEvent("focusin", { bubbles: true }));
    await nextTick();
    expect(bubble()).toBeNull();

    send("pointerup", trigger, "touch", HERE_POINT, 1080);
    send("click", trigger, "touch", HERE_POINT, 1090);
    await nextTick();
    expect(bubble()).not.toBeNull();

    // The focus stays on the trigger; the bubble goes all the same.
    await vi.advanceTimersByTimeAsync(2600);
    expect(bubble()).toBeNull();
  });

  // The tag of a picker is a button that opens its tooltip after it acts, and
  // the words of the description in it open a small tooltip of their own. A
  // tap on a word takes the focus off the button, and the tooltip it stands
  // in closed with it: the reader saw the small tooltip flash and both go.
  it("keeps a bubble the finger opened when the focus leaves for a word in it", async () => {
    vi.useFakeTimers();
    const wrapper = mountTooltip(
      "<button type='button' class='btn'>Острые темы</button>",
      { text: undefined, showOnTap: true },
      () =>
        h(TooltipContent, {
          html: 'Игра затрагивает <span class="bb-tip" tabindex="0" data-tip-text="пояснение">спорные</span> темы',
        }),
    );
    const button = wrapper.find(".btn").element;
    await tap(button);
    expect(bubble()).not.toBeNull();

    // The word takes the focus of the tap, or nobody does: either way the
    // focus leaves the button for something that is not the trigger.
    const term = bubble()!.querySelector(".bb-tip")!;
    button.dispatchEvent(
      new FocusEvent("focusout", { bubbles: true, relatedTarget: term }),
    );
    button.dispatchEvent(
      new FocusEvent("focusout", { bubbles: true, relatedTarget: null }),
    );
    await tap(term, 3000);

    expect(bubble()).not.toBeNull();
    expect(
      bubble()!.querySelector(".tooltip--nested")?.textContent?.trim(),
    ).toBe("пояснение");

    // And it stays past the time a one-line bubble of plain words goes by
    // itself: it has a word to press in it.
    await vi.advanceTimersByTimeAsync(5000);
    expect(bubble()).not.toBeNull();
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

    // A click of the mouse is not a tap: it neither closes the bubble nor
    // sets the clock of a tapped one running under a resting pointer.
    send("click", trigger, "mouse");
    await vi.advanceTimersByTimeAsync(3000);
    expect(bubble()).not.toBeNull();

    trigger.dispatchEvent(new MouseEvent("mouseleave"));
    await vi.advanceTimersByTimeAsync(200);
    expect(bubble()).toBeNull();
  });
});

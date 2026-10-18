/**
 * @vitest-environment jsdom
 */

/**
 * The words of a tag description: the server's markup, whose [tip] words
 * open a small tooltip of their own, on hover, on a tap and on a focus from
 * the keyboard.
 *
 * The markup is taken from the pairs of hintMarkup.cases.ts, the string both
 * the server and the moderator's preview draw, so a name the tooltip reads
 * and the server stopped writing fails here. The picture of a word is asked
 * for when the word opens, and not with the text.
 *
 * A picture can fail to load. What used to happen then was an assignment to
 * outerHTML, which took the <img> out from under Vue's patcher, so a flag, a
 * rendered element and a colour of the theme stand there now.
 */
import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { nextTick } from "vue";
import { mount } from "@vue/test-utils";
import TooltipContent from "./TooltipContent.vue";
import { trackInput } from "./touchRule";
import { HINT_MARKUP_PAIRS } from "@/shared/lib/utils/hintMarkup.cases";
import { hintToHtml } from "@/shared/lib/utils/bbcode";
import { VIEWPORT_EDGE } from "@/shared/lib/constants/geometry";

const HERE = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(join(HERE, "TooltipContent.vue"), "utf8");

/** The server's markup of the pair whose input holds the given spelling. */
const serverHtml = (spelling: string): string => {
  const pair = HINT_MARKUP_PAIRS.find(([input]) => input.includes(spelling));
  if (!pair) throw new Error(`no pair holds ${spelling}`);
  return pair[1];
};

const TEXT = serverHtml('[tip text="Политика, религия, национальные');
const IMAGE = serverHtml('[tip img="/images/erp-tooltip.gif"]');
const BOTH = serverHtml('[tip img="https://example.com/a.gif" text=');

// Attached to the document: a touch outside has to reach the listener on
// `document`, and it only gets there by bubbling through a mounted tree.
const mountHtml = (html: string) =>
  mount(TooltipContent, { props: { html }, attachTo: document.body });

// jsdom has no PointerEvent and no TouchEvent constructor. What the handlers
// read of an event is its type, its target and its pointerType, and a bubbling
// MouseEvent with the pointerType set by hand carries all three.
const send = (
  el: Element,
  type: string,
  pointerType?: string,
  init: MouseEventInit = {},
) => {
  const event = new MouseEvent(type, {
    bubbles: true,
    cancelable: true,
    ...init,
  });
  if (pointerType)
    Object.defineProperty(event, "pointerType", { value: pointerType });
  el.dispatchEvent(event);
};

// A tap as the browser sends it (touchRule.ts): the finger down, touchstart,
// the finger up, the mouse events made up after it, the click.
const tap = async (el: Element) => {
  send(el, "pointerdown", "touch");
  el.dispatchEvent(new Event("touchstart", { bubbles: true }));
  send(el, "pointerup", "touch");
  send(el, "mouseover");
  send(el, "click", "touch");
  await nextTick();
};

// The pointer of a mouse moving onto an element and off it.
const hover = async (el: Element) => {
  send(el, "pointerover", "mouse");
  send(el, "mouseover");
  await nextTick();
};

const word = (wrapper: ReturnType<typeof mountHtml>) =>
  wrapper.find(".bb-tip").element as HTMLElement;

const small = () => document.querySelector(".tooltip--nested");

const pointerListeners = (spy: { mock: { calls: unknown[][] } }) =>
  spy.mock.calls
    .filter((call) => call[0] === "pointerdown")
    .map((call) => call[1]);

// jsdom draws no canvas, and the bubble measures its words on one; the
// measure is not what these tests are about.
beforeEach(() => {
  vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(null);
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
  document.body.innerHTML = "";
});

describe("TooltipContent", () => {
  it("draws the server's markup, and plain text as text", () => {
    const html = mountHtml(TEXT);
    expect(html.find(".bb-tip").text()).toBe("спорные");
    html.unmount();

    const plain = mount(TooltipContent, {
      props: { text: "<b>в группе</b> уже выбран тег" },
    });
    expect(plain.find("b").exists()).toBe(false);
    expect(plain.text()).toBe("<b>в группе</b> уже выбран тег");
  });

  it("opens the text of a word on hover, wrapping at the width of a picture", async () => {
    const wrapper = mountHtml(TEXT);

    await hover(word(wrapper));

    expect(small()?.textContent?.trim()).toBe(
      "Политика, религия, национальные конфликты",
    );
    expect((small() as HTMLElement).style.maxWidth).toBe("314px");
    wrapper.unmount();
  });

  it("opens the same way on the markup of the moderator's preview", async () => {
    const wrapper = mountHtml(
      hintToHtml('[tip text="подсказка"]слово[/tip] и дальше'),
    );

    await hover(word(wrapper));

    expect(small()?.textContent?.trim()).toBe("подсказка");
    wrapper.unmount();
  });

  it("asks for the picture when the word opens, and not before", async () => {
    const wrapper = mountHtml(IMAGE);
    expect(document.querySelector("img")).toBeNull();

    await hover(word(wrapper));

    const image = small()?.querySelector("img");
    expect(image?.getAttribute("src")).toBe("/images/erp-tooltip.gif");
    wrapper.unmount();
  });

  it("shows the text of a picture under it", async () => {
    const wrapper = mountHtml(BOTH);

    await hover(word(wrapper));

    expect(small()?.querySelector("img")?.getAttribute("src")).toBe(
      "https://example.com/a.gif",
    );
    expect(small()?.querySelector(".rich-text-caption")?.textContent).toBe(
      "Подпись",
    );
    wrapper.unmount();
  });

  it("replaces a picture that failed with markup Vue owns", async () => {
    const wrapper = mountHtml(IMAGE);
    await hover(word(wrapper));

    small()!.querySelector("img")!.dispatchEvent(new Event("error"));
    await nextTick();

    expect(small()?.querySelector("img")).toBeNull();
    expect(small()?.querySelector(".rich-text-error")?.textContent).toBe(
      "Не удалось загрузить изображение",
    );
    wrapper.unmount();
  });

  it("closes a while after the pointer leaves, and stays while it is on the tooltip", async () => {
    vi.useFakeTimers();
    const wrapper = mountHtml(TEXT);
    await hover(word(wrapper));

    send(word(wrapper), "mouseout", undefined, { relatedTarget: small() });
    small()!.dispatchEvent(new MouseEvent("mouseenter"));
    vi.advanceTimersByTime(500);
    await nextTick();
    expect(small()).not.toBeNull();

    // The pointer leaves the tooltip: it waits a moment for the pointer to
    // come back, as a word it crosses on the way does not close it.
    small()!.dispatchEvent(new MouseEvent("mouseleave"));
    vi.advanceTimersByTime(150);
    await nextTick();
    expect(small()).not.toBeNull();

    vi.advanceTimersByTime(50);
    await nextTick();
    expect(small()).toBeNull();

    // Off the word onto the text around it: the same moment, then gone.
    await hover(word(wrapper));
    send(word(wrapper), "mouseout", undefined, {
      relatedTarget: wrapper.element,
    });
    vi.advanceTimersByTime(150);
    await nextTick();
    expect(small()).not.toBeNull();
    vi.advanceTimersByTime(50);
    await nextTick();
    expect(small()).toBeNull();
    wrapper.unmount();
  });

  it("stays inside the window at its right edge", async () => {
    vi.spyOn(HTMLElement.prototype, "getBoundingClientRect").mockImplementation(
      function (this: HTMLElement) {
        const at = this.classList.contains("bb-tip")
          ? { left: 990, top: 300, width: 30, height: 20 }
          : { left: 0, top: 0, width: 314, height: 60 };
        return {
          ...at,
          x: at.left,
          y: at.top,
          right: at.left + at.width,
          bottom: at.top + at.height,
          toJSON: () => at,
        } as DOMRect;
      },
    );
    const wrapper = mountHtml(TEXT);

    await hover(word(wrapper));
    await nextTick();
    await nextTick();

    // Over the word, which stands at the right edge: moved left until the
    // whole tooltip is inside the window, with the site's margin to spare.
    const bubble = small() as HTMLElement;
    expect(parseFloat(bubble.style.left)).toBe(
      window.innerWidth - 314 - VIEWPORT_EDGE,
    );
    expect(parseFloat(bubble.style.top)).toBeLessThan(300);
    wrapper.unmount();
  });

  it("opens on a focus from the keyboard, and Escape closes it", async () => {
    const wrapper = mountHtml(TEXT);
    document.dispatchEvent(new KeyboardEvent("keydown", { key: "Tab" }));

    word(wrapper).focus();
    await nextTick();
    expect(small()).not.toBeNull();

    word(wrapper).dispatchEvent(
      new KeyboardEvent("keydown", { key: "Escape", bubbles: true }),
    );
    await nextTick();
    expect(small()).toBeNull();
    wrapper.unmount();
  });

  it("never writes into the DOM behind Vue", () => {
    expect(source).not.toContain("outerHTML");
  });

  it("takes its colours from the theme", () => {
    expect(source).not.toMatch(/#[0-9a-fA-F]{3,6}\b/);
  });
});

describe("TooltipContent on touch", () => {
  it("opens the tooltip of a word on a tap", async () => {
    const wrapper = mountHtml(TEXT);

    await tap(word(wrapper));

    expect(small()).not.toBeNull();
    wrapper.unmount();
  });

  it("closes when the touch lands outside the word and its tooltip", async () => {
    const wrapper = mountHtml(TEXT);
    await tap(word(wrapper));

    const elsewhere = document.createElement("div");
    document.body.appendChild(elsewhere);
    await tap(elsewhere);

    expect(small()).toBeNull();
    wrapper.unmount();
  });

  it("holds the tooltip open when the touch lands on it", async () => {
    const wrapper = mountHtml(IMAGE);
    await tap(word(wrapper));

    await tap(small()!.querySelector("img")!);

    expect(small()).not.toBeNull();
    wrapper.unmount();
  });

  it("closes on a second tap of the same word", async () => {
    const wrapper = mountHtml(TEXT);

    await tap(word(wrapper));
    expect(small()).not.toBeNull();

    await tap(word(wrapper));
    expect(small()).toBeNull();
    wrapper.unmount();
  });

  it("opens nothing when the finger starts a scroll on the word", async () => {
    const wrapper = mountHtml(TEXT);
    const term = word(wrapper);

    // The browser takes the gesture for a scroll: no click follows.
    send(term, "pointerdown", "touch");
    term.dispatchEvent(new Event("touchstart", { bubbles: true }));
    send(term, "pointercancel", "touch");
    await nextTick();

    expect(small()).toBeNull();
    wrapper.unmount();
  });

  it("does not take the mouse events made up after a tap for hover", async () => {
    const wrapper = mountHtml(TEXT);

    // A finger that went down somewhere; the browser then sends mouseover
    // to whatever lies under it.
    send(document.body, "pointerdown", "touch");
    send(word(wrapper), "mouseover");
    await nextTick();

    expect(small()).toBeNull();
    wrapper.unmount();
  });

  it("leaves nothing behind when the outer tooltip unmounts it", async () => {
    // The page-wide input tracker is installed once and stays for the page;
    // it is not this component's to take away.
    trackInput();
    const addSpy = vi.spyOn(document, "addEventListener");
    const removeSpy = vi.spyOn(document, "removeEventListener");

    const wrapper = mountHtml(TEXT);
    await tap(word(wrapper));
    expect(small()).not.toBeNull();

    wrapper.unmount();

    expect(small()).toBeNull();
    expect(pointerListeners(addSpy)).toHaveLength(1);
    expect(pointerListeners(removeSpy)).toEqual(pointerListeners(addSpy));
  });
});

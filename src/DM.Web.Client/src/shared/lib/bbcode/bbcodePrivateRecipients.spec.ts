/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { initBbcodeInteractive } from "./bbcodeInteractive";
import { cleanupBbcodeInteractive } from "./bbcodeCleanup";
import {
  privateRecipientsText,
  PRIVATE_HOVER_DELAY,
} from "./bbcodePrivateRecipients";
import { TOOLTIP_OFFSET } from "@/shared/lib/constants/geometry";

const HERE = dirname(fileURLToPath(import.meta.url));

/** Server HTML, parsed out of the document. */
function fragmentOf(html: string): DocumentFragment {
  const template = document.createElement("template");
  template.innerHTML = html;
  return template.content;
}

/**
 * Who reads private text, in a tooltip over its green text, from the block's
 * data-recipients, over the line under the pointer. The hover waits longer
 * than a control's and a press keeps it away.
 */
describe("who reads a hidden block", () => {
  const containers: HTMLElement[] = [];

  /** The corpus the server wrote: its display HTML of one input. */
  function serverDisplay(input: string): string {
    const corpus = JSON.parse(
      readFileSync(
        resolve(HERE, "../testing/markupCorpus.golden.json"),
        "utf8",
      ),
    ) as { entries: Array<{ input: string; display: string }> };
    const entry = corpus.entries.find((e) => e.input === input);
    if (!entry) throw new Error(`the corpus has no entry for ${input}`);
    return entry.display;
  }

  const DISPLAY = serverDisplay('[PRIVATE="Петя,Вася"]раз[/PRIVATE]дваQ0001Q');
  const WORDS = "Получатели: DungeonMaster, Assistant, Петя, Вася";

  function render(html: string = DISPLAY): HTMLElement {
    const container = document.createElement("div");
    container.innerHTML = html;
    document.body.appendChild(container);
    containers.push(container);
    initBbcodeInteractive(container);
    return container;
  }

  const pieceOf = (container: HTMLElement): HTMLElement =>
    container.querySelector(".private-message") as HTMLElement;

  const bubble = (): HTMLElement | null =>
    document.querySelector<HTMLElement>('[role="tooltip"]');

  function send(
    target: EventTarget,
    type: string,
    pointerType?: string,
    time?: number,
  ) {
    const event = new MouseEvent(type, { bubbles: true, cancelable: true });
    if (pointerType) {
      Object.defineProperty(event, "pointerType", { value: pointerType });
    }
    if (time !== undefined) {
      Object.defineProperty(event, "timeStamp", { value: time });
    }
    target.dispatchEvent(event);
  }

  /** The mouse comes over the element (pointerover tells it is a mouse). */
  function hover(element: Element) {
    send(element, "pointerover", "mouse");
    element.dispatchEvent(new MouseEvent("mouseenter"));
  }

  /** The bubble up by hover, the wait run out. */
  async function openByHover(element: Element) {
    hover(element);
    await vi.advanceTimersByTimeAsync(PRIVATE_HOVER_DELAY);
  }

  /** Lets Vue render the bubble, and a fade run out. */
  const settle = () => vi.advanceTimersByTimeAsync(50);

  beforeEach(() => {
    vi.useFakeTimers();
    vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(null);
  });

  afterEach(async () => {
    while (containers.length > 0) {
      const container = containers.pop() as HTMLElement;
      cleanupBbcodeInteractive(container);
      container.remove();
    }
    await settle();
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("reads the names the server writes on the block", () => {
    const piece = fragmentOf(DISPLAY).querySelector(".private-message")!;

    expect(privateRecipientsText(piece)).toBe(WORDS);
  });

  // The page writes no line of recipients: the post reads as its author wrote
  // it, and only the bubble names the readers.
  it("finds no line of recipients in the text the server writes", () => {
    const text = fragmentOf(DISPLAY).textContent ?? "";

    expect(text).not.toContain("Получатели");
    expect(text).not.toContain("DungeonMaster");
  });

  it("shows them on hover after a longer wait than a control's", async () => {
    // A control waits 150 ms; a reader resting the pointer on a line of
    // text is not asking yet.
    expect(PRIVATE_HOVER_DELAY).toBeGreaterThanOrEqual(500);
    hover(pieceOf(render()));

    await vi.advanceTimersByTimeAsync(400);
    expect(bubble()).toBeNull();

    await vi.advanceTimersByTimeAsync(PRIVATE_HOVER_DELAY);
    expect(bubble()?.textContent?.trim()).toBe(WORDS);
  });

  it("keeps the bubble out of the text", async () => {
    const container = render();
    const before = container.textContent;

    hover(pieceOf(container));
    await vi.advanceTimersByTimeAsync(PRIVATE_HOVER_DELAY + 50);

    expect(bubble()).not.toBeNull();
    // A selection of the post copies what it copied before.
    expect(container.contains(bubble())).toBe(false);
    expect(container.textContent).toBe(before);
    // No Tab stop on every green block of a room.
    expect(pieceOf(container).hasAttribute("tabindex")).toBe(false);
  });

  it("points at the line under the pointer, where the pointer is", async () => {
    // Two lines of the block; the pointer rests on the second, off its middle.
    const lines = [
      { left: 100, right: 500, top: 300, bottom: 320 },
      { left: 20, right: 500, top: 321, bottom: 341 },
    ].map(
      ({ left, right, top, bottom }) =>
        new DOMRect(left, top, right - left, bottom - top),
    );
    const own = Element.prototype.getClientRects;
    vi.spyOn(Element.prototype, "getClientRects").mockImplementation(function (
      this: Element,
    ) {
      if (this.classList.contains("private-message")) {
        return lines as unknown as DOMRectList;
      }
      return own.call(this);
    });
    vi.spyOn(Element.prototype, "getBoundingClientRect").mockImplementation(
      function (this: Element) {
        return this.classList.contains("tooltip")
          ? new DOMRect(0, 0, 200, 40)
          : new DOMRect(0, 0, 0, 0);
      },
    );
    const piece = pieceOf(render());

    send(piece, "pointerover", "mouse");
    piece.dispatchEvent(
      new MouseEvent("mouseenter", { clientX: 300, clientY: 330 }),
    );
    await vi.advanceTimersByTimeAsync(PRIVATE_HOVER_DELAY + 50);

    const style = bubble()!.style;
    // Over the second line, not over the block: its top is 321.
    expect(parseFloat(style.top)).toBe(321 - 40 - TOOLTIP_OFFSET);
    // Centred on the pointer, not on the line that runs from 20 to 500.
    expect(parseFloat(style.left)).toBe(300 - 200 / 2);
  });

  it("goes when the pointer leaves the block", async () => {
    const piece = pieceOf(render());
    hover(piece);
    await vi.advanceTimersByTimeAsync(PRIVATE_HOVER_DELAY + 50);

    piece.dispatchEvent(new MouseEvent("mouseleave"));
    await vi.advanceTimersByTimeAsync(200);

    expect(bubble()).toBeNull();
  });

  it("stays away while the mouse presses on the text to select it", async () => {
    const piece = pieceOf(render());
    hover(piece);
    send(piece, "mousedown");

    await vi.advanceTimersByTimeAsync(PRIVATE_HOVER_DELAY * 3);

    expect(bubble()).toBeNull();
  });

  it("stays away from a selection dragged into the block", async () => {
    const piece = pieceOf(render());

    // The button went down on the words before the block; the pointer comes
    // over the block with it held, and crosses the gap between two lines.
    send(piece, "pointerover", "mouse");
    piece.dispatchEvent(new MouseEvent("mouseenter", { buttons: 1 }));
    piece.dispatchEvent(new MouseEvent("mouseleave", { buttons: 1 }));
    piece.dispatchEvent(new MouseEvent("mouseenter", { buttons: 1 }));
    await vi.advanceTimersByTimeAsync(PRIVATE_HOVER_DELAY * 3);
    expect(bubble()).toBeNull();

    // The selection made, the pointer leaves and comes back: a question again.
    piece.dispatchEvent(new MouseEvent("mouseleave"));
    piece.dispatchEvent(new MouseEvent("mouseenter"));
    await vi.advanceTimersByTimeAsync(PRIVATE_HOVER_DELAY + 50);
    expect(bubble()).not.toBeNull();
  });

  /** A private text that is one link and nothing more. */
  const LINK_BLOCK =
    '<span class="private-message" data-recipients="DungeonMaster, Assistant">' +
    '<a href="https://example.com/map" target="_blank" rel="noopener" class="inside">карта бухты</a></span>';

  // The click on a link goes through at once, and the bubble is the hover's.
  it("lets a mouse click follow a link inside at once", async () => {
    const container = render(LINK_BLOCK);
    const link = container.querySelector(".inside")!;
    const followed = vi.fn();
    link.addEventListener("click", followed);

    send(link, "pointerdown", "mouse");
    const click = new MouseEvent("click", { bubbles: true, cancelable: true });
    link.dispatchEvent(click);
    await settle();

    expect(click.defaultPrevented).toBe(false);
    expect(followed).toHaveBeenCalledTimes(1);
    expect(bubble()).toBeNull();
  });

  it("closes on a press elsewhere", async () => {
    const piece = pieceOf(render());

    await openByHover(piece);
    await settle();
    expect(bubble()).not.toBeNull();
    send(document.body, "pointerdown", "mouse");
    await settle();
    expect(bubble()).toBeNull();
  });

  it("closes on a scroll and on a key", async () => {
    const piece = pieceOf(render());

    await openByHover(piece);
    await settle();
    window.dispatchEvent(new Event("scroll"));
    await settle();
    expect(bubble()).toBeNull();

    await openByHover(piece);
    await settle();
    document.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowDown" }));
    await settle();
    expect(bubble()).toBeNull();
  });

  it("shows the names the players wrote as text, never as markup", async () => {
    // The server escapes the name; read back into markup, it would run.
    const container = render(
      '<span class="private-message" data-recipients="DungeonMaster, ' +
        'Assistant, &lt;img src=x onerror=alert(1)&gt;">тайна</span>',
    );

    await openByHover(pieceOf(container));
    await settle();

    expect(bubble()?.querySelector("img")).toBeNull();
    expect(bubble()?.textContent).toContain("<img src=x onerror=alert(1)>");
  });

  it("stops listening once the text is cleaned up", async () => {
    const container = render();
    cleanupBbcodeInteractive(container);

    hover(pieceOf(container));
    await vi.advanceTimersByTimeAsync(PRIVATE_HOVER_DELAY * 2);

    expect(bubble()).toBeNull();
  });
});

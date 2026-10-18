/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, afterEach, beforeEach, vi } from "vitest";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import {
  initSpoilers,
  initBbcodeInteractive,
  cleanupBbcodeInteractive,
  privateRecipientsText,
  PRIVATE_HOVER_DELAY,
} from "./bbcodeInteractive";
import { TAP_CLOSE_DELAY } from "@/shared/ui/Tooltip";
import { TOOLTIP_OFFSET } from "@/shared/lib/constants/geometry";
import {
  closeImageViewer,
  viewedImage,
} from "@/shared/lib/composables/useImageViewer";
import { compileSass, styleRules } from "@/shared/lib/testing/styles";
import { SPOILER_SHOW_TEXT, SPOILER_HIDE_TEXT } from "./bbcodeConstants";

const HERE = dirname(fileURLToPath(import.meta.url));

/** Server HTML, parsed out of the document. */
function fragmentOf(html: string): DocumentFragment {
  const template = document.createElement("template");
  template.innerHTML = html;
  return template.content;
}

/**
 * The head of a spoiler shows the caption the author gave it, closed and open.
 *
 * The server writes the caption of [spoiler text="..."] into the head, and the
 * standard text when there is none. The page wrote the standard text over the
 * head on every initialisation and every toggle, so no reader ever saw a
 * caption. Then it wrote "Скрыть содержимое" over the caption of an open one,
 * and the reader lost the name of what he had just opened: the words of the
 * open head are the server's (data-swaptext), the caption itself when there is
 * one, and they swap only on a spoiler without one, as on DM2.
 */
describe("the spoiler head", () => {
  const containers: HTMLElement[] = [];

  afterEach(() => {
    while (containers.length > 0) {
      const container = containers.pop() as HTMLElement;
      cleanupBbcodeInteractive(container);
      container.remove();
    }
  });

  /** The spoiler as the server writes it, the open words in data-swaptext. */
  function render(head: string, open: string | null = head): HTMLElement {
    const container = document.createElement("div");
    const swap = open === null ? "" : ` data-swaptext="${open}"`;
    container.innerHTML =
      `<a href="#" class="spoiler-head"${swap}>` +
      `${head}</a><div class="spoiler">текст</div>`;
    document.body.appendChild(container);
    containers.push(container);
    initSpoilers(container);
    return container;
  }

  const headOf = (container: HTMLElement): HTMLElement =>
    container.querySelector(".spoiler-head") as HTMLElement;

  it("shows the author's caption, closed, open and closed again", () => {
    const head = headOf(render("Карта сокровищ"));
    expect(head.textContent).toBe("Карта сокровищ");

    head.click();
    expect(head.textContent).toBe("Карта сокровищ");
    expect(head.getAttribute("aria-expanded")).toBe("true");

    head.click();
    expect(head.textContent).toBe("Карта сокровищ");
    expect(head.getAttribute("aria-expanded")).toBe("false");
  });

  it("swaps the standard words when the author gave no caption", () => {
    const head = headOf(render(SPOILER_SHOW_TEXT, SPOILER_HIDE_TEXT));
    expect(head.textContent).toBe(SPOILER_SHOW_TEXT);

    head.click();
    expect(head.textContent).toBe(SPOILER_HIDE_TEXT);

    head.click();
    expect(head.textContent).toBe(SPOILER_SHOW_TEXT);
  });

  it("swaps the standard words on a head written without open words", () => {
    // The spoiler the global chat puts an image behind.
    const head = headOf(render(SPOILER_SHOW_TEXT, null));

    head.click();
    expect(head.textContent).toBe(SPOILER_HIDE_TEXT);

    head.click();
    expect(head.textContent).toBe(SPOILER_SHOW_TEXT);
  });

  it("keeps the caption when the content is initialised again open", () => {
    const container = render("Карта");
    headOf(container).click();

    cleanupBbcodeInteractive(container);
    initSpoilers(container);

    expect(headOf(container).textContent).toBe("Карта");
  });

  it("writes the caption as text, never as markup", () => {
    const head = headOf(render("&lt;b&gt;жирно&lt;/b&gt;"));
    expect(head.textContent).toBe("<b>жирно</b>");
    expect(head.querySelector("b")).toBeNull();

    head.click();
    expect(head.textContent).toBe("<b>жирно</b>");
    expect(head.querySelector("b")).toBeNull();
  });
});

/**
 * A picture of the text opens whole over the page on a click: in the text it
 * is drawn no taller than 500 pixels, as on DM2, and a tall map could not be
 * read there. The owner's word, 27.09.2026, and the cap of DM2 on 28.09.2026.
 * A picture inside [link] goes where the link goes, as before.
 */
describe("a picture of the text", () => {
  const containers: HTMLElement[] = [];
  const SRC = "https://example.com/karta.png";

  afterEach(() => {
    closeImageViewer();
    while (containers.length > 0) {
      const container = containers.pop() as HTMLElement;
      cleanupBbcodeInteractive(container);
      container.remove();
    }
  });

  /** The text as the parsers write it, initialised the way a post is. */
  function render(html: string): HTMLElement {
    const container = document.createElement("div");
    container.innerHTML = html;
    document.body.appendChild(container);
    containers.push(container);
    initBbcodeInteractive(container);
    return container;
  }

  const picture = `<img src="${SRC}" class="bb-image" data-bb-tag="img" alt="" referrerpolicy="no-referrer" />`;
  const click = (el: Element, init: MouseEventInit = {}) => {
    const event = new MouseEvent("click", {
      bubbles: true,
      cancelable: true,
      button: 0,
      ...init,
    });
    el.dispatchEvent(event);
    return event;
  };

  it("opens whole in the viewer on a click", () => {
    const container = render(`Карта:<br>${picture}`);

    click(container.querySelector("img") as HTMLImageElement);

    expect(viewedImage.value).toEqual({ src: SRC });
  });

  it("opens one that came with the text later, and once", () => {
    const container = render("Карта:");
    container.innerHTML += picture;
    // ContentText initialises again on every change of the text.
    initBbcodeInteractive(container);
    initBbcodeInteractive(container);

    const event = click(container.querySelector("img") as HTMLImageElement);

    expect(viewedImage.value).toEqual({ src: SRC });
    expect(event.defaultPrevented).toBe(true);
  });

  it("leaves a picture inside [link] to the link", () => {
    const container = render(`<a href="https://example.com/">${picture}</a>`);
    // Whether the link was left to go, read above the text; the page under
    // test then stays where it is, since jsdom does not navigate.
    let leftToTheLink = false;
    document.body.addEventListener(
      "click",
      (event) => {
        leftToTheLink = !event.defaultPrevented;
        event.preventDefault();
      },
      { once: true },
    );

    click(container.querySelector("img") as HTMLImageElement);

    expect(viewedImage.value).toBeNull();
    expect(leftToTheLink).toBe(true);
  });

  it("leaves a press with a modifier to the browser", () => {
    const container = render(picture);

    click(container.querySelector("img") as HTMLImageElement, {
      ctrlKey: true,
    });

    expect(viewedImage.value).toBeNull();
  });

  it("stops listening once the text is cleaned up", () => {
    const container = render(picture);
    cleanupBbcodeInteractive(container);

    click(container.querySelector("img") as HTMLImageElement);

    expect(viewedImage.value).toBeNull();
  });
});

/** The cap the picture is drawn under in the text, read off the stylesheet. */
describe("the height of a picture in the text", () => {
  const rules = styleRules(
    compileSass(
      [
        '@use "@/assets/styles/BbcodeContent" as *',
        ".bbcode-content",
        "  +bbcode-content",
      ].join("\n"),
    ),
  );
  const declared = (selector: string) =>
    rules.find((rule) => rule.selector === selector)?.declarations ?? {};

  it("is 500 pixels, as on DM2", () => {
    expect(
      declared(".bbcode-content img, .bbcode-content .bb-image")["max-height"],
    ).toBe("var(--bb-image-max-height, 500px)");
  });

  it("keeps the picture whole in a box clamped in height alone", () => {
    expect(
      declared(".bbcode-content img, .bbcode-content .bb-image")["object-fit"],
    ).toBe("contain");
  });
});

/**
 * Who reads a hidden block, in a tooltip over its green text.
 *
 * The server writes the names on the block (data-recipients), and the tooltip
 * is all that says them: the line " (Получатели: ...)" that stood after the
 * block is gone (the owner, 05.10: "а зачем это вообще текстом, если при
 * наведении показывается?"). The bubble stands over the line of the text under
 * the pointer.
 *
 * A reader rests the pointer on what he reads, so the hover waits longer than
 * a control's does, and a press on the text (the start of a selection) keeps
 * the bubble away. A finger follows the site's touch rule with one addition:
 * the first tap on the hidden text opens the bubble wherever it lands, a link
 * or a spoiler inside included, and the second follows the link or opens the
 * spoiler; a scroll and a long press open nothing. The names are the players'
 * own and are shown as text.
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

  /** A tap as the browser sends it, the made-up mouse events included. */
  function tap(element: Element) {
    send(element, "pointerdown", "touch");
    element.dispatchEvent(new Event("touchstart", { bubbles: true }));
    send(element, "pointerup", "touch");
    element.dispatchEvent(new MouseEvent("mouseenter"));
    send(element, "click", "touch");
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

  it("opens on a tap on the green text", async () => {
    tap(pieceOf(render()));
    await settle();

    expect(bubble()?.textContent?.trim()).toBe(WORDS);
  });

  /** A tap as tap() makes it, answering with the click it sent. */
  function tapClick(element: Element): MouseEvent {
    send(element, "pointerdown", "touch");
    send(element, "pointerup", "touch");
    const click = new MouseEvent("click", { bubbles: true, cancelable: true });
    Object.defineProperty(click, "pointerType", { value: "touch" });
    element.dispatchEvent(click);
    return click;
  }

  /** A hidden text that is one link and nothing more. */
  const LINK_BLOCK =
    '<span class="private-message" data-recipients="DungeonMaster, Assistant">' +
    '<a href="https://example.com/map" target="_blank" rel="noopener" class="inside">карта бухты</a></span>';

  // A hidden text that is a link and nothing else would never say on a phone
  // whom it is for if the tap went straight through the link (the owner,
  // 05.10: "как поступать, если приватный текст полностью состоит из ссылки
  // или спойлера").
  it("says who reads it on the first tap on a link inside, and does not follow it", async () => {
    const container = render(LINK_BLOCK);
    const link = container.querySelector(".inside")!;
    const followed = vi.fn();
    link.addEventListener("click", followed);

    const click = tapClick(link);
    await settle();

    expect(bubble()?.textContent?.trim()).toBe(
      "Получатели: DungeonMaster, Assistant",
    );
    expect(click.defaultPrevented).toBe(true);
    expect(followed).not.toHaveBeenCalled();
  });

  it("follows the link on the tap that comes while the bubble is up", async () => {
    const container = render(LINK_BLOCK);
    const link = container.querySelector(".inside")!;
    const followed = vi.fn();
    link.addEventListener("click", followed);

    tapClick(link);
    await settle();
    const second = tapClick(link);
    await settle();

    expect(second.defaultPrevented).toBe(false);
    expect(followed).toHaveBeenCalledTimes(1);
    expect(bubble()).toBeNull();
  });

  it("opens a spoiler inside on the second tap, the first one says who reads it", async () => {
    const container = render(
      '<span class="private-message" data-recipients="DungeonMaster, Assistant">' +
        '<a href="#" class="spoiler-head" data-swaptext="Скрыть содержимое">Показать содержимое</a>' +
        '<div class="spoiler">тайна</div></span>',
    );
    const head = container.querySelector(".spoiler-head")!;

    tapClick(head);
    await settle();
    expect(bubble()).not.toBeNull();
    expect(head.getAttribute("aria-expanded")).toBe("false");

    tapClick(head);
    await settle();
    expect(head.getAttribute("aria-expanded")).toBe("true");
    expect(bubble()).toBeNull();
  });

  // The mouse keeps what it had: the click on a link goes through at once,
  // and the bubble is the hover's.
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

  it("opens nothing for a scroll or a long press that start on it", async () => {
    const piece = pieceOf(render());

    send(piece, "pointerdown", "touch");
    piece.dispatchEvent(new Event("touchstart", { bubbles: true }));
    send(piece, "pointercancel", "touch");
    // The mouse events a browser makes up are no hover either.
    piece.dispatchEvent(new MouseEvent("mouseenter"));
    await vi.advanceTimersByTimeAsync(PRIVATE_HOVER_DELAY * 2);
    expect(bubble()).toBeNull();

    // A long press, and a browser that ends it with a click all the same.
    send(piece, "pointerdown", "touch", 5000);
    send(piece, "pointerup", "touch", 5800);
    send(piece, "click", "touch", 5810);
    await vi.advanceTimersByTimeAsync(PRIVATE_HOVER_DELAY * 2);
    expect(bubble()).toBeNull();
  });

  it("closes on a second tap, and on a tap elsewhere", async () => {
    const piece = pieceOf(render());

    tap(piece);
    await settle();
    tap(piece);
    await settle();
    expect(bubble()).toBeNull();

    tap(piece);
    await settle();
    send(document.body, "pointerdown", "touch");
    await settle();
    expect(bubble()).toBeNull();
  });

  it("closes on a scroll and on a key", async () => {
    const piece = pieceOf(render());

    tap(piece);
    await settle();
    window.dispatchEvent(new Event("scroll"));
    await settle();
    expect(bubble()).toBeNull();

    tap(piece);
    await settle();
    document.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowDown" }));
    await settle();
    expect(bubble()).toBeNull();
  });

  it("closes by itself after a tap when the list is one line", async () => {
    tap(pieceOf(render()));
    await settle();
    expect(bubble()).not.toBeNull();

    await vi.advanceTimersByTimeAsync(TAP_CLOSE_DELAY);

    expect(bubble()).toBeNull();
  });

  it("shows the names the players wrote as text, never as markup", async () => {
    // The server escapes the name; read back into markup, it would run.
    const container = render(
      '<span class="private-message" data-recipients="DungeonMaster, ' +
        'Assistant, &lt;img src=x onerror=alert(1)&gt;">тайна</span>',
    );

    tap(pieceOf(container));
    await settle();

    expect(bubble()?.querySelector("img")).toBeNull();
    expect(bubble()?.textContent).toContain("<img src=x onerror=alert(1)>");
  });

  it("stops listening once the text is cleaned up", async () => {
    const container = render();
    cleanupBbcodeInteractive(container);

    hover(pieceOf(container));
    tap(pieceOf(container));
    await vi.advanceTimersByTimeAsync(PRIVATE_HOVER_DELAY * 2);

    expect(bubble()).toBeNull();
  });
});

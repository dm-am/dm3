/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, afterEach } from "vitest";
import { initBbcodeInteractive } from "./bbcodeInteractive";
import { cleanupBbcodeInteractive } from "./bbcodeCleanup";
import { bbcodeStage } from "@/shared/lib/testing/bbcodeStage";
import {
  closeImageViewer,
  viewedImage,
} from "@/shared/lib/composables/useImageViewer";

/**
 * A picture of the text opens whole over the page on a click: in the text it
 * is drawn no taller than 500 pixels, as on DM2. A picture inside [link] goes
 * where the link goes.
 */
describe("a picture of the text", () => {
  const SRC = "https://example.com/karta.png";

  afterEach(closeImageViewer);
  /** The text as the parsers write it, initialised the way a post is. */
  const render = bbcodeStage(initBbcodeInteractive);

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

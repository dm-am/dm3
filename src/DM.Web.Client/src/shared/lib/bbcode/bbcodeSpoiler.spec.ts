/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from "vitest";
import { cleanupBbcodeInteractive } from "./bbcodeCleanup";
import { initSpoilers } from "./bbcodeSpoiler";
import { bbcodeStage } from "@/shared/lib/testing/bbcodeStage";
import { SPOILER_SHOW_TEXT, SPOILER_HIDE_TEXT } from "./bbcodeConstants";

/**
 * The head shows the author's caption, closed and open; the open words are the
 * server's (data-swaptext) and swap only on a spoiler without a caption, as on
 * DM2.
 */
describe("the spoiler head", () => {
  const stage = bbcodeStage(initSpoilers);

  /** The spoiler as the server writes it, the open words in data-swaptext. */
  function render(head: string, open: string | null = head): HTMLElement {
    const swap = open === null ? "" : ` data-swaptext="${open}"`;
    return stage(
      `<a href="#" class="spoiler-head"${swap}>` +
        `${head}</a><div class="spoiler">текст</div>`,
    );
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

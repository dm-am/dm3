/**
 * @vitest-environment jsdom
 */

/**
 * An [nsfw] block: closed under its head, behind the 18+ overlay on the first
 * open until confirmed ("Развернуть все" counts), and remembered across a
 * re-init after cleanup.
 */

import { describe, it, expect, afterEach } from "vitest";
import { cleanupBbcodeInteractive } from "./bbcodeCleanup";
import { bbcodeStage } from "@/shared/lib/testing/bbcodeStage";
import { initNsfw } from "./bbcodeNsfw";
import { NSFW_SHOW_TEXT, NSFW_HIDE_TEXT } from "./bbcodeConstants";
import {
  clearRegistry,
  expandAll,
} from "@/shared/lib/composables/useExpandableRegistry";

/** The block as the server writes it. */
const SERVER =
  '<a href="#" class="nsfw-head" data-swaptext="Скрыть шокирующий контент">' +
  "Показать шокирующий контент</a>" +
  '<div class="nsfw-spoiler">текст</div>';

/** The rest of the block after the head, the same on every initialisation. */
const BODY =
  '<div class="bb-collapse"><div class="bb-collapse-clip">' +
  '<div class="nsfw-content-wrapper" style="position: relative;">' +
  '<div class="nsfw-spoiler">текст</div>' +
  '<div class="nsfw-overlay" role="button" tabindex="0" ' +
  'aria-label="Подтвердить возраст 18+ и показать содержимое">' +
  '<span class="nsfw-warning">Если вам исполнилось 18 лет и вы готовы к ' +
  "просмотру контента, который может оказаться для вас неприемлемым, " +
  "нажмите сюда</span></div></div></div></div>";

describe("the nsfw block", () => {
  afterEach(clearRegistry);
  const stage = bbcodeStage(initNsfw);
  const render = (): HTMLElement => stage(SERVER);

  const headOf = (container: HTMLElement): HTMLElement =>
    container.querySelector(".nsfw-head") as HTMLElement;
  const overlayOf = (container: HTMLElement): HTMLElement =>
    container.querySelector(".nsfw-overlay") as HTMLElement;
  const collapseOf = (container: HTMLElement): HTMLElement =>
    container.querySelector(".bb-collapse") as HTMLElement;

  it("builds the structure the stylesheet draws, node by node", () => {
    const container = render();

    expect(container.innerHTML).toBe(
      '<a href="#" class="nsfw-head" data-swaptext="Скрыть шокирующий контент" ' +
        'data-initialized="true" role="button" tabindex="0" aria-expanded="false">' +
        "Показать шокирующий контент</a>" +
        BODY,
    );
  });

  it("reuses the structure when initialised again after cleanup", () => {
    const container = render();
    cleanupBbcodeInteractive(container);
    initNsfw(container);

    expect(container.innerHTML).toBe(
      '<a href="#" class="nsfw-head" data-swaptext="Скрыть шокирующий контент" ' +
        'role="button" tabindex="0" aria-expanded="false" data-initialized="true">' +
        "Показать шокирующий контент</a>" +
        BODY,
    );
  });

  it("is closed and inert until the head is pressed, open after", () => {
    const container = render();
    const head = headOf(container);
    expect(collapseOf(container).classList.contains("open")).toBe(false);
    expect(collapseOf(container).inert).toBe(true);
    expect(head.getAttribute("aria-expanded")).toBe("false");
    expect(head.textContent).toBe(NSFW_SHOW_TEXT);

    head.click();

    expect(collapseOf(container).classList.contains("open")).toBe(true);
    expect(collapseOf(container).inert).toBe(false);
    expect(head.getAttribute("aria-expanded")).toBe("true");
    expect(head.textContent).toBe(NSFW_HIDE_TEXT);
  });

  it("shows the 18+ overlay on the first open", () => {
    const container = render();

    headOf(container).click();

    expect(overlayOf(container).classList.contains("confirmed")).toBe(false);
  });

  it("is confirmed by a press on the overlay, and by Enter on it", () => {
    const pressed = render();
    headOf(pressed).click();
    overlayOf(pressed).click();
    expect(overlayOf(pressed).classList.contains("confirmed")).toBe(true);

    const keyed = render();
    headOf(keyed).click();
    overlayOf(keyed).dispatchEvent(
      new KeyboardEvent("keydown", { key: "Enter", cancelable: true }),
    );
    expect(overlayOf(keyed).classList.contains("confirmed")).toBe(true);
  });

  it("keeps the confirmation through closing and opening again", () => {
    const container = render();
    const head = headOf(container);
    head.click();
    overlayOf(container).click();

    head.click();
    head.click();

    expect(overlayOf(container).classList.contains("confirmed")).toBe(true);
  });

  it("shows the overlay again on every open of a block nobody confirmed", () => {
    const container = render();
    const head = headOf(container);
    head.click();
    head.click();
    head.click();

    expect(overlayOf(container).classList.contains("confirmed")).toBe(false);
  });

  it("opens and counts as confirmed on Развернуть все", () => {
    const container = render();

    expandAll();

    expect(collapseOf(container).classList.contains("open")).toBe(true);
    expect(overlayOf(container).classList.contains("confirmed")).toBe(true);
  });

  it("remembers the confirmation when initialised again after cleanup", () => {
    const container = render();
    headOf(container).click();
    overlayOf(container).click();
    headOf(container).click();

    cleanupBbcodeInteractive(container);
    initNsfw(container);
    headOf(container).click();

    expect(overlayOf(container).classList.contains("confirmed")).toBe(true);
  });
});

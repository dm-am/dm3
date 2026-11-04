/**
 * @vitest-environment jsdom
 */

/**
 * The assembly: the line breaks next to a spoiler or nsfw block go, then the
 * spoilers, nsfw blocks, pictures and private text come to life; each part has
 * its own spec.
 */

import { describe, it, expect, afterEach } from "vitest";
import { initBbcodeInteractive } from "./bbcodeInteractive";
import { cleanupBbcodeInteractive } from "./bbcodeCleanup";
import { bbcodeStage } from "@/shared/lib/testing/bbcodeStage";
import { clearRegistry } from "@/shared/lib/composables/useExpandableRegistry";
import {
  closeImageViewer,
  viewedImage,
} from "@/shared/lib/composables/useImageViewer";

describe("initBbcodeInteractive", () => {
  afterEach(() => {
    closeImageViewer();
    clearRegistry();
  });
  const render = bbcodeStage(initBbcodeInteractive);

  it("takes the line breaks next to a spoiler and an nsfw block away", () => {
    const container = render(
      'а<br><a href="#" class="spoiler-head">Показать содержимое</a><br>' +
        '<div class="spoiler">б</div><br>' +
        'в<br><a href="#" class="nsfw-head">Показать шокирующий контент</a>' +
        '<br><div class="nsfw-spoiler">г</div><br>д',
    );

    expect(container.querySelectorAll(":scope > br")).toHaveLength(0);
  });

  it("keeps the line breaks inside the text", () => {
    const container = render(
      'раз<br>два<a href="#" class="spoiler-head">Показать содержимое</a>' +
        '<div class="spoiler">б<br>в</div>',
    );

    expect(container.querySelectorAll("br")).toHaveLength(2);
  });

  it("brings the spoiler, the nsfw block, the picture and the private text to life", () => {
    const container = render(
      '<a href="#" class="spoiler-head">Показать содержимое</a>' +
        '<div class="spoiler">б</div>' +
        '<a href="#" class="nsfw-head">Показать шокирующий контент</a>' +
        '<div class="nsfw-spoiler">г</div>' +
        '<span class="private-message" data-recipients="DungeonMaster">тайна</span>' +
        '<img src="https://example.com/a.png" class="bb-image">',
    );

    expect(container.querySelector(".spoiler-head")?.getAttribute("role")).toBe(
      "button",
    );
    expect(container.querySelector(".nsfw-overlay")).not.toBeNull();
    (container.querySelector(".bb-image") as HTMLElement).click();
    expect(viewedImage.value).toEqual({ src: "https://example.com/a.png" });
  });

  it("lets cleanup return the text to a second initialisation", () => {
    const container = render(
      '<a href="#" class="spoiler-head">Показать содержимое</a>' +
        '<div class="spoiler">б</div>',
    );

    cleanupBbcodeInteractive(container);
    expect(container.querySelector("[data-initialized]")).toBeNull();

    initBbcodeInteractive(container);
    const head = container.querySelector(".spoiler-head") as HTMLElement;
    head.click();
    expect(head.getAttribute("aria-expanded")).toBe("true");
  });
});

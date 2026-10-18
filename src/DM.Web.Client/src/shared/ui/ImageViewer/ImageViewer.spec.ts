/**
 * @vitest-environment jsdom
 */

/**
 * The viewer a picture of the text opens in: the picture whole over the
 * dimmed page, and three ways out, a click anywhere, the cross and Escape.
 */
import { afterEach, describe, expect, it } from "vitest";
import { nextTick } from "vue";
import { enableAutoUnmount, mount } from "@vue/test-utils";
import {
  closeImageViewer,
  openImageViewer,
  viewedImage,
} from "@/shared/lib/composables/useImageViewer";
import { closeOpenWindows } from "@/shared/lib/composables/useOpenWindows";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import ImageViewer from "./ImageViewer.vue";

enableAutoUnmount(afterEach);
afterEach(() => {
  closeImageViewer();
  document.body.innerHTML = "";
});

const SRC = "https://example.com/karta.png";

async function opened() {
  const wrapper = mount(ImageViewer, { attachTo: document.body });
  openImageViewer(SRC);
  await nextTick();
  const frame = document.querySelector(".image-viewer") as HTMLElement;
  return { wrapper, frame };
}

describe("the picture viewer", () => {
  it("shows nothing until a picture is opened", () => {
    mount(ImageViewer, { attachTo: document.body });

    expect(document.querySelector(".image-viewer")).toBeNull();
  });

  // The address of a picture in a post is the author's, and the post keeps
  // its referrer from the host; so does the copy.
  it("shows the picture opened, and tells its host nothing", async () => {
    const { frame } = await opened();

    const img = frame.querySelector("img") as HTMLImageElement;
    expect(img.getAttribute("src")).toBe(SRC);
    expect(img.getAttribute("referrerpolicy")).toBe("no-referrer");
    expect(frame.getAttribute("role")).toBe("dialog");
    expect(frame.getAttribute("aria-modal")).toBe("true");
  });

  it("closes on a click on the picture", async () => {
    const { frame } = await opened();

    (frame.querySelector("img") as HTMLElement).click();

    expect(viewedImage.value).toBeNull();
  });

  it("closes on a click beside it", async () => {
    const { frame } = await opened();

    frame.click();

    expect(viewedImage.value).toBeNull();
  });

  it("closes on the cross, where the caret starts", async () => {
    const { frame } = await opened();
    await nextTick();
    const cross = frame.querySelector(".image-viewer-close") as HTMLElement;

    expect(cross.getAttribute("aria-label")).toBe("Закрыть");
    expect(document.activeElement).toBe(cross);
    cross.click();

    expect(viewedImage.value).toBeNull();
  });

  it("closes on Escape", async () => {
    const { frame } = await opened();

    frame.dispatchEvent(
      new KeyboardEvent("keydown", { key: "Escape", bubbles: true }),
    );

    expect(viewedImage.value).toBeNull();
  });

  it("closes with the session, as the windows do", async () => {
    await opened();

    closeOpenWindows();

    expect(viewedImage.value).toBeNull();
  });
});

/** How the viewer stands over the page, read off its stylesheet. */
describe("the picture viewer on the page", () => {
  const rules = styleRules(
    componentCss(
      join(dirname(fileURLToPath(import.meta.url)), "ImageViewer.vue"),
    ),
  );
  const declared = (selector: string) =>
    rules.find((rule) => rule.selector === selector)?.declarations ?? {};

  it("dims the page the way the windows do, and scrolls a long picture", () => {
    expect(declared(".image-viewer")).toMatchObject({
      position: "fixed",
      "background-color": "var(--overlay-bg)",
      overflow: "auto",
    });
  });

  // Whole: as wide as it is, or as the window when it is wider; as tall as
  // it is, however tall.
  it("draws the picture at its own size, no wider than the window", () => {
    expect(declared(".image-viewer-picture")).toMatchObject({
      "max-width": "100%",
      height: "auto",
    });
    expect(declared(".image-viewer-picture")["max-height"]).toBeUndefined();
  });
});

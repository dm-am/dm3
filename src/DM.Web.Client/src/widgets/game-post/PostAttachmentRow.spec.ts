/**
 * @vitest-environment jsdom
 */

/**
 * The name of an attached picture opens it over the page, in the viewer a
 * picture of the text opens in. It used to open the file in a tab of its own,
 * so the reader left the room to look at a map and had to find the way back.
 *
 * What stays the browser's: a click with a modifier (a tab, a window, a
 * download, whatever the reader asked for) and the name of any other file,
 * which the viewer cannot show.
 */
import { describe, it, expect, afterEach } from "vitest";
import { enableAutoUnmount, mount } from "@vue/test-utils";
import type { PostAttachment } from "@/entities/game";
import { apiUrl } from "@/shared/api/client";
import {
  closeImageViewer,
  viewedImage,
} from "@/shared/lib/composables/useImageViewer";
import PostAttachmentRow from "./PostAttachmentRow.vue";

enableAutoUnmount(afterEach);
afterEach(() => closeImageViewer());

const attachment = (contentType: string, fileName: string) =>
  ({
    id: "a-1",
    fileName,
    contentType,
    sizeBytes: 2048,
    url: "/v1/posts/p-1/attachments/a-1",
  }) as PostAttachment;

const picture = attachment("image/png", "Карта.png");
const pdf = attachment("application/pdf", "Правила.pdf");

/**
 * A click on the name, as the browser dispatches it. Answers whether the row
 * took the default (following the link) away.
 */
function clickName(file: PostAttachment, init: MouseEventInit = {}) {
  const wrapper = mount(PostAttachmentRow, { props: { file } });
  const name = wrapper.get(".attachment-name").element;
  let preventedByRow = false;
  // Heard after the row's own handler: notes what the row did, then keeps
  // jsdom from following the link either way.
  name.addEventListener("click", (event) => {
    preventedByRow = event.defaultPrevented;
    event.preventDefault();
  });
  name.dispatchEvent(
    new MouseEvent("click", {
      bubbles: true,
      cancelable: true,
      button: 0,
      ...init,
    }),
  );
  return preventedByRow;
}

describe("the name of an attached file", () => {
  it("opens a picture over the page, not in a tab", () => {
    expect(clickName(picture)).toBe(true);
    expect(viewedImage.value).toEqual({ src: apiUrl(picture.url) });
  });

  it.each([
    ["Ctrl", { ctrlKey: true }],
    ["Cmd", { metaKey: true }],
    ["Shift", { shiftKey: true }],
    ["Alt", { altKey: true }],
  ])("leaves a click with %s to the browser", (_, init) => {
    expect(clickName(picture, init)).toBe(false);
    expect(viewedImage.value).toBeNull();
  });

  it("leaves a file that is not a picture to the browser", () => {
    expect(clickName(pdf)).toBe(false);
    expect(viewedImage.value).toBeNull();
  });

  it("keeps the address of the file on the name, for a new tab", () => {
    const wrapper = mount(PostAttachmentRow, { props: { file: picture } });

    const name = wrapper.get(".attachment-name");
    expect(name.attributes("href")).toBe(apiUrl(picture.url));
    expect(name.attributes("target")).toBe("_blank");
  });
});

/**
 * A link is copied as any link is, from the browser's own menu: the row has no
 * button of its own for it. Under the name stands its size alone.
 */
describe("the row of an attached file", () => {
  it("offers no copy of the link, only the name and the size", () => {
    const wrapper = mount(PostAttachmentRow, { props: { file: pdf } });

    expect(wrapper.find("button").exists()).toBe(false);
    expect(wrapper.text()).not.toMatch(/Скопировать|Скопировано/);
    expect(wrapper.text().replace(/\s+/g, " ").trim()).toBe(
      "Правила.pdf 2,0 КБ",
    );
  });
});

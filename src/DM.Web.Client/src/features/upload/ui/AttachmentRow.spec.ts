/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, afterEach, vi } from "vitest";
import { enableAutoUnmount, mount } from "@vue/test-utils";
import type { PostAttachment } from "@/entities/game";
import { apiUrl } from "@/shared/api/client";
import {
  closeImageViewer,
  viewedImage,
} from "@/shared/lib/composables/useImageViewer";
import { privateTooltipOver } from "@/shared/lib/testing/privateTooltip";
import AttachmentRow from "./AttachmentRow.vue";

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

// Answers whether the row took the default away.
function clickName(file: PostAttachment, init: MouseEventInit = {}) {
  const wrapper = mount(AttachmentRow, { props: { file } });
  const name = wrapper.get(".attachment-name").element;
  let preventedByRow = false;
  // Heard after the row's own handler; keeps jsdom from following the link.
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
    const wrapper = mount(AttachmentRow, { props: { file: picture } });

    const name = wrapper.get(".attachment-name");
    expect(name.attributes("href")).toBe(apiUrl(picture.url));
    expect(name.attributes("target")).toBe("_blank");
  });
});

describe("the row of an attached file", () => {
  it("shows the name and the size", () => {
    const wrapper = mount(AttachmentRow, { props: { file: pdf } });

    expect(wrapper.text().replace(/\s+/g, " ").trim()).toBe(
      "Правила.pdf 2,0 КБ",
    );
  });
});

describe("the row of a file kept to some readers", () => {
  afterEach(() => vi.useRealTimers());

  const marked = {
    ...picture,
    recipients: "DungeonMaster, Assistant, Вася",
    addressees: [{ id: "c-vasya", name: "Вася" }],
  };

  it("is green, as a hidden block is", () => {
    const wrapper = mount(AttachmentRow, { props: { file: marked } });

    expect(wrapper.get(".attachment-name").classes()).toContain(
      "private-message",
    );
    const open = mount(AttachmentRow, { props: { file: picture } });
    expect(open.get(".attachment-name").classes()).not.toContain(
      "private-message",
    );
  });

  // The hidden block's own tooltip, not a copy of it.
  it("names its readers on hover, as a hidden block does", async () => {
    vi.useFakeTimers();
    const wrapper = mount(AttachmentRow, {
      props: { file: marked },
      attachTo: document.body,
    });

    expect(
      await privateTooltipOver(wrapper.get(".attachment-name").element),
    ).toBe("Получатели: DungeonMaster, Assistant, Вася");
  });
});

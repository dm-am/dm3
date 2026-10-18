/**
 * @vitest-environment jsdom
 */

/**
 * An image of the global chat stands behind a spoiler only in the compact
 * layout (docs/architecture/BBCODE_RENDERING.md, "Картинки общего чата"). The
 * server wraps every one of them in a spoiler marked data-bb-gate="image",
 * since the layout is the reader's and the server does not know it, and this
 * component decides by the layout it draws the message in.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { mount, enableAutoUnmount } from "@vue/test-utils";
import { nextTick } from "vue";
import ChatMessage from "./ChatMessage.vue";
import type { MessageWithContinuation } from "@/shared/lib/utils/chat";

enableAutoUnmount(afterEach);

// A class and not an arrow: the header of a message is a one-line row
// (FitRow), which constructs its observer with `new`.
vi.stubGlobal(
  "ResizeObserver",
  class {
    observe = vi.fn();
    unobserve = vi.fn();
    disconnect = vi.fn();
  },
);

const IMAGE =
  '<img src="https://example.com/a.png" alt="" class="bb-image" data-bb-tag="img" />';

/** A message holding a gated image and a spoiler its author wrote. */
const message = {
  id: "m1",
  createdUtc: "2026-09-24T10:00:00Z",
  modifiedUtc: null,
  author: { username: "Alice" },
  text:
    '<a href="#" class="spoiler-head" data-bb-gate="image" data-swaptext="Скрыть содержимое">Показать содержимое</a>' +
    `<div class="spoiler" data-bb-gate="image">${IMAGE}</div>` +
    '<br /><a href="#" class="spoiler-head" data-swaptext="Скрыть содержимое">Показать содержимое</a>' +
    '<div class="spoiler">авторский спойлер</div>',
  isContinuation: false,
  likes: [],
} as unknown as MessageWithContinuation;

function heads(compact: boolean) {
  const wrapper = mount(ChatMessage, { props: { message, compact } });
  return { wrapper, heads: () => wrapper.findAll(".msg-text .spoiler-head") };
}

describe("an image of the global chat", () => {
  it("stands in place in the full layout, the author's spoiler stays", async () => {
    const { wrapper, heads: found } = heads(false);
    await nextTick();

    expect(found()).toHaveLength(1);
    expect(found()[0].attributes("data-bb-gate")).toBeUndefined();
    const image = wrapper.get('.msg-text img[src="https://example.com/a.png"]');
    expect(image.element.closest(".spoiler")).toBeNull();
  });

  it("stands behind its spoiler in the compact layout", async () => {
    const { heads: found } = heads(true);
    await nextTick();

    expect(found()).toHaveLength(2);
    expect(found()[0].attributes("data-bb-gate")).toBe("image");
  });

  it("follows a change of layout in a message already shown", async () => {
    const { wrapper, heads: found } = heads(false);
    await nextTick();
    expect(found()).toHaveLength(1);

    await wrapper.setProps({ compact: true });
    await nextTick();
    expect(found()).toHaveLength(2);
  });
});

/**
 * @vitest-environment jsdom
 */

/**
 * An image of the global chat in the compact layout is an ordinary spoiler
 * with the image in it (docs/architecture/BBCODE_RENDERING.md, "Картинки
 * общего чата"), and the one place that decides it is the message component.
 *
 * The page used to decide it a second time on its own: in the compact layout
 * it walked every message it had drawn, hid each image and put a link in its
 * place, "[описание]" or "[изображение]". So the spoiler the message had
 * opened showed that link instead of the picture. The message is stubbed here
 * with a drawn image in it, so what is read is only what the page does to a
 * message's body, and it must do nothing.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { nextTick } from "vue";
import { flushPromises, mount, enableAutoUnmount } from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";

const { api } = vi.hoisted(() => ({
  api: {
    getMessages: vi.fn(),
    getEvents: vi.fn(),
    getEvent: vi.fn(),
    getMessagesAround: vi.fn(),
    getMessagesNearDate: vi.fn(),
  },
}));

vi.mock("@/entities/global-chat/api/globalChatApi", () => ({ default: api }));

vi.mock("@/shared/lib/composables/useSignalR", async () => {
  const { ref } = await import("vue");
  return {
    useGlobalSignalR: () => ({
      connect: vi.fn(),
      disconnect: vi.fn(),
      onNotification: () => () => {},
      isConnected: ref(false),
    }),
  };
});

/**
 * The virtualizer, reduced to a window that shows the first rows: jsdom lays
 * nothing out, and the real one would draw no row at all.
 */
vi.mock("@/shared/lib/composables/useVirtualScroll", async () => {
  const { ref } = await import("vue");
  return {
    useVirtualScroll: () => ({
      virtualizer: ref(null),
      virtualItems: ref(
        Array.from({ length: 6 }, (_, index) => ({
          index,
          key: index,
          start: 0,
        })),
      ),
      totalSize: ref(0),
      measureElement: vi.fn(),
      scrollToIndex: vi.fn(),
      range: ref({ startIndex: 0, endIndex: 5 }),
    }),
  };
});

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: {}, query: {} }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

vi.mock("vue-final-modal", () => ({
  useModal: () => ({ open: vi.fn(), close: vi.fn() }),
}));

import GlobalChatPage from "./GlobalChatPage.vue";
import { useUiStore } from "@/shared/stores/ui";

enableAutoUnmount(afterEach);
afterEach(() => {
  vi.restoreAllMocks();
});

/** A message body as the message component leaves it: an opened image. */
const DrawnMessage = {
  template:
    '<div class="msg-text"><div class="spoiler" data-bb-gate="image">' +
    '<img src="https://example.com/a.png" alt="Описание" class="bb-image" />' +
    "</div></div>",
};

const message = (id: number) => ({
  id: `m${id}`,
  text: "<p>text</p>",
  createdUtc: new Date(2026, 2, 14, 12, 0, id).toISOString(),
  author: { username: "Alice" },
  likes: [],
});

let pinia: Pinia;

async function openChat() {
  const wrapper = mount(GlobalChatPage, {
    shallow: true,
    attachTo: document.body,
    global: {
      plugins: [pinia],
      stubs: { ChatFeed: false, ChatMessage: DrawnMessage },
    },
  });
  await flushPromises();
  await nextTick();
  return wrapper;
}

/** What the page did to the drawn images, if anything. */
const touched = (wrapper: Awaited<ReturnType<typeof openChat>>) => ({
  images: wrapper.findAll(".msg-text img").length,
  hidden: wrapper
    .findAll(".msg-text img")
    .filter((img) => (img.element as HTMLElement).style.display === "none")
    .length,
  links: wrapper.findAll(".compact-image-link").length,
});

describe("an image of the global chat in the compact layout", () => {
  beforeEach(() => {
    vi.stubGlobal(
      "ResizeObserver",
      class {
        observe() {}
        unobserve() {}
        disconnect() {}
      },
    );
    vi.stubGlobal(
      "IntersectionObserver",
      class {
        observe() {}
        unobserve() {}
        disconnect() {}
      },
    );
    pinia = createPinia();
    setActivePinia(pinia);
    api.getMessages.mockResolvedValue({
      data: {
        resources: [message(1), message(2)],
        paging: {
          prevCursor: null,
          nextCursor: null,
          hasPrev: false,
          hasNext: false,
        },
      },
      error: null,
    });
    api.getEvents.mockResolvedValue({ data: { resources: [] }, error: null });
  });

  it("is left to the message: the page swaps no image for a link", async () => {
    useUiStore().setMessageLayout("compact");
    const wrapper = await openChat();

    expect(touched(wrapper)).toEqual({ images: 2, hidden: 0, links: 0 });
  });

  it("is left alone when the reader switches to the compact layout", async () => {
    useUiStore().setMessageLayout("full");
    const wrapper = await openChat();

    useUiStore().setMessageLayout("compact");
    await flushPromises();
    await nextTick();

    expect(touched(wrapper)).toEqual({ images: 2, hidden: 0, links: 0 });
  });
});

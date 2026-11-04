/**
 * Where the reader of a feed stands. A message the feed brings into view is
 * marked a moment after the scroll, on a timer, and the timer can outlive the
 * feed: the page is left before it fires.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { defineComponent, h, ref } from "vue";
import { mount } from "@vue/test-utils";
import { useFeedPosition } from "./useFeedPosition";

afterEach(() => {
  vi.useRealTimers();
  document.body.innerHTML = "";
});

/** A feed on the page whose list holds the message m1. */
function mountFeed() {
  let position!: ReturnType<typeof useFeedPosition>;
  const Feed = defineComponent({
    setup() {
      const list = ref<HTMLElement | null>(null);
      position = useFeedPosition({
        list,
        rows: ref([]),
        scrollToIndex: vi.fn(),
      });
      return () => h("div", { ref: list }, [h("div", { id: "msg-m1" })]);
    },
  });
  const wrapper = mount(Feed, { attachTo: document.body });
  return { wrapper, position };
}

describe("useFeedPosition", () => {
  it("marks the arrival of the message it brings into view", async () => {
    vi.useFakeTimers();
    const { position } = mountFeed();

    position.scrollToMessage("m1");
    await vi.advanceTimersByTimeAsync(100);

    const arrived = document.getElementById("msg-m1");
    expect(arrived?.classList.contains("highlight-unread")).toBe(true);
  });

  it("marks nothing once the feed is gone", async () => {
    vi.useFakeTimers();
    const { wrapper, position } = mountFeed();

    position.scrollToMessage("m1");
    wrapper.unmount();
    const elsewhere = document.body.appendChild(document.createElement("div"));
    elsewhere.id = "msg-m1";
    await vi.advanceTimersByTimeAsync(100);

    expect(elsewhere.classList.contains("highlight-unread")).toBe(false);
  });
});

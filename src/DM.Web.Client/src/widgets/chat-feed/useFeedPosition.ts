import { nextTick, ref, type Ref } from "vue";
import { isNearBottom } from "@/shared/lib/utils/liveTail";
import {
  isDateSeparator,
  type MessageOrSeparator,
} from "@/shared/lib/utils/chat";
import { markArrival } from "@/shared/lib/composables/useScrollToElement";
import type { VirtualScrollReturn } from "@/shared/lib/composables/useVirtualScroll";

/** Where the reader of a feed stands, and the two ways the page moves him. */
export function useFeedPosition(options: {
  /** The scrolling list of the feed. */
  list: Ref<HTMLElement | null>;
  /** The rows of the list, date separators included, oldest first. */
  rows: Ref<MessageOrSeparator[]>;
  /** The virtualizer's own scroll, which knows rows not measured yet. */
  scrollToIndex: VirtualScrollReturn["scrollToIndex"];
}) {
  const { list, rows, scrollToIndex } = options;

  const isScrolling = ref(false);
  let scrollEndTimeout: ReturnType<typeof setTimeout> | null = null;

  /** Set a viewport up, cleared near the bottom: the gap keeps it from flickering. */
  const isFarFromBottom = ref(false);

  function measureDeparture() {
    const el = list.value;
    if (!el) return;
    const distance = el.scrollHeight - el.scrollTop - el.clientHeight;
    if (distance > el.clientHeight) isFarFromBottom.value = true;
    else if (isNearBottom(el)) isFarFromBottom.value = false;
  }

  function settleScrolling() {
    if (scrollEndTimeout) clearTimeout(scrollEndTimeout);
    scrollEndTimeout = setTimeout(() => {
      isScrolling.value = false;
      scrollEndTimeout = null;
    }, 150);
  }

  /** The reader stands at the end of the loaded window, measured now. */
  function atBottom(): boolean {
    return list.value ? isNearBottom(list.value) : true;
  }

  // Through the virtualizer: rows not measured yet make scrollHeight undershoot.
  function scrollToBottom() {
    nextTick(() => {
      const last = rows.value.length - 1;
      if (last >= 0) scrollToIndex(last, { align: "end" });
    });
  }

  /** Brings a message into view and marks its arrival with the site's pulse. */
  function scrollToMessage(id: string, align: "center" | "start" = "center") {
    const index = rows.value.findIndex(
      (item) => !isDateSeparator(item) && item.id === id,
    );
    if (index >= 0) scrollToIndex(index, { align });
    // The virtualizer draws the row after it has scrolled to it.
    nextTick(() => {
      setTimeout(() => {
        // Gone with the feed: the page may be left before the timer fires.
        if (!list.value) return;
        const element = document.getElementById(`msg-${id}`);
        if (element) markArrival(element);
      }, 100);
    });
  }

  return {
    isScrolling,
    isFarFromBottom,
    measureDeparture,
    settleScrolling,
    atBottom,
    scrollToBottom,
    scrollToMessage,
  };
}

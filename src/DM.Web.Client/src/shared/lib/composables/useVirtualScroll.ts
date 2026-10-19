/** @tanstack/vue-virtual for lists of measured, dynamic-height rows. */

import { computed, type Ref, type ComputedRef } from "vue";
import {
  useVirtualizer,
  type VirtualItem,
  type Virtualizer,
} from "@tanstack/vue-virtual";

export interface VirtualScrollOptions {
  /** Total number of items */
  count: Ref<number> | ComputedRef<number>;
  /** Scroll container element */
  container: Ref<HTMLElement | null>;
  /** Estimated row height in px (default: 80) */
  estimateSize?: number;
  /** Number of items to render above/below viewport (default: 10) */
  overscan?: number;
  /**
   * Stable key per item, for a list that is prepended to, so measurements stay
   * with the item and not the index. Defaults to the index.
   */
  getItemKey?: (index: number) => string | number;
}

export interface VirtualScrollReturn {
  virtualizer: Ref<Virtualizer<HTMLElement, Element>>;
  /** Currently visible virtual items */
  virtualItems: ComputedRef<VirtualItem[]>;
  /** Total height of all items */
  totalSize: ComputedRef<number>;
  /** Ref callback for measuring element heights — pass as :ref on each virtual row */
  measureElement: (el: Element) => void;
  /** Scroll to a specific index */
  scrollToIndex: (
    index: number,
    options?: { align?: "start" | "center" | "end" | "auto" },
  ) => void;
  /** Get the visible range */
  range: ComputedRef<{ startIndex: number; endIndex: number } | null>;
}

export function useVirtualScroll(
  options: VirtualScrollOptions,
): VirtualScrollReturn {
  const {
    count,
    container,
    estimateSize = 80,
    overscan = 10,
    getItemKey,
  } = options;

  // Options must be reactive (computed) for @tanstack/vue-virtual to re-measure
  // when count changes or scroll element becomes available after mount
  const virtualizer = useVirtualizer(
    computed(() => ({
      count: count.value,
      getScrollElement: () => container.value,
      estimateSize: () => estimateSize,
      overscan,
      ...(getItemKey ? { getItemKey } : {}),
    })),
  );

  const virtualItems = computed(() => virtualizer.value.getVirtualItems());
  const totalSize = computed(() => virtualizer.value.getTotalSize());
  const range = computed(() => {
    const r = virtualizer.value.range;
    return r ? { startIndex: r.startIndex, endIndex: r.endIndex } : null;
  });

  function scrollToIndex(
    index: number,
    opts?: { align?: "start" | "center" | "end" | "auto" },
  ) {
    virtualizer.value.scrollToIndex(index, opts);
  }

  // Always call through .value to reference current virtualizer instance
  function measureElement(el: Element) {
    virtualizer.value.measureElement(el);
  }

  return {
    virtualizer,
    virtualItems,
    totalSize,
    measureElement,
    scrollToIndex,
    range,
  };
}

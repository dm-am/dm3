/**
 * Composable wrapping @tanstack/vue-virtual for dynamic-height lists.
 *
 * Provides virtual scrolling with:
 * - Dynamic row heights via measureElement
 * - Configurable overscan for smooth scrolling
 * - Reactive count and scroll element
 */

import { computed, type Ref, type ComputedRef } from "vue";
import { useVirtualizer } from "@tanstack/vue-virtual";

export interface VirtualScrollOptions {
  /** Total number of items */
  count: Ref<number> | ComputedRef<number>;
  /** Scroll container element */
  container: Ref<HTMLElement | null>;
  /** Estimated row height in px (default: 80) */
  estimateSize?: number;
  /** Number of items to render above/below viewport (default: 10) */
  overscan?: number;
}

export interface VirtualScrollReturn {
  /** The virtualizer instance */
  virtualizer: ReturnType<typeof useVirtualizer>;
  /** Currently visible virtual items */
  virtualItems: ComputedRef<ReturnType<ReturnType<typeof useVirtualizer>["getVirtualItems"]>>;
  /** Total height of all items */
  totalSize: ComputedRef<number>;
  /** Ref callback for measuring element heights — pass as :ref on each virtual row */
  measureElement: (el: HTMLElement) => void;
  /** Scroll to a specific index */
  scrollToIndex: (index: number, options?: { align?: "start" | "center" | "end" | "auto" }) => void;
  /** Get the visible range */
  range: ComputedRef<{ startIndex: number; endIndex: number } | null>;
}

export function useVirtualScroll(options: VirtualScrollOptions): VirtualScrollReturn {
  const {
    count,
    container,
    estimateSize = 80,
    overscan = 10,
  } = options;

  // Options must be reactive (computed) for @tanstack/vue-virtual to re-measure
  // when count changes or scroll element becomes available after mount
  const virtualizer = useVirtualizer(computed(() => ({
    count: count.value,
    getScrollElement: () => container.value,
    estimateSize: () => estimateSize,
    overscan,
  })));

  const virtualItems = computed(() => virtualizer.value.getVirtualItems());
  const totalSize = computed(() => virtualizer.value.getTotalSize());
  const range = computed(() => {
    const r = virtualizer.value.range;
    return r ? { startIndex: r.startIndex, endIndex: r.endIndex } : null;
  });

  function scrollToIndex(index: number, opts?: { align?: "start" | "center" | "end" | "auto" }) {
    virtualizer.value.scrollToIndex(index, opts);
  }

  // Always call through .value to reference current virtualizer instance
  function measureElement(el: HTMLElement) {
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

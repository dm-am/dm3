import {
  getCurrentInstance,
  nextTick,
  onBeforeUnmount,
  ref,
  type Ref,
} from "vue";

/**
 * Sentinel-driven paging of a chat's message list, shared by both chats: one
 * load per direction, the viewport anchored while older messages are prepended,
 * observers stopped during a jump to a message, a failed direction parked.
 */
export interface InfiniteScrollDirection {
  /** Mounted only while more history exists, so mostly null. */
  sentinel: Ref<HTMLElement | null>;

  /** Asked on every intersection rather than cached: paging state moves. */
  hasMore: () => boolean;

  /** Fetches and merges one page. Reporting a failure is the loader's own job. */
  load: () => Promise<unknown>;

  /**
   * The last attempt failed: the direction parks until the sentinel's retry,
   * or every intersection would ask again.
   */
  failed?: () => boolean;
}

export interface AnchoredInfiniteScrollOptions {
  /** The scrolling element: the observers' root, and what anchoring adjusts. */
  container: Ref<HTMLElement | null>;

  /** Older messages, above the viewport. Every chat list has this end. */
  older: InfiniteScrollDirection;

  /** Newer messages, below the viewport; the messenger's list has none. */
  newer?: InfiniteScrollDirection;
}

/** A sentinel counts as reached this far outside the container. */
const SENTINEL_MARGIN_PX = 100;

/** How long a jump-to-message landing keeps moving the viewport (suspend()). */
export const LANDING_SCROLL_MS = 600;

export function useAnchoredInfiniteScroll(
  options: AnchoredInfiniteScrollOptions,
) {
  const isLoadingOlder = ref(false);
  const isLoadingNewer = ref(false);

  let olderObserver: IntersectionObserver | null = null;
  let newerObserver: IntersectionObserver | null = null;
  let suspended = false;

  /**
   * Loads one page of older messages and puts the viewport back. Also the
   * sentinel's retry, which is what clears `failed`, so it is not checked here.
   */
  async function loadOlder(): Promise<void> {
    if (isLoadingOlder.value || !options.older.hasMore()) return;

    const container = options.container.value;
    if (!container) return;

    isLoadingOlder.value = true;
    const scrollHeightBefore = container.scrollHeight;

    try {
      await options.older.load();
    } finally {
      nextTick(() => {
        // The height just added is the offset that keeps the first visible message.
        container.scrollTop = container.scrollHeight - scrollHeightBefore;
        // Released once scrollTop is corrected, or the sentinel queues another page.
        isLoadingOlder.value = false;
      });
    }
  }

  /** No anchoring counterpart: appending below the viewport does not move it. */
  async function loadNewer(): Promise<void> {
    const newer = options.newer;
    if (!newer || isLoadingNewer.value || !newer.hasMore()) return;

    isLoadingNewer.value = true;
    try {
      await newer.load();
    } finally {
      isLoadingNewer.value = false;
    }
  }

  /** Only suspension and a parked failure here: the loaders own the rest. */
  function observeDirection(
    root: HTMLElement,
    direction: InfiniteScrollDirection,
    load: () => Promise<void>,
    rootMargin: string,
  ): IntersectionObserver | null {
    const sentinel = direction.sentinel.value;
    if (!sentinel) return null;

    const observer = new IntersectionObserver(
      (entries) => {
        if (suspended || !entries[0].isIntersecting || direction.failed?.())
          return;
        load();
      },
      { root, rootMargin, threshold: 0 },
    );
    observer.observe(sentinel);
    return observer;
  }

  /** Safe to call again: the previous observers are dropped first. */
  function setupInfiniteScroll(): void {
    const root = options.container.value;
    if (!root) return;

    cleanupInfiniteScroll();

    olderObserver = observeDirection(
      root,
      options.older,
      loadOlder,
      `${SENTINEL_MARGIN_PX}px 0px 0px 0px`,
    );

    if (options.newer) {
      newerObserver = observeDirection(
        root,
        options.newer,
        loadNewer,
        `0px 0px ${SENTINEL_MARGIN_PX}px 0px`,
      );
    }
  }

  function cleanupInfiniteScroll(): void {
    olderObserver?.disconnect();
    newerObserver?.disconnect();
    olderObserver = null;
    newerObserver = null;
  }

  /** While something else drives the viewport; callers resume once it settles. */
  function suspend(): void {
    suspended = true;
  }

  function resume(): void {
    suspended = false;
  }

  // Guarded so the composable can be exercised on its own: registering the
  // hook outside setup() is a warning rather than a mechanism.
  if (getCurrentInstance()) onBeforeUnmount(cleanupInfiniteScroll);

  return {
    isLoadingOlder,
    isLoadingNewer,
    loadOlder,
    loadNewer,
    setupInfiniteScroll,
    cleanupInfiniteScroll,
    suspend,
    resume,
  };
}

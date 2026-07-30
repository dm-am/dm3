import {
  getCurrentInstance,
  nextTick,
  onBeforeUnmount,
  ref,
  type Ref,
} from "vue";

/**
 * Sentinel-driven paging for a chat's message list: the observers, one
 * in-flight load per direction, and the scroll anchoring that keeps the
 * viewport still while older messages are prepended above it.
 *
 * Both chat views grew their own copy, and the copies drifted the same way the
 * hover toolbar did. The global chat learned two guards the messenger never
 * got: it stops the observers while a jump-to-message animation is sweeping
 * the list, and it parks a direction whose last request failed instead of
 * asking the server again on every intersection. Extracting the mechanism
 * without those guards would have been the same divergence in a new place, so
 * both live here and both pages get them.
 *
 * What stays in the pages is the per-message domain work — like, edit, delete,
 * warn — one API call each, different per view.
 */
export interface InfiniteScrollDirection {
  /**
   * The element the observer watches. Chats mount it only while more history
   * exists, so it is null for most of a list's life and the observer attaches
   * when setup runs with it present.
   */
  sentinel: Ref<HTMLElement | null>;

  /** Asked on every intersection rather than cached: paging state moves. */
  hasMore: () => boolean;

  /** Fetches and merges one page. Reporting a failure is the loader's own job. */
  load: () => Promise<unknown>;

  /**
   * Whether the last attempt failed. Without this an observer turns a single
   * refused request into a loop: the sentinel is still on screen, so every
   * intersection asks again. A failed direction parks until the reader asks
   * for it explicitly through the sentinel's own retry.
   */
  failed?: () => boolean;
}

export interface AnchoredInfiniteScrollOptions {
  /** The scrolling element: the observers' root, and what anchoring adjusts. */
  container: Ref<HTMLElement | null>;

  /** Older messages, above the viewport. Every chat list has this end. */
  older: InfiniteScrollDirection;

  /**
   * Newer messages, below the viewport. Omitted by lists that already end at
   * the newest message — the messenger's does, so it has no bottom sentinel.
   */
  newer?: InfiniteScrollDirection;
}

/**
 * How far outside the container a sentinel counts as reached. The page loads
 * before the reader hits the actual edge, so the next screenful is usually
 * already there.
 */
const SENTINEL_MARGIN_PX = 100;

/**
 * How long a jump-to-message landing takes to settle: the virtualizer scrolls
 * to the row and then remeasures, so the viewport keeps moving after the call
 * returns. Both chats wait this out before resuming — see suspend().
 */
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
   * Loads one page of older messages and puts the viewport back where the
   * reader left it. Prepending grows the list above the visible area, which
   * would otherwise carry whatever they were reading downwards out of sight.
   *
   * Also the path behind the sentinel's retry button, so a manual attempt
   * anchors identically — hence no check on `failed` here: the reader asking
   * again is exactly what clears that state.
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
        // The sentinel only intersects within SENTINEL_MARGIN_PX of the top,
        // so the height the page just added is the offset that leaves the
        // previously first visible message where it was.
        container.scrollTop = container.scrollHeight - scrollHeightBefore;
        // Released here rather than right after the await: until the DOM has
        // settled and scrollTop is corrected, the sentinel is still inside the
        // root and a second intersection would queue another page.
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

  /**
   * The callback keeps only the guards it alone owns — suspension and a parked
   * failure. In-flight and hasMore belong to the loaders, which the retry
   * buttons enter directly and would otherwise bypass.
   */
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

  /**
   * Attaches the observers to the sentinels currently in the DOM. Safe to call
   * again — the messenger re-runs it on every chat switch, and dropping the
   * previous observers first is what keeps that from stacking them.
   */
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

  /**
   * Stops the observers while something else drives the viewport. Landing on a
   * linked message scrolls through the list, and every sentinel that flies
   * past on the way would queue a page nobody asked for. Callers resume once
   * their scroll has settled.
   */
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

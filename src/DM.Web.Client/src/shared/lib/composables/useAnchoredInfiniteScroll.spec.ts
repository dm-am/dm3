import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { nextTick, ref } from "vue";
import { useAnchoredInfiniteScroll } from "./useAnchoredInfiniteScroll";

/** One observer the code under test created, with the browser's part faked. */
type FakeObserver = {
  root: unknown;
  rootMargin: string;
  targets: unknown[];
  disconnected: boolean;
  /** Reports the sentinel as reached, the way a real scroll would. */
  reach: () => void;
};

let observers: FakeObserver[] = [];

/** An element with write access to the two properties jsdom leaves inert. */
type ScrollBox = HTMLElement & { scrollHeight: number; scrollTop: number };

/**
 * A scroll container whose height the loaders can grow and whose offset can be
 * written: jsdom lays nothing out, so anchoring would otherwise have nothing to
 * measure or move.
 */
function container(scrollHeight: number): ScrollBox {
  const el = document.createElement("div") as ScrollBox;
  Object.defineProperty(el, "scrollHeight", {
    value: scrollHeight,
    writable: true,
    configurable: true,
  });
  Object.defineProperty(el, "scrollTop", {
    value: 0,
    writable: true,
    configurable: true,
  });
  return el;
}

const sentinel = () => ref(document.createElement("div"));

describe("useAnchoredInfiniteScroll", () => {
  beforeEach(() => {
    observers = [];
    vi.stubGlobal(
      "IntersectionObserver",
      class {
        private entry: FakeObserver;

        constructor(
          callback: (entries: { isIntersecting: boolean }[]) => void,
          options: { root?: unknown; rootMargin?: string },
        ) {
          this.entry = {
            root: options.root ?? null,
            rootMargin: options.rootMargin ?? "",
            targets: [],
            disconnected: false,
            reach: () => callback([{ isIntersecting: true }]),
          };
          observers.push(this.entry);
        }

        observe(target: unknown) {
          this.entry.targets.push(target);
        }

        disconnect() {
          this.entry.disconnected = true;
        }

        unobserve() {}

        takeRecords() {
          return [];
        }
      },
    );
  });

  afterEach(() => vi.unstubAllGlobals());

  it("puts the viewport back where it was after older messages are prepended", async () => {
    const box = container(1000);
    const scroll = useAnchoredInfiniteScroll({
      container: ref(box),
      older: {
        sentinel: sentinel(),
        hasMore: () => true,
        load: async () => {
          box.scrollHeight = 1400;
        },
      },
    });

    await scroll.loadOlder();
    await nextTick();

    // Without this the 400px of history that arrived above the viewport would
    // carry whatever was being read downwards, out of sight.
    expect(box.scrollTop).toBe(400);
  });

  it("does not start a second load while one is in flight", async () => {
    let finish = () => {};
    const load = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          finish = resolve;
        }),
    );
    const scroll = useAnchoredInfiniteScroll({
      container: ref(container(1000)),
      older: { sentinel: sentinel(), hasMore: () => true, load },
    });
    scroll.setupInfiniteScroll();

    scroll.loadOlder();
    scroll.loadOlder();
    observers[0].reach();

    expect(load).toHaveBeenCalledTimes(1);

    finish();
    await nextTick();
  });

  it("corrects the offset before it releases the in-flight flag", async () => {
    let offset = 0;
    let stillLoadingWhenAnchored: boolean | null = null;
    const box = container(1000);
    Object.defineProperty(box, "scrollTop", {
      get: () => offset,
      set: (value: number) => {
        offset = value;
        stillLoadingWhenAnchored = scroll.isLoadingOlder.value;
      },
    });

    const scroll = useAnchoredInfiniteScroll({
      container: ref(box),
      older: {
        sentinel: sentinel(),
        hasMore: () => true,
        load: async () => {
          box.scrollHeight = 1400;
        },
      },
    });

    await scroll.loadOlder();
    await nextTick();

    // The sentinel is still inside the root until the offset is corrected, so
    // releasing the flag any earlier would let that intersection queue a page.
    expect(stillLoadingWhenAnchored).toBe(true);
    expect(scroll.isLoadingOlder.value).toBe(false);
  });

  it("stops asking after a failure, and asks again when the reader does", async () => {
    const load = vi.fn(async () => {});
    let failed = true;
    const scroll = useAnchoredInfiniteScroll({
      container: ref(container(1000)),
      older: {
        sentinel: sentinel(),
        hasMore: () => true,
        load,
        failed: () => failed,
      },
    });
    scroll.setupInfiniteScroll();

    observers[0].reach();
    // The sentinel stays on screen after a failed page, so an unguarded
    // observer would keep hammering a server that just refused.
    expect(load).not.toHaveBeenCalled();

    // The retry button enters the loader directly: being asked again is what
    // clears the failure, so the loader must not consult it.
    await scroll.loadOlder();
    expect(load).toHaveBeenCalledTimes(1);

    failed = false;
    observers[0].reach();
    await nextTick();
    expect(load).toHaveBeenCalledTimes(2);
  });

  it("loads nothing when the direction says there is nothing left", async () => {
    const load = vi.fn(async () => {});
    const scroll = useAnchoredInfiniteScroll({
      container: ref(container(1000)),
      older: { sentinel: sentinel(), hasMore: () => false, load },
    });
    scroll.setupInfiniteScroll();

    observers[0].reach();
    await scroll.loadOlder();

    expect(load).not.toHaveBeenCalled();
  });

  it("holds both directions while a jump-to-message landing is scrolling", async () => {
    const older = vi.fn(async () => {});
    const newer = vi.fn(async () => {});
    const scroll = useAnchoredInfiniteScroll({
      container: ref(container(1000)),
      older: { sentinel: sentinel(), hasMore: () => true, load: older },
      newer: { sentinel: sentinel(), hasMore: () => true, load: newer },
    });
    scroll.setupInfiniteScroll();

    scroll.suspend();
    observers[0].reach();
    observers[1].reach();
    expect(older).not.toHaveBeenCalled();
    expect(newer).not.toHaveBeenCalled();

    scroll.resume();
    observers[0].reach();
    observers[1].reach();
    await nextTick();
    expect(older).toHaveBeenCalledTimes(1);
    expect(newer).toHaveBeenCalledTimes(1);
  });

  it("works without a newer direction at all", async () => {
    const scroll = useAnchoredInfiniteScroll({
      container: ref(container(1000)),
      older: {
        sentinel: sentinel(),
        hasMore: () => true,
        load: async () => {},
      },
    });
    scroll.setupInfiniteScroll();

    // The messenger's list ends at the newest message: no bottom sentinel to
    // watch, and its retry path never fires.
    expect(observers).toHaveLength(1);
    expect(observers[0].rootMargin).toBe("100px 0px 0px 0px");

    await expect(scroll.loadNewer()).resolves.toBeUndefined();
    expect(scroll.isLoadingNewer.value).toBe(false);
  });

  it("does not stack observers when set up again", () => {
    const scroll = useAnchoredInfiniteScroll({
      container: ref(container(1000)),
      older: {
        sentinel: sentinel(),
        hasMore: () => true,
        load: async () => {},
      },
    });

    // The messenger re-runs setup on every chat switch.
    scroll.setupInfiniteScroll();
    scroll.setupInfiniteScroll();

    expect(observers[0].disconnected).toBe(true);
    expect(observers[1].disconnected).toBe(false);
  });

  it("watches the container it was given as the observer root", () => {
    const box = container(1000);
    const top = sentinel();
    const scroll = useAnchoredInfiniteScroll({
      container: ref(box),
      older: { sentinel: top, hasMore: () => true, load: async () => {} },
    });
    scroll.setupInfiniteScroll();

    expect(observers[0].root).toBe(box);
    expect(observers[0].targets).toEqual([top.value]);
  });
});

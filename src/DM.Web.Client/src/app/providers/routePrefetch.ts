/**
 * Fetches a page's chunk while the reader aims at its link, so the click finds
 * it loaded. One set of bubbling listeners on the document covers every link,
 * RouterLink or not. loadRouteLocation writes the component back into the
 * record, so the navigation makes no second request. Speculative traffic
 * competes with the page being opened, hence the waits and the refusals.
 */
import { loadRouteLocation, type RouteLocationResolved } from "vue-router";

import { onSlowConnection, savingData } from "@/shared/lib/utils/connection";
import { toInternalPath } from "@/shared/lib/utils/internalUrl";
import router from "./router";
import { cancelPendingReload } from "./staleBundle";

/** A pointer crossing a column of links spends a few ms on each; a stop is longer. */
const HOVER_DWELL_MS = 65;

/**
 * Longer than the pointer's: tabbing past a column of links focuses each, and
 * the moderation panel alone is ten page graphs; a held Tab repeats faster.
 */
const FOCUS_DWELL_MS = 150;

/** The anchor an event happened inside, if it happened inside one. */
function linkFrom(event: Event): HTMLAnchorElement | null {
  const target = event.target;
  return target instanceof Element
    ? target.closest<HTMLAnchorElement>("a[href]")
    : null;
}

/** The last of the matched records: the page itself, under its shells. */
function leafOf(matched: readonly unknown[]): unknown {
  return matched[matched.length - 1];
}

/**
 * The route a click would open, or null: another tab or a download, an in-page
 * anchor, another origin, an address that is not our route, the page open now.
 */
function routeBehind(
  link: HTMLAnchorElement,
  catchAll: unknown,
): RouteLocationResolved | null {
  if (link.target !== "" && link.target !== "_self") return null;
  if (link.hasAttribute("download")) return null;
  if ((link.getAttribute("href") ?? "").startsWith("#")) return null;

  const path = toInternalPath(link.href);
  if (path === null) return null;

  const resolved = router.resolve(path);
  if (leafOf(resolved.matched) === catchAll) return null;
  if (resolved.path === router.currentRoute.value.path) return null;

  return resolved;
}

/** The matched records, not the address: many profiles share one chunk. */
function chunkKey(route: RouteLocationResolved): string {
  return route.matched.map((record) => record.path).join(">");
}

/**
 * A guess while a navigation is in flight shares the channel with the page
 * being opened; currentRoute changes only when it ends, so the router is asked.
 */
function watchNavigation() {
  let navigating = false;

  const stopWatchingStart = router.beforeEach(() => {
    navigating = true;
  });
  // A guard that throws reaches neither afterEach nor the page, and a flag left
  // standing would stop prefetching for the session.
  const stopWatchingEnd = router.afterEach(() => {
    navigating = false;
  });
  const stopWatchingError = router.onError(() => {
    navigating = false;
  });

  return {
    inFlight: (): boolean => navigating,
    stop: (): void => {
      stopWatchingStart();
      stopWatchingEnd();
      stopWatchingError();
    },
  };
}

/** Asks for the chunks behind a link once per page load. */
function createPrefetcher(inFlight: () => boolean) {
  /** Chunk keys already asked for. Never fetched twice on one page load. */
  const requested = new Set<string>();
  // The 404 record, found by an address no route owns and compared by
  // identity, so renaming the catch-all does not turn the check off.
  const catchAll = leafOf(router.resolve("/__no-route-owns-this__").matched);

  return function prefetch(link: HTMLAnchorElement): void {
    // Per prefetch: a phone leaving wifi turns these on mid-session.
    if (savingData() || onSlowConnection()) return;
    // Dropped rather than deferred: the reader is opening something else.
    if (inFlight()) return;

    const route = routeBehind(link, catchAll);
    if (route === null) return;

    const key = chunkKey(route);
    if (requested.has(key)) return;
    // Before the request: a resting pointer raises more than one event.
    requested.add(key);

    void loadRouteLocation(route).catch(() => {
      // Free to fail: the navigation asks again and reports its own failure,
      // and a guess is no reason to reload the page the reader is reading.
      cancelPendingReload();
      requested.delete(key);
    });
  };
}

/**
 * One wait for the whole site: a reader aims at one thing at a time, so a new
 * aim replaces the pending one, whichever way it arrived.
 */
function createAim(arrive: (link: HTMLAnchorElement) => void) {
  let dwellTimer: ReturnType<typeof setTimeout> | null = null;
  /** The link currently being aimed at, by pointer or by focus. */
  let aimedAt: HTMLAnchorElement | null = null;

  function clearDwellTimer(): void {
    if (dwellTimer !== null) clearTimeout(dwellTimer);
    dwellTimer = null;
  }

  /** Takes a link as the current aim and starts its wait. */
  function aimAt(link: HTMLAnchorElement, dwellMs: number): void {
    // Re-arming would restart the wait on every repeated event.
    if (link === aimedAt) return;

    clearDwellTimer();
    aimedAt = link;
    dwellTimer = setTimeout(() => {
      dwellTimer = null;
      arrive(link);
    }, dwellMs);
  }

  /** Gives up the current aim, if it is this link. */
  function stopAiming(link: HTMLAnchorElement): void {
    if (link !== aimedAt) return;
    clearDwellTimer();
    aimedAt = null;
  }

  return { aimAt, stopAiming, clearDwellTimer };
}

/** Returns the teardown, which only the test calls: the app outlives it. */
export function installRoutePrefetch(): () => void {
  const navigation = watchNavigation();
  const prefetch = createPrefetcher(navigation.inFlight);
  const { aimAt, stopAiming, clearDwellTimer } = createAim(prefetch);

  function onPointerOver(event: Event): void {
    const link = linkFrom(event);
    if (link !== null) aimAt(link, HOVER_DWELL_MS);
  }

  function onPointerOut(event: Event): void {
    const link = linkFrom(event);
    if (link === null) return;
    // mouseout also fires onto an element inside the link.
    const movedTo = (event as MouseEvent).relatedTarget;
    if (movedTo instanceof Node && link.contains(movedTo)) return;

    stopAiming(link);
  }

  function onFocusIn(event: Event): void {
    const link = linkFrom(event);
    if (link !== null) aimAt(link, FOCUS_DWELL_MS);
  }

  function onFocusOut(event: Event): void {
    // Focus may leave for something that is not a link, and no focusin follows.
    const link = linkFrom(event);
    if (link !== null) stopAiming(link);
  }

  const options: AddEventListenerOptions = { passive: true };
  document.addEventListener("mouseover", onPointerOver, options);
  document.addEventListener("mouseout", onPointerOut, options);
  document.addEventListener("focusin", onFocusIn, options);
  document.addEventListener("focusout", onFocusOut, options);

  return () => {
    clearDwellTimer();
    document.removeEventListener("mouseover", onPointerOver, options);
    document.removeEventListener("mouseout", onPointerOut, options);
    document.removeEventListener("focusin", onFocusIn, options);
    document.removeEventListener("focusout", onFocusOut, options);
    navigation.stop();
  };
}

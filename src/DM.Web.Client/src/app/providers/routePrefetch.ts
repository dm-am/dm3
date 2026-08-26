/**
 * Fetching a page's chunk while the reader is still deciding to open it.
 *
 * Every page under the shell is a lazy `import()`, so the first thing a click
 * on a link buys is a round trip: the chunk is requested only once the
 * navigation has already started, and nothing renders until it lands. A reader
 * whose pointer is resting on a link has told us which chunk that will be, a
 * few hundred milliseconds before the click — enough for the request to be
 * finished by the time it arrives.
 *
 * ONE set of listeners on the document, not a wrapper component and not a
 * directive. There are a couple of hundred links across a hundred components;
 * either of those mechanisms would have to reach every one of them, would be
 * forgotten by the hundred-and-first, and would put a component between the
 * reader and every link on the site for a behaviour that renders nothing at
 * all. Every event used here bubbles, so the whole site costs five handlers no
 * matter how many links a page draws, and markup written tomorrow is covered
 * the day it is written — including links that are not RouterLink, which the
 * two other mechanisms cannot see.
 *
 * The chunks are loaded through vue-router's own `loadRouteLocation`, which is
 * what the router itself uses to resolve a lazy route component: it writes the
 * resolved component back into the route record, so the navigation that
 * follows finds it already there and no second request is made anywhere.
 *
 * Everything below the fetching is about NOT fetching. Speculative traffic
 * competes with the traffic the reader asked for, so the wrong guess is not
 * merely wasted — it is subtracted from the page they are actually opening.
 * Hence a wait before believing an aim, a pause while a navigation is in
 * flight, and a list of links that name no chunk worth having.
 *
 * Nothing here draws, moves or measures anything. The only observable
 * difference is WHEN a chunk is asked for.
 */
import { loadRouteLocation, type RouteLocationResolved } from "vue-router";

import { onSlowConnection, savingData } from "@/shared/lib/utils/connection";
import { toInternalPath } from "@/shared/lib/utils/internalUrl";
import router from "./router";

/**
 * How long a pointer has to rest on a link before its chunk is worth asking
 * for.
 *
 * Without a delay every sweep of the mouse across the sidebar would fetch a
 * dozen pages the reader never looked at, which spends their traffic to make
 * ours look fast. A pointer crossing a column of links on the way somewhere
 * else spends a handful of milliseconds on each; a pointer that has stopped on
 * one is aiming at it, and the click is still a couple of hundred milliseconds
 * away. Sixty-five sits in that gap: long enough that a crossing is not a
 * request, short enough that the head start is nearly the whole of it.
 */
const HOVER_DWELL_MS = 65;

/**
 * The same wait, for keyboard focus. Longer than the pointer's, and the reason
 * is the reverse of the pointer's.
 *
 * A mouse sweep is involuntary — the reader never chose the links it crossed —
 * and a few tens of milliseconds tell a crossing from a stop. Every focus, by
 * contrast, is a deliberate keypress. That was the argument for fetching on
 * focus at once, and it was wrong about the case that matters: a reader tabbing
 * to something PAST a column of links presses that key once per link on the
 * way. The moderation panel is ten top-level routes in a row, so one pass down
 * it is ten whole page graphs, where a mouse sweep down the same column costs
 * at most one. The keyboard was handed exactly the cascade the pointer's wait
 * exists to prevent.
 *
 * What has to be told apart here is a step of a traversal from an arrival, and
 * time is what separates them: a held Tab repeats every few tens of
 * milliseconds, a steady pass is not much slower, and a reader who has arrived
 * still has to decide and press Enter. This sits above the first two. It is a
 * threshold and not a proof — a fast enough pass still fetches the link it ends
 * on — but the cascade is what it was written against, and what it costs the
 * reader who did stop is 150 ms out of a head start measured in hundreds.
 */
const FOCUS_DWELL_MS = 150;

/** The anchor an event happened inside, if it happened inside one. */
function linkFrom(event: Event): HTMLAnchorElement | null {
  const target = event.target;
  return target instanceof Element
    ? target.closest<HTMLAnchorElement>("a[href]")
    : null;
}

/**
 * The record every address nothing else owns resolves to.
 *
 * router.ts ends with a catch-all, so `resolve` never comes back empty: an
 * internal link that is not a route of ours — a file the server hands over, an
 * address that used to be a page — resolves to the 404 screen, and prefetching
 * that is fetching an error page for a reader who has not asked for one.
 *
 * Found by asking the router for an address no route can plausibly own, rather
 * than by naming the route: the record is then compared by identity, so
 * renaming "not-found" or moving it cannot quietly turn this check off. When
 * there is no catch-all at all this reads `undefined`, which is then exactly
 * what an unmatched address produces — the same refusal, for the same reason.
 */
function catchAllRecord(): unknown {
  return leafOf(router.resolve("/__no-route-owns-this__").matched);
}

/** The last of the matched records: the page itself, under its shells. */
function leafOf(matched: readonly unknown[]): unknown {
  return matched[matched.length - 1];
}

/**
 * The route a click on this link would open, or null when the link names no
 * chunk worth fetching.
 *
 * The refusals, in order: a link that opens somewhere else (a new tab has its
 * own module registry, so the fetch would be thrown away) or downloads instead
 * of navigating; an in-page anchor, which moves the scroll and loads nothing;
 * anything not on this origin, which `toInternalPath` decides — including
 * `mailto:` and the disguised-internal forms it was written for; an internal
 * address that is not one of our routes; and a link to the page already open,
 * whose chunk is by definition already here.
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

/**
 * What the chunks behind a link are remembered by.
 *
 * The matched RECORDS, not the address: `/users/anna` and `/users/boris` are
 * two addresses drawn by one component, and every profile link on a page would
 * otherwise count as a separate thing to fetch. Records are also what a nested
 * route is — a game room is its zone shell plus the room — and both halves are
 * needed before that page can render.
 */
function chunkKey(route: RouteLocationResolved): string {
  return route.matched.map((record) => record.path).join(">");
}

/**
 * Starts the listening. Returns the teardown, which nothing in the running app
 * calls — the app outlives it — and which the test uses so one case's handlers
 * do not answer the next one's events.
 */
export function installRoutePrefetch(): () => void {
  /** Chunk keys already asked for. Never fetched twice on one page load. */
  const requested = new Set<string>();
  const catchAll = catchAllRecord();

  let dwellTimer: ReturnType<typeof setTimeout> | null = null;
  /** The link currently being aimed at, by pointer or by focus. */
  let aimedAt: HTMLAnchorElement | null = null;

  /**
   * A navigation the reader started is in flight.
   *
   * The one case where this feature can make the site slower rather than
   * faster: a reader clicks A, its chunk starts downloading, and the pointer
   * comes to rest on B on the way to the mouse being let go. Without this, B's
   * whole graph joins the queue and shares the channel with the page that is
   * actually opening. `router.currentRoute` is no help — it is updated when
   * the navigation ENDS, which is the moment the danger passes — so the router
   * is asked to say when instead.
   */
  let navigating = false;

  function clearDwellTimer(): void {
    if (dwellTimer !== null) clearTimeout(dwellTimer);
    dwellTimer = null;
  }

  function prefetch(link: HTMLAnchorElement): void {
    // Asked per prefetch rather than once at install: a phone leaving wifi
    // turns both of these on mid-session, and the reader who set Save-Data did
    // so to stop exactly this kind of request.
    if (savingData() || onSlowConnection()) return;
    // Dropped rather than deferred. A guess made while the reader was opening
    // something else has already been overtaken by what they did.
    if (navigating) return;

    const route = routeBehind(link, catchAll);
    if (route === null) return;

    const key = chunkKey(route);
    if (requested.has(key)) return;
    // Remembered BEFORE the request goes out, not after it lands: a pointer
    // resting on a link raises more than one event, and the second must find
    // the first already counted rather than a fetch still in flight.
    requested.add(key);

    void loadRouteLocation(route).catch(() => {
      // A prefetch that fails has to cost nothing. The router's own navigation
      // asks for the same module again and reports its failure the way it
      // always has (the stale-chunk reload in router.ts); forgetting the key
      // here only means a later hover is allowed to try again.
      requested.delete(key);
    });
  }

  /**
   * Takes a link as the current aim and starts its wait. One timer for the
   * whole site: a reader aims at one thing at a time, so a new aim replaces
   * the pending one whichever way it arrived — a focus cancels a pointer's
   * wait and a pointer cancels a focus's.
   */
  function aimAt(link: HTMLAnchorElement, dwellMs: number): void {
    // Already the aim: the timer for it is either still running or has already
    // fired, and re-arming would restart the wait on every repeated event.
    if (link === aimedAt) return;

    clearDwellTimer();
    aimedAt = link;
    dwellTimer = setTimeout(() => {
      dwellTimer = null;
      prefetch(link);
    }, dwellMs);
  }

  /** Gives up the current aim, if it is this link. */
  function stopAiming(link: HTMLAnchorElement): void {
    if (link !== aimedAt) return;
    clearDwellTimer();
    aimedAt = null;
  }

  function onPointerOver(event: Event): void {
    const link = linkFrom(event);
    if (link !== null) aimAt(link, HOVER_DWELL_MS);
  }

  function onPointerOut(event: Event): void {
    const link = linkFrom(event);
    if (link === null) return;
    // `mouseout` also fires when the pointer crosses from the link onto an
    // icon INSIDE it. Cancelling there would restart the wait on every inner
    // element a link is made of, and a link made of enough of them would never
    // reach the end of it.
    const movedTo = (event as MouseEvent).relatedTarget;
    if (movedTo instanceof Node && link.contains(movedTo)) return;

    stopAiming(link);
  }

  function onFocusIn(event: Event): void {
    const link = linkFrom(event);
    if (link !== null) aimAt(link, FOCUS_DWELL_MS);
  }

  function onFocusOut(event: Event): void {
    // Focus leaving for something that is not a link: the next focusin would
    // otherwise never arrive to replace this aim, and the wait would end by
    // fetching a link the reader has already left.
    const link = linkFrom(event);
    if (link !== null) stopAiming(link);
  }

  function onTouchStart(event: Event): void {
    const link = linkFrom(event);
    // A touch screen has no hover at all, so without this the whole feature
    // would be for readers holding a mouse. No wait here, unlike the two
    // above: a finger already down on a link is a click in progress, not an
    // aim that might be abandoned, and the couple of hundred milliseconds
    // before it lifts is precisely the head start being bought.
    if (link !== null) prefetch(link);
  }

  // Passive: not one of these handlers calls preventDefault, and saying so
  // keeps the touch one off the critical path of scrolling.
  const options: AddEventListenerOptions = { passive: true };
  document.addEventListener("mouseover", onPointerOver, options);
  document.addEventListener("mouseout", onPointerOut, options);
  document.addEventListener("focusin", onFocusIn, options);
  document.addEventListener("focusout", onFocusOut, options);
  document.addEventListener("touchstart", onTouchStart, options);

  const stopWatchingStart = router.beforeEach(() => {
    navigating = true;
  });
  // Both endings. `afterEach` runs for a navigation that was aborted or
  // redirected as well as one that arrived, but a guard that THROWS reaches
  // neither it nor the reader's page — and a flag left standing after one of
  // those would switch prefetching off for the rest of the session.
  const stopWatchingEnd = router.afterEach(() => {
    navigating = false;
  });
  const stopWatchingError = router.onError(() => {
    navigating = false;
  });

  return () => {
    clearDwellTimer();
    document.removeEventListener("mouseover", onPointerOver, options);
    document.removeEventListener("mouseout", onPointerOut, options);
    document.removeEventListener("focusin", onFocusIn, options);
    document.removeEventListener("focusout", onFocusOut, options);
    document.removeEventListener("touchstart", onTouchStart, options);
    stopWatchingStart();
    stopWatchingEnd();
    stopWatchingError();
  };
}

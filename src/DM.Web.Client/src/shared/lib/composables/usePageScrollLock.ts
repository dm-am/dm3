import { onBeforeUnmount } from "vue";
import { getScrollContainer } from "@/shared/lib/scroll";

/** Windows open right now, of every kind: the lock is one for all of them. */
let holders = 0;

/** The container locked by the first window and what it carried before. */
let locked: { el: HTMLElement; overflow: string; gutter: string } | null = null;

const lock = () => {
  holders += 1;
  if (holders > 1) return;
  const el = getScrollContainer();
  if (!el) return;
  locked = {
    el,
    overflow: el.style.overflow,
    gutter: el.style.getPropertyValue("scrollbar-gutter"),
  };
  el.style.setProperty("scrollbar-gutter", "stable");
  el.style.overflow = "hidden";
};

const unlock = () => {
  if (holders === 0) return;
  holders -= 1;
  if (holders > 0 || !locked) return;
  const { el, overflow, gutter } = locked;
  locked = null;
  // Given back only if it is still what this lock put there: a value written
  // over it while the page was held is somebody else's and stays.
  if (el.style.overflow === "hidden") el.style.overflow = overflow;
  if (el.style.getPropertyValue("scrollbar-gutter") === "stable")
    el.style.setProperty("scrollbar-gutter", gutter);
};

/**
 * Holds the page still under a window.
 *
 * The page does not scroll in the window of the browser: it scrolls inside
 * App.vue's `.main`, and `body` stands at overflow: hidden for good
 * (Reset.sass). So the scroll lock of vue-final-modal, which targets `body`,
 * never locked anything, and this locks the container from the scroll registry
 * instead. The side column of a narrow screen (MobileDrawer) holds the page
 * with this same lock.
 *
 * One more thing this lock must do: keep the place of the scrollbar. `.main`
 * scrolls with `overflow-y: scroll`, and on a desktop with a classic scrollbar
 * the bar takes real width; `overflow: hidden` alone takes the bar away and
 * the whole page under the dimming jumps sideways by its width, and back again
 * when the window closes. `scrollbar-gutter: stable` keeps that width reserved
 * while the bar is gone.
 *
 * Counted, because a window may stand over a window (the question "Закрыть без
 * сохранения?" over the one it guards): the page is let go when the last of
 * them closes. A holder that unmounts while holding lets go on the way out, so
 * a route change under an open window cannot leave the page frozen.
 */
export function usePageScrollLock() {
  let held = false;

  const hold = () => {
    if (held) return;
    held = true;
    lock();
  };

  const release = () => {
    if (!held) return;
    held = false;
    unlock();
  };

  onBeforeUnmount(release);

  return { hold, release };
}

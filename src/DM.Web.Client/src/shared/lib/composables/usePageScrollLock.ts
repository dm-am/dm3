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
 * Holds the page still under a window. The page scrolls in App.vue's `.main`,
 * not body, so the lock of vue-final-modal locks nothing; scrollbar-gutter keeps
 * the width of a classic scrollbar. Counted for a window over a window, and let
 * go by a holder that unmounts.
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

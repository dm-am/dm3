import { onScopeDispose, watch, type Ref } from "vue";

/**
 * The windows of the site that are open right now, and the one thing done to
 * all of them at once: closing them when the session ends.
 *
 * A window stands over a page that was drawn for a signed-in reader. Once the
 * site learns the session is gone, the header and the page turn into a
 * guest's, and a window left standing over them offers a press the server will
 * refuse. The owner's word is that they close together with the session.
 *
 * Each window registers its own way out rather than being found in the DOM:
 * only the window knows what closing it means (a request in flight, a question
 * over typed text), and the two tiers of windows the site has, the library's
 * and the self-rolled one, look nothing alike from the outside.
 *
 * The guest windows do not register. The login window is the one a signed-out
 * reader uses to come back, and the end of a session is exactly what opens it:
 * a second refusal arriving a moment after the first must not take it away.
 */
const open = new Set<() => void>();

/**
 * Keeps `close` among the open windows while `shown` is true. Registered on
 * the effect scope of the caller, so a window that leaves with its screen
 * leaves the list too.
 */
export function useOpenWindow(shown: Ref<boolean>, close: () => void): void {
  let registered = false;
  const release = () => {
    open.delete(close);
    registered = false;
  };

  watch(
    shown,
    (on) => {
      if (on && !registered) {
        open.add(close);
        registered = true;
      } else if (!on && registered) {
        release();
      }
    },
    { immediate: true, flush: "sync" },
  );

  onScopeDispose(release);
}

/**
 * Closes every open window, each its own way. A window is taken off the list
 * before it is asked to close, so one that closes through two paths at once
 * (its own request refused with the same 401) is asked only here.
 */
export function closeOpenWindows(): void {
  for (const close of [...open]) {
    open.delete(close);
    close();
  }
}

/** How many windows are open. For tests. */
export function openWindowCount(): number {
  return open.size;
}

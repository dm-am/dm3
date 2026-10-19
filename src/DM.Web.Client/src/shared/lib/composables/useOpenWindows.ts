import { onScopeDispose, watch, type Ref } from "vue";

/**
 * The open windows of the site, closed together when the session ends. Each
 * registers its own way out, since only it knows what closing means. The guest
 * windows do not: the login window is how a signed-out reader comes back.
 */
const open = new Set<() => void>();

/** Keeps `close` listed while `shown` is true, on the caller's effect scope. */
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
 * Closes every open window, each its own way, taken off the list first so a
 * window closing through two paths is asked once.
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

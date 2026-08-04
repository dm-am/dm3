import { watch } from "vue";
import { useAuthStore } from "@/shared/stores";

/**
 * Run something whenever the viewer changes.
 *
 * The six sidebar blocks that hold per-viewer data each asked their own
 * question, and all six asked the wrong one: whether a username had appeared
 * ("signed in") or disappeared ("signed out"). Signing a different account in
 * from a second tab is neither. The auth store relays that through the
 * `storage` event and writes the new user straight in, so the name goes from A
 * to B in one step and both halves of every condition were false: the header
 * showed B while "Мои игры" went on listing A's games, private ones included.
 *
 * The watcher already fires only when the name actually changes, so the
 * conditions excluded exactly the case that was broken and nothing else.
 *
 * Passing null through in between is not a fix, and was the first thing tried:
 * Vue batches watcher callbacks, so `user = null` followed by `user = incoming`
 * in one tick coalesces into a single A-to-B call and every block sees the same
 * step it already mishandled. The question belongs here instead, asked once.
 *
 * @param handler Receives the new viewer's username, or undefined for a guest.
 */
export function useViewerChange(
  handler: (username: string | undefined) => void,
): void {
  const auth = useAuthStore();
  watch(
    () => auth.user?.username,
    (username) => handler(username),
  );
}

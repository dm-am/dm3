import { watch } from "vue";
import { useAuthStore } from "@/shared/stores";

/**
 * Runs something whenever the viewer changes, A to B in another tab included,
 * which "signed in" and "signed out" both miss.
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

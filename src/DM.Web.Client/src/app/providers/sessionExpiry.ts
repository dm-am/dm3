/**
 * What the app does when the server says the session is gone.
 *
 * The HTTP client is the one that sees the 401, but it lives in `shared` and
 * may know neither the auth store's meaning nor the router. It reports the
 * event; this module decides what it costs.
 *
 * It is a module of its own rather than a closure in `main.ts` because main is
 * the entry point — it mounts the app, so nothing can call into it from a test.
 */
import { setSessionExpiredHandler } from "@/shared/api";
import { loginLocation } from "@/shared/lib/auth";
import { useAuthStore } from "@/shared/stores";
import router from "./router";

/**
 * Exported for the test. Clearing the store is the load-bearing half: the
 * header, the sidebar blocks, the comment forms and the route guard all read
 * it, so wiping only the persisted copy left a signed-out viewer with a
 * signed-in interface until the next full reload.
 */
export function endExpiredSession(): void {
  // updateUser is the only writer of the persisted copy, so this drops both at
  // once — and the resulting `storage` event still logs the other tabs out.
  useAuthStore().updateUser(null);

  // Only a page the viewer may no longer see is worth leaving, and this fires
  // on any 401 from any request. Navigating unconditionally tore a reader off
  // a public page mid-sentence — out of a game room, out of a topic — and took
  // whatever was typed in a field without a draft-key with it. A protected page
  // is left, and it is left the way the guard leaves it: replaced, carrying the
  // address, so signing in puts the viewer back where the session ran out.
  const current = router.currentRoute.value;
  if (current.meta.requiresAuth) {
    void router.replace(loginLocation(current.fullPath));
  }
}

/** Installed once at startup, before the app mounts. */
export function installSessionExpiredHandler(): void {
  setSessionExpiredHandler(endExpiredSession);
}

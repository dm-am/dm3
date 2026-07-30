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
  void router.push({ name: "home" });
}

/** Installed once at startup, before the app mounts. */
export function installSessionExpiredHandler(): void {
  setSessionExpiredHandler(endExpiredSession);
}

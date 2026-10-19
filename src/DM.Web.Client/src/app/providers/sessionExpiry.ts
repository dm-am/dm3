// What the app does when the HTTP client in shared reports an expired session;
// apart from main.ts so a test can call it.
import { setSessionExpiredHandler } from "@/shared/api";
import { loginLocation } from "@/shared/lib/auth";
import { closeOpenWindows } from "@/shared/lib/composables/useOpenWindows";
import { useAuthStore } from "@/shared/stores";
import router from "./router";

/** Clears the store, not only the persisted copy: the whole interface reads it. */
export function endExpiredSession(): void {
  // A press in a window opened for the signed-in reader would meet the same
  // refusal. The login window stays (useOpenWindows).
  closeOpenWindows();

  // Drops the persisted copy too; its storage event logs the other tabs out.
  useAuthStore().updateUser(null);

  // Fires on any 401, so only a protected page is left, the way the guard
  // leaves it.
  const current = router.currentRoute.value;
  if (current.meta.requiresAuth) {
    void router.replace(loginLocation(current.fullPath));
  }
}

/** Installed once at startup, before the app mounts. */
export function installSessionExpiredHandler(): void {
  setSessionExpiredHandler(endExpiredSession);
}

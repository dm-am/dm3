/**
 * Authentication store
 * @module shared/stores/auth
 *
 * Global authentication state. Used across all layers.
 * Holds who the viewer is, persists it and keeps browser tabs in step.
 *
 * State only, by design: signing in, signing out and refreshing the viewer are
 * account endpoints, and a store in `shared` calling those would put domain
 * knowledge in the layer every other layer may import. Those live in
 * entities/user (`session.ts`) and come back in through `updateUser`.
 */
import { defineStore } from "pinia";
import type { User } from "@/shared/api/models/common/user";
import { Theme } from "@/shared/api/models/personal";
import { ref, computed, onScopeDispose } from "vue";
import { useUiStore } from "./ui";

export const useAuthStore = defineStore("auth", () => {
  const userKey = "user";

  // Initialize user from localStorage immediately when creating the store
  // This allows components to react instantly without waiting for API.
  // A corrupted value (manual edit, partial write, schema drift) must not crash
  // app startup — clear the bad key and start as a guest.
  function readStoredUser(): User | null {
    const stored = localStorage.getItem(userKey);
    if (!stored) return null;
    try {
      return JSON.parse(stored) as User;
    } catch {
      localStorage.removeItem(userKey);
      return null;
    }
  }

  const user = ref<User | null>(readStoredUser());

  function updateUser(newUser: User | null) {
    const { updateTheme } = useUiStore();

    user.value = newUser;
    if (newUser === null) localStorage.removeItem(userKey);
    else localStorage.setItem(userKey, JSON.stringify(newUser));
    updateTheme(newUser?.settings?.theme ?? Theme.Light);
  }

  // Initialize theme immediately based on stored user
  if (user.value) {
    const { updateTheme } = useUiStore();
    updateTheme(user.value.settings?.theme ?? Theme.Light);
  }

  const isAuthenticated = computed(() => user.value !== null);

  // Sync logout across browser tabs via localStorage events
  if (typeof window !== "undefined") {
    const onStorage = (e: StorageEvent) => {
      if (e.key === userKey && !e.newValue) {
        user.value = null;
        const { updateTheme } = useUiStore();
        updateTheme(Theme.Light);
      } else if (e.key === userKey && e.newValue) {
        try {
          user.value = JSON.parse(e.newValue);
          const { updateTheme } = useUiStore();
          updateTheme(user.value?.settings?.theme ?? Theme.Light);
        } catch {
          // Ignore malformed JSON
        }
      }
    };

    window.addEventListener("storage", onStorage);

    // The subscription belongs to this store instance and not to the window: a
    // setup store runs inside an effect scope that `$dispose` and app teardown
    // stop, so the listener goes away with the instance that registered it
    // instead of piling up on a window every later instance shares.
    onScopeDispose(() => window.removeEventListener("storage", onStorage));
  }

  return {
    user,
    isAuthenticated,
    updateUser,
  };
});

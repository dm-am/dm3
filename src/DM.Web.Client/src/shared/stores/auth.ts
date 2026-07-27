/**
 * Authentication store
 * @module shared/stores/auth
 *
 * Global authentication state. Used across all layers.
 * Manages user session, login/logout, and current user info.
 */
import { defineStore } from "pinia";
import type { User } from "@/shared/api/models/common/user";
import { Theme } from "@/shared/api/models/personal";
import { ref, computed } from "vue";
import accountApi from "@/shared/api/accountApi";
import personalApi from "@/shared/api/personalApi";
import { useUiStore } from "./ui";
import type { BadRequestError } from "@/shared/api/models/common";
import type {
  LoginCredentials,
  RegisterCredentials,
} from "@/shared/api/models/account";

export const useAuthStore = defineStore("root", () => {
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

  async function register(credentials: RegisterCredentials) {
    const { error } = await accountApi.register(credentials);
    if (error && ("invalidProperties" in error || "errors" in error))
      return error as BadRequestError;
    return null;
  }

  async function signIn(credentials: LoginCredentials) {
    // Use cookie-based auth (signIn sets HttpOnly cookie)
    const { data, error } = await accountApi.signIn(credentials);

    if (data) {
      updateUser(data);
      return null;
    }

    if (error && ("invalidProperties" in error || "errors" in error)) {
      return error as BadRequestError;
    }

    return null;
  }

  async function signOut() {
    await accountApi.signOut();
    updateUser(null);
  }

  // "Выйти со всех устройств": terminate every other active session first,
  // then sign the current one out. The backend has no single "logout
  // everywhere" endpoint (DELETE account/sessions/others keeps the current
  // session), so combining the two calls logs the user out on all devices.
  async function signOutAll() {
    await accountApi.logoutAll();
    await accountApi.signOut();
    updateUser(null);
  }

  async function fetchUser() {
    if (!accountApi.isAuthenticated()) return;

    // User is already initialized from localStorage when store is created
    // Here we only refresh from server (background refresh)
    const { data } = await personalApi.getMyProfile();
    updateUser(data ?? null);
  }

  // Initialize theme immediately based on stored user
  if (user.value) {
    const { updateTheme } = useUiStore();
    updateTheme(user.value.settings?.theme ?? Theme.Light);
  }

  const isAuthenticated = computed(() => user.value !== null);

  // Sync logout across browser tabs via localStorage events
  if (typeof window !== "undefined") {
    window.addEventListener("storage", (e) => {
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
    });
  }

  return {
    user,
    isAuthenticated,
    register,
    signIn,
    signOut,
    signOutAll,
    fetchUser,
    updateUser,
  };
});

/** @deprecated Use useAuthStore instead */
export const useUserStore = useAuthStore;

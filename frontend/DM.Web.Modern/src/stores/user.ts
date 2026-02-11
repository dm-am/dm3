import { defineStore } from "pinia";
import type { User } from "@/api/models/community";
import { ColorSchema } from "@/api/models/community";
import { ref, computed } from "vue";
import accountApi from "@/api/requests/accountApi";
import { useUiStore } from "@/stores/ui";
import type { BadRequestError } from "@/api/models/common";
import type {
  LoginCredentials,
  RegisterCredentials,
} from "@/api/models/account";

export const useUserStore = defineStore("root", () => {
  const userKey = "user";

  // Инициализируем user из localStorage СРАЗУ при создании store
  // Это позволяет компонентам реагировать мгновенно, без ожидания API
  const storedUser = localStorage.getItem(userKey);
  const user = ref<User | null>(storedUser ? JSON.parse(storedUser) : null);

  function updateUser(newUser: User | null) {
    const { updateTheme } = useUiStore();

    user.value = newUser;
    if (newUser === null) localStorage.removeItem(userKey);
    else localStorage.setItem(userKey, JSON.stringify(newUser));
    updateTheme(newUser?.settings?.colorSchema ?? ColorSchema.Light);
  }

  async function register(credentials: RegisterCredentials) {
    const { error } = await accountApi.register(credentials);
    if (error && ("invalidProperties" in error || "errors" in error)) return error as BadRequestError;
    return null;
  }

  async function signIn(credentials: LoginCredentials) {
    // Use cookie-based auth (signIn sets HttpOnly cookie)
    const { data, error } = await accountApi.signIn(credentials);

    if (data?.resource) {
      updateUser(data.resource);
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

  async function fetchUser() {
    if (!accountApi.isAuthenticated()) return;

    // User уже инициализирован из localStorage при создании store
    // Здесь только обновляем с сервера (фоновый refresh)
    const { data } = await accountApi.fetchUser();
    updateUser(data?.resource ?? null);
  }

  // Инициализируем тему сразу на основе сохраненного пользователя
  if (user.value) {
    const { updateTheme } = useUiStore();
    updateTheme(user.value.settings?.colorSchema ?? ColorSchema.Light);
  }

  const isAuthenticated = computed(() => user.value !== null);

  // Sync logout across browser tabs via localStorage events
  if (typeof window !== "undefined") {
    window.addEventListener("storage", (e) => {
      if (e.key === userKey && !e.newValue) {
        user.value = null;
        const { updateTheme } = useUiStore();
        updateTheme(ColorSchema.Light);
      } else if (e.key === userKey && e.newValue) {
        try {
          user.value = JSON.parse(e.newValue);
          const { updateTheme } = useUiStore();
          updateTheme(user.value?.settings?.colorSchema ?? ColorSchema.Light);
        } catch {
          // Ignore malformed JSON
        }
      }
    });
  }

  return { user, isAuthenticated, register, signIn, signOut, fetchUser, updateUser };
});

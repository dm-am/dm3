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
    if (error && "errors" in error) return error as BadRequestError;
    return null;
  }

  async function signIn(credentials: LoginCredentials) {
    const result = await accountApi.signInOAuth(credentials);

    if (result.success && result.user) {
      updateUser(result.user);
      return null;
    }

    return result.error ?? null;
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

  /**
   * Set OAuth tokens from external auth callback (Discord, etc.)
   */
  function setOAuthTokens(accessToken: string, refreshToken?: string) {
    // Import Api dynamically to avoid circular deps
    import("@/api").then((module) => {
      module.default.updateTokens({
        access_token: accessToken,
        refresh_token: refreshToken || "",
        token_type: "Bearer",
        expires_in: 3600,
      });
    });
  }

  // Инициализируем тему сразу на основе сохранённого пользователя
  if (user.value) {
    const { updateTheme } = useUiStore();
    updateTheme(user.value.settings?.colorSchema ?? ColorSchema.Light);
  }

  const isAuthenticated = computed(() => user.value !== null);

  return { user, isAuthenticated, register, signIn, signOut, fetchUser, setOAuthTokens };
});

import { defineStore } from "pinia";
import { computed, ref, watch } from "vue";
import { Theme } from "@/shared/api/models/community";

/**
 * Верстка сообщений, постов, комментариев и топиков.
 * Future-proof union — новые значения (например, "cozy") добавляются без рефакторинга.
 *
 * - `compact` — без аватаров, плотная компоновка
 * - `full`    — с аватарами, развернутая компоновка
 */
export type MessageLayout = "compact" | "full";

export const MESSAGE_LAYOUTS = ["compact", "full"] as const;

const THEME_STORAGE_KEY = "dm_theme";
const MESSAGE_LAYOUT_KEY = "dm_message_layout";

function isMessageLayout(value: unknown): value is MessageLayout {
  return value === "compact" || value === "full";
}

/**
 * Определяет начальную тему:
 * 1. Из localStorage (если пользователь уже выбирал)
 * 2. Из prefers-color-scheme (системные настройки ОС)
 * 3. Fallback на Light
 */
function getInitialTheme(): Theme {
  const stored = localStorage.getItem(THEME_STORAGE_KEY);
  if (stored === Theme.Dark || stored === Theme.Light) {
    return stored;
  }

  if (typeof window !== "undefined" && window.matchMedia) {
    const prefersDark = window.matchMedia(
      "(prefers-color-scheme: dark)",
    ).matches;
    if (prefersDark) {
      return Theme.Dark;
    }
  }

  return Theme.Light;
}

/**
 * Определяет начальную верстку сообщений:
 * 1. Из localStorage (если пользователь уже выбирал)
 * 2. Fallback на `full` (с аватарами) — дефолт для новых пользователей
 */
function getInitialMessageLayout(): MessageLayout {
  const stored = localStorage.getItem(MESSAGE_LAYOUT_KEY);
  return isMessageLayout(stored) ? stored : "full";
}

export const useUiStore = defineStore("ui", () => {
  const theme = ref(getInitialTheme());
  const messageLayout = ref<MessageLayout>(getInitialMessageLayout());

  // Сохраняем в localStorage при изменении
  watch(theme, (newTheme) => {
    localStorage.setItem(THEME_STORAGE_KEY, newTheme);
  });

  watch(messageLayout, (newValue) => {
    localStorage.setItem(MESSAGE_LAYOUT_KEY, newValue);
  });

  const updateTheme = (newTheme: Theme) => (theme.value = newTheme);
  const toggleTheme = () => {
    updateTheme(theme.value === Theme.Dark ? Theme.Light : Theme.Dark);
  };

  const isCompactLayout = computed(() => messageLayout.value === "compact");

  const setMessageLayout = (layout: MessageLayout) => {
    messageLayout.value = layout;
  };

  return {
    theme,
    updateTheme,
    toggleTheme,
    messageLayout,
    isCompactLayout,
    setMessageLayout,
  };
});

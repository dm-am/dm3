import { defineStore } from "pinia";
import { ref, watch } from "vue";
import { Theme } from "@/shared/api/models/community";

const THEME_STORAGE_KEY = "dm_theme";

/**
 * Определяет начальную тему:
 * 1. Из localStorage (если пользователь уже выбирал)
 * 2. Из prefers-color-scheme (системные настройки ОС)
 * 3. Fallback на Light
 */
function getInitialTheme(): Theme {
  // 1. Проверяем localStorage
  const stored = localStorage.getItem(THEME_STORAGE_KEY);
  if (stored === Theme.Dark || stored === Theme.Light) {
    return stored;
  }

  // 2. Проверяем системные настройки
  if (typeof window !== "undefined" && window.matchMedia) {
    const prefersDark = window.matchMedia(
      "(prefers-color-scheme: dark)",
    ).matches;
    if (prefersDark) {
      return Theme.Dark;
    }
  }

  // 3. Fallback
  return Theme.Light;
}

export const useUiStore = defineStore("ui", () => {
  const theme = ref(getInitialTheme());

  // Сохраняем в localStorage при изменении
  watch(theme, (newTheme) => {
    localStorage.setItem(THEME_STORAGE_KEY, newTheme);
  });

  const updateTheme = (newTheme: Theme) => (theme.value = newTheme);
  const toggleTheme = () => {
    updateTheme(
      theme.value === Theme.Dark ? Theme.Light : Theme.Dark,
    );
  };

  return { theme, updateTheme, toggleTheme };
});

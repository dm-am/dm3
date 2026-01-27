import { defineStore } from "pinia";
import { ref, watch } from "vue";
import { ColorSchema } from "@/api/models/community";

const THEME_STORAGE_KEY = "dm_theme";

/**
 * Определяет начальную тему:
 * 1. Из localStorage (если пользователь уже выбирал)
 * 2. Из prefers-color-scheme (системные настройки ОС)
 * 3. Fallback на Light
 */
function getInitialTheme(): ColorSchema {
  // 1. Проверяем localStorage
  const stored = localStorage.getItem(THEME_STORAGE_KEY);
  if (stored === ColorSchema.Dark || stored === ColorSchema.Light) {
    return stored;
  }

  // 2. Проверяем системные настройки
  if (typeof window !== "undefined" && window.matchMedia) {
    const prefersDark = window.matchMedia(
      "(prefers-color-scheme: dark)",
    ).matches;
    if (prefersDark) {
      return ColorSchema.Dark;
    }
  }

  // 3. Fallback
  return ColorSchema.Light;
}

export const useUiStore = defineStore("ui", () => {
  const theme = ref(getInitialTheme());

  // Сохраняем в localStorage при изменении
  watch(theme, (newTheme) => {
    localStorage.setItem(THEME_STORAGE_KEY, newTheme);
  });

  const updateTheme = (newTheme: ColorSchema) => (theme.value = newTheme);
  const toggleTheme = () => {
    updateTheme(
      theme.value === ColorSchema.Dark ? ColorSchema.Light : ColorSchema.Dark,
    );
  };

  return { theme, updateTheme, toggleTheme };
});

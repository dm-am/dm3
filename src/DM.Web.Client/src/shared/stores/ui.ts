import { defineStore } from "pinia";
import { ref, watch } from "vue";
import { Theme } from "@/shared/api/models/community";

const THEME_STORAGE_KEY = "dm_theme";
const COMPACT_MODE_KEY = "dm_compact_mode";

// Legacy keys from component-level implementations
const LEGACY_COMMENTS_COMPACT_KEY = "comments-compact-mode";
const LEGACY_GLOBAL_CHAT_COMPACT_KEY = "globalChat-compact-mode";

/**
 * Мигрирует настройки compact mode из старых component-level ключей
 * в единый глобальный ключ. Выполняется один раз при инициализации.
 */
function migrateCompactModeSettings(): void {
  // Если глобальный ключ уже установлен — не мигрируем
  if (localStorage.getItem(COMPACT_MODE_KEY) !== null) {
    // Очистка старых ключей (если остались)
    localStorage.removeItem(LEGACY_COMMENTS_COMPACT_KEY);
    localStorage.removeItem(LEGACY_GLOBAL_CHAT_COMPACT_KEY);
    return;
  }

  // Приоритет: comments > globalChat (comments более часто используется)
  const commentsCompact = localStorage.getItem(LEGACY_COMMENTS_COMPACT_KEY);
  const globalChatCompact = localStorage.getItem(LEGACY_GLOBAL_CHAT_COMPACT_KEY);

  if (commentsCompact !== null) {
    localStorage.setItem(COMPACT_MODE_KEY, commentsCompact);
  } else if (globalChatCompact !== null) {
    localStorage.setItem(COMPACT_MODE_KEY, globalChatCompact);
  }

  // Очистка старых ключей
  localStorage.removeItem(LEGACY_COMMENTS_COMPACT_KEY);
  localStorage.removeItem(LEGACY_GLOBAL_CHAT_COMPACT_KEY);
}

// Run migration on module load
migrateCompactModeSettings();

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

/**
 * Определяет начальный режим отображения:
 * 1. Из localStorage (если пользователь уже выбирал)
 * 2. Fallback на true (компактный по умолчанию)
 */
function getInitialCompactMode(): boolean {
  const stored = localStorage.getItem(COMPACT_MODE_KEY);
  if (stored !== null) {
    return stored === "true";
  }
  return false; // Обычный вид с портретами по умолчанию
}

export const useUiStore = defineStore("ui", () => {
  const theme = ref(getInitialTheme());
  const isCompactMode = ref(getInitialCompactMode());

  // Сохраняем в localStorage при изменении
  watch(theme, (newTheme) => {
    localStorage.setItem(THEME_STORAGE_KEY, newTheme);
  });

  watch(isCompactMode, (newValue) => {
    localStorage.setItem(COMPACT_MODE_KEY, String(newValue));
  });

  const updateTheme = (newTheme: Theme) => (theme.value = newTheme);
  const toggleTheme = () => {
    updateTheme(theme.value === Theme.Dark ? Theme.Light : Theme.Dark);
  };

  const toggleCompactMode = () => {
    isCompactMode.value = !isCompactMode.value;
  };

  return { theme, updateTheme, toggleTheme, isCompactMode, toggleCompactMode };
});

import { defineStore } from "pinia";
import { computed, ref, watch } from "vue";
import { Theme } from "@/shared/api/models/community";

/**
 * Layout of messages, posts, comments and topics.
 * Future-proof union — new values (e.g. "cozy") can be added without refactoring.
 *
 * - `compact` — no avatars, dense layout
 * - `full`    — with avatars, expanded layout
 */
export type MessageLayout = "compact" | "full";

export const MESSAGE_LAYOUTS = ["compact", "full"] as const;

const THEME_STORAGE_KEY = "dm_theme";
const MESSAGE_LAYOUT_KEY = "dm_message_layout";

function isMessageLayout(value: unknown): value is MessageLayout {
  return value === "compact" || value === "full";
}

/**
 * Determines the initial theme:
 * 1. From localStorage (if the user has chosen before)
 * 2. From prefers-color-scheme (OS system settings)
 * 3. Fallback to Light
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
 * Determines the initial message layout:
 * 1. From localStorage (if the user has chosen before)
 * 2. Fallback to `full` (with avatars) — the default for new users
 */
function getInitialMessageLayout(): MessageLayout {
  const stored = localStorage.getItem(MESSAGE_LAYOUT_KEY);
  return isMessageLayout(stored) ? stored : "full";
}

export const useUiStore = defineStore("ui", () => {
  const theme = ref(getInitialTheme());
  const messageLayout = ref<MessageLayout>(getInitialMessageLayout());

  // Mobile off-canvas navigation drawer (burger menu). Ephemeral UI state —
  // not persisted, always starts closed on load/reload.
  const isMobileDrawerOpen = ref(false);
  const openDrawer = () => {
    isMobileDrawerOpen.value = true;
  };
  const closeDrawer = () => {
    isMobileDrawerOpen.value = false;
  };
  const toggleDrawer = () => {
    isMobileDrawerOpen.value = !isMobileDrawerOpen.value;
  };

  // Persist to localStorage on change
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
    isMobileDrawerOpen,
    openDrawer,
    closeDrawer,
    toggleDrawer,
  };
});

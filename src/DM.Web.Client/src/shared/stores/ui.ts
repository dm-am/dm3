import { defineStore } from "pinia";
import { computed, ref, watch } from "vue";
import { Theme } from "@/shared/api/models/personal";

/**
 * Layout of messages, posts, comments and topics.
 *
 * - `compact` — no avatars, dense layout
 * - `full`    — with avatars, expanded layout
 *
 * This array is the only declaration of the set: the union type and the guard
 * over the persisted value are derived from it, so a new layout is added here
 * and nowhere else.
 */
export const MESSAGE_LAYOUTS = ["compact", "full"] as const;

export type MessageLayout = (typeof MESSAGE_LAYOUTS)[number];

const THEME_STORAGE_KEY = "dm_theme";
const MESSAGE_LAYOUT_KEY = "dm_message_layout";

function isMessageLayout(value: unknown): value is MessageLayout {
  return (MESSAGE_LAYOUTS as readonly unknown[]).includes(value);
}

/**
 * The theme this device has chosen, or null while it has none.
 *
 * The theme belongs to the device and not to the account, so this key holds the
 * whole record of the choice. The anti-FOUC script of index.html reads the same
 * key before the bundle loads; themeBoot.spec.ts is what holds the two in step.
 */
function readStoredTheme(): Theme | null {
  const stored = localStorage.getItem(THEME_STORAGE_KEY);
  return stored === Theme.Dark || stored === Theme.Light ? stored : null;
}

function storeTheme(theme: Theme): void {
  localStorage.setItem(THEME_STORAGE_KEY, theme);
}

/**
 * Determines the initial theme:
 * 1. From localStorage (if the user has chosen before)
 * 2. From prefers-color-scheme (OS system settings)
 * 3. Fallback to Light
 */
function getInitialTheme(): Theme {
  const stored = readStoredTheme();
  if (stored) {
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
    storeTheme(newTheme);
  });

  watch(messageLayout, (newValue) => {
    localStorage.setItem(MESSAGE_LAYOUT_KEY, newValue);
  });

  const updateTheme = (newTheme: Theme) => (theme.value = newTheme);
  const toggleTheme = () => {
    updateTheme(theme.value === Theme.Dark ? Theme.Light : Theme.Dark);
  };

  /**
   * The theme saved on the account, offered as a starting value and nothing
   * more.
   *
   * The theme belongs to the device: one that has already chosen keeps its
   * choice, so the account fills in only a device that has none — the first
   * sign-in here and never again. The value is recorded on the spot instead of
   * being left to the watcher above, which fires on a change and would write
   * nothing when the account offers the theme already on screen: unrecorded,
   * the device stays unchosen and takes the account theme again at every later
   * sign-in.
   */
  const adoptAccountTheme = (accountTheme: Theme | undefined) => {
    if (!accountTheme || readStoredTheme()) return;
    updateTheme(accountTheme);
    storeTheme(accountTheme);
  };

  const isCompactLayout = computed(() => messageLayout.value === "compact");

  const setMessageLayout = (layout: MessageLayout) => {
    messageLayout.value = layout;
  };

  return {
    theme,
    updateTheme,
    toggleTheme,
    adoptAccountTheme,
    messageLayout,
    isCompactLayout,
    setMessageLayout,
    isMobileDrawerOpen,
    openDrawer,
    closeDrawer,
    toggleDrawer,
  };
});

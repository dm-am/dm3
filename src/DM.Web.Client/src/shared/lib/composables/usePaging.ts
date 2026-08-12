/**
 * Composable for accessing user paging preferences
 * @module shared/lib/composables/usePaging
 *
 * Provides reactive access to user's preferred page sizes.
 * Falls back to defaults for unauthenticated users.
 */
import { computed } from "vue";
import { useAuthStore } from "@/shared/stores/auth";

/**
 * Default page sizes when user is not authenticated or has no preferences.
 *
 * Exported because the account settings form has to draw the same numbers it
 * serves: a second set of literals there shows the reader a page size no list
 * on the site uses.
 */
export const DEFAULT_PAGE_SIZES = {
  postsPerPage: 20,
  commentsPerPage: 20,
  topicsPerPage: 20,
  messagesPerPage: 20,
  entitiesPerPage: 20,
  pollsPerPage: 15, // Divisible by 3 for 3-column grid layout
} as const;

/**
 * Returns reactive paging preferences from current user or defaults
 */
export function usePaging() {
  const authStore = useAuthStore();

  /** Posts per page (for game rooms) */
  const postsPerPage = computed(() => {
    return (
      authStore.user?.settings?.paging?.postsPerPage ??
      DEFAULT_PAGE_SIZES.postsPerPage
    );
  });

  /** Comments per page (for game/topic comments) */
  const commentsPerPage = computed(() => {
    return (
      authStore.user?.settings?.paging?.commentsPerPage ??
      DEFAULT_PAGE_SIZES.commentsPerPage
    );
  });

  /** Topics per page (for forum boards) */
  const topicsPerPage = computed(() => {
    return (
      authStore.user?.settings?.paging?.topicsPerPage ??
      DEFAULT_PAGE_SIZES.topicsPerPage
    );
  });

  /** Messages per page (for chats) */
  const messagesPerPage = computed(() => {
    return (
      authStore.user?.settings?.paging?.messagesPerPage ??
      DEFAULT_PAGE_SIZES.messagesPerPage
    );
  });

  /** Entities per page (for lists: games, users, blogs, etc.) */
  const entitiesPerPage = computed(() => {
    return (
      authStore.user?.settings?.paging?.entitiesPerPage ??
      DEFAULT_PAGE_SIZES.entitiesPerPage
    );
  });

  /** Polls per page (divisible by 3 for grid layout) */
  const pollsPerPage = computed(() => {
    // Not customizable in user settings - use default
    return DEFAULT_PAGE_SIZES.pollsPerPage;
  });

  return {
    postsPerPage,
    commentsPerPage,
    topicsPerPage,
    messagesPerPage,
    entitiesPerPage,
    pollsPerPage,
  };
}

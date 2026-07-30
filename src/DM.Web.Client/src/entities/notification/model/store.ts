import { defineStore, storeToRefs } from "pinia";
import { ref } from "vue";
import { notificationApi } from "../api";
import { useAuthStore } from "@/shared/stores";

export const useNotificationStore = defineStore("notification", () => {
  const { user: currentUser } = storeToRefs(useAuthStore());

  // Unread notifications count (header badge)
  const unreadCount = ref(0);

  // Debounce state for fetchUnreadCount (prevents request flood from SignalR)
  let fetchUnreadDebounceTimer: ReturnType<typeof setTimeout> | null = null;
  const FETCH_UNREAD_DEBOUNCE_MS = 2000; // 2 seconds

  /**
   * Fetch the unread notifications count.
   * @param immediate - If true, fetches immediately (for app start). If false, debounces (for SignalR).
   */
  async function fetchUnreadCount(immediate = false) {
    if (!currentUser.value) return;

    // Clear any pending debounced fetch
    if (fetchUnreadDebounceTimer) {
      clearTimeout(fetchUnreadDebounceTimer);
      fetchUnreadDebounceTimer = null;
    }

    const doFetch = async () => {
      const { data } = await notificationApi.getUnreadCount();
      if (data) {
        unreadCount.value = data.count;
      }
    };

    if (immediate) {
      await doFetch();
    } else {
      // Debounce: wait before fetching (batches rapid SignalR notifications)
      fetchUnreadDebounceTimer = setTimeout(doFetch, FETCH_UNREAD_DEBOUNCE_MS);
    }
  }

  return {
    unreadCount,
    fetchUnreadCount,
  };
});

import { defineStore, storeToRefs } from "pinia";
import { ref } from "vue";
import { notificationApi } from "../api";
import { useAuthStore } from "@/shared/stores";
import { startStopwatch, type Stopwatch } from "@/shared/lib/utils/keyedCache";

export const useNotificationStore = defineStore("notification", () => {
  const { user: currentUser } = storeToRefs(useAuthStore());

  // Unread notifications count (header badge)
  const unreadCount = ref(0);

  // Debounce state for fetchUnreadCount (prevents request flood from SignalR)
  let fetchUnreadDebounceTimer: ReturnType<typeof setTimeout> | null = null;
  const FETCH_UNREAD_DEBOUNCE_MS = 2000; // 2 seconds

  // Ceiling on the debounce. Every deferred call used to restart the wait, so a
  // stream of events spaced closer than the debounce postponed the request for
  // as long as it lasted, and the badge stood still exactly while there was
  // most to report. The wait is now bounded: whatever the traffic, the count is
  // re-read at least this often. What is left of the ceiling is asked of the
  // shared stopwatch rather than measured here, so elapsed time is decided in
  // the one place the client keeps that arithmetic.
  const FETCH_UNREAD_MAX_WAIT_MS = 10000; // 10 seconds
  let deferredSince: Stopwatch | null = null;

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
      deferredSince = null;
      const { data } = await notificationApi.getUnreadCount();
      if (data) {
        unreadCount.value = data.count;
      }
    };

    if (immediate) {
      await doFetch();
    } else {
      // Debounce: wait before fetching (batches rapid SignalR notifications),
      // but never past the ceiling counted from the first deferred call.
      if (deferredSince === null) deferredSince = startStopwatch();
      const delay = Math.min(
        FETCH_UNREAD_DEBOUNCE_MS,
        deferredSince.remainingMs(FETCH_UNREAD_MAX_WAIT_MS),
      );
      fetchUnreadDebounceTimer = setTimeout(doFetch, delay);
    }
  }

  return {
    unreadCount,
    fetchUnreadCount,
  };
});

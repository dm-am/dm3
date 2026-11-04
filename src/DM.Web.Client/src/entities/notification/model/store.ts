import { defineStore, storeToRefs } from "pinia";
import { ref } from "vue";
import { notificationApi } from "../api";
import { useAuthStore } from "@/shared/stores";
import { deferredRead } from "@/shared/lib/utils/deferredRead";

export const useNotificationStore = defineStore("notification", () => {
  const { user: currentUser } = storeToRefs(useAuthStore());

  const unreadCount = ref(0);

  // Batches the pushes, so they do not flood the server with reads; a stream of
  // pushes must not hold the badge still, so the count is reread at least every ten seconds.
  const countRead = deferredRead(
    async () => {
      const { data } = await notificationApi.getUnreadCount();
      if (data) {
        unreadCount.value = data.count;
      }
    },
    2000,
    10000,
  );

  /** @param immediate At start; the pushes are debounced. */
  async function fetchUnreadCount(immediate = false) {
    if (!currentUser.value) return;
    if (immediate) await countRead.now();
    else countRead.later();
  }

  return {
    unreadCount,
    fetchUnreadCount,
  };
});

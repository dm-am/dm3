import { ref, computed, watch, type Ref } from "vue";
import { storeToRefs } from "pinia";
import { useAuthStore } from "@/shared/stores";
import type { Username } from "@/shared/api/models/community";
import type { ModeratedProfile } from "@/shared/api/models/moderation";
import { userIsModerator } from "@/entities/user/@x/moderation";
import { moderationApi } from "../api";

/**
 * Composable that fetches the moderated profile for a user
 * if the current user has Moderator+ role.
 * Returns null if the current user is not a moderator.
 */
export function useModeratedProfile(username: Ref<string> | (() => string)) {
  const { user: currentUser } = storeToRefs(useAuthStore());
  const moderatedProfile = ref<ModeratedProfile | null>(null);
  const loading = ref(false);

  const isModerator = computed(() => userIsModerator(currentUser.value));

  const usernameValue = computed(() =>
    typeof username === "function" ? username() : username.value,
  );

  async function refresh() {
    if (!isModerator.value || !usernameValue.value) {
      moderatedProfile.value = null;
      return;
    }

    loading.value = true;
    try {
      const { data } = await moderationApi.getModeratedProfile(
        usernameValue.value as Username,
      );
      moderatedProfile.value = data ?? null;
    } catch {
      moderatedProfile.value = null;
    } finally {
      loading.value = false;
    }
  }

  watch(usernameValue, refresh, { immediate: true });
  watch(isModerator, (newVal) => {
    if (newVal) refresh();
    else moderatedProfile.value = null;
  });

  return { moderatedProfile, loading, refresh };
}

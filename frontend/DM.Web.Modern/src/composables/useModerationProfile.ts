import { ref, computed, watch, type Ref } from "vue";
import { storeToRefs } from "pinia";
import { useUserStore } from "@/stores";
import { type UserLogin, UserRole } from "@/api/models/community";
import type { ModerationProfile } from "@/api/models/moderation";
import moderationApi from "@/api/requests/moderationApi";

const MODERATOR_ROLES: string[] = [
  UserRole.Moderator,
  UserRole.SeniorModerator,
  UserRole.Admin,
];

/**
 * Composable that fetches the moderation profile for a user
 * if the current user has Moderator+ role.
 * Returns null if the current user is not a moderator.
 */
export function useModerationProfile(login: Ref<string> | (() => string)) {
  const { user: currentUser } = storeToRefs(useUserStore());
  const moderationProfile = ref<ModerationProfile | null>(null);
  const loading = ref(false);

  const isModerator = computed(() => {
    const roles = currentUser.value?.roles;
    if (!roles) return false;
    return roles.some((r) => MODERATOR_ROLES.includes(r));
  });

  const loginValue = computed(() =>
    typeof login === "function" ? login() : login.value,
  );

  async function refresh() {
    if (!isModerator.value || !loginValue.value) {
      moderationProfile.value = null;
      return;
    }

    loading.value = true;
    try {
      const { data } = await moderationApi.getModerationProfile(
        loginValue.value as UserLogin,
      );
      moderationProfile.value = data?.resource ?? null;
    } catch {
      moderationProfile.value = null;
    } finally {
      loading.value = false;
    }
  }

  watch(loginValue, refresh, { immediate: true });
  watch(isModerator, (newVal) => {
    if (newVal) refresh();
    else moderationProfile.value = null;
  });

  return { moderationProfile, loading, refresh };
}

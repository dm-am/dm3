import { ref, watch, type Ref } from "vue";
import { blacklistApi } from "@/entities/user";
import type { User } from "@/shared/api/models/community";
import { useActionResult } from "@/shared/lib/composables/useActionResult";

/**
 * Whether the viewer's own blacklist holds the owner of a profile, and the
 * press that puts him on it or takes him off.
 */
export function useProfileBlacklist(user: Ref<User | null>) {
  // The profile answers it (blockedByViewer), a press on the spot until the next
  // read; `user` is replaced on every load, so following it follows the page.
  const isBlocked = ref(false);
  const isBlockLoading = ref(false);
  watch(
    user,
    (loaded) => {
      isBlocked.value = loaded?.blockedByViewer === true;
    },
    { immediate: true },
  );

  /**
   * Puts this user on the viewer's blacklist or takes him off, at once: the same
   * button takes either step back, so nothing asks first and nothing is said but
   * a refusal, under the row of buttons.
   */
  const blockResult = useActionResult();

  async function toggleBlock() {
    if (!user.value || isBlockLoading.value) return;
    const name = user.value.username;
    const blocking = !isBlocked.value;
    isBlockLoading.value = true;
    blockResult.clear();
    const { error } = blocking
      ? await blacklistApi.blockUser({ username: name })
      : await blacklistApi.unblockUser(name);
    isBlockLoading.value = false;
    if (error) {
      blockResult.refuse(
        error,
        blocking
          ? "Пользователь не добавлен в черный список"
          : "Пользователь не убран из черного списка",
      );
      return;
    }
    // The answer is about the profile it was asked on, which the reader may have left.
    if (user.value?.username === name) isBlocked.value = blocking;
  }

  return { isBlocked, isBlockLoading, blockResult, toggleBlock };
}

export type ProfileBlacklist = ReturnType<typeof useProfileBlacklist>;

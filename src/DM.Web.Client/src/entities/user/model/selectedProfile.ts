import { ref } from "vue";
import type { GeneralError } from "@/shared/api/models/common";
import type { User, Username, UserPicture } from "./types";
import { unwrapResource } from "@/shared/api";
import { userApi } from "../api";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";

export function useSelectedProfile() {
  const selectedUser = ref<User | null>(null);
  const loadingProfile = ref(false);

  // The answer for the name the reader already left must not land on the newer one.
  const profileGuard = createRequestGuard();

  /** The error, so the caller tells "no such user" from "server is down". */
  async function trySelectProfile(
    username: Username,
  ): Promise<GeneralError | null> {
    const requestId = profileGuard.next();
    loadingProfile.value = true;
    selectedUser.value = null;

    // The profile, not the short user of the lists.
    const { data, error } = await userApi.getUserProfile(username);

    if (!profileGuard.isCurrent(requestId)) return null;

    loadingProfile.value = false;

    if (error) return error;

    selectedUser.value = unwrapResource<User>(data);
    return null;
  }

  // By id: an upload can outlive the profile it started on.
  function applySelectedUserPicture(
    userId: string,
    picture: UserPicture,
  ): void {
    const current = selectedUser.value;
    if (!current || current.id !== userId) return;
    selectedUser.value = { ...current, picture };
  }

  return {
    selectedUser,
    loadingProfile,
    trySelectProfile,
    applySelectedUserPicture,
  };
}

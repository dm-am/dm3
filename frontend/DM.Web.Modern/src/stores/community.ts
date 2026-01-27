import { defineStore, storeToRefs } from "pinia";
import { ref } from "vue";
import type { AxiosProgressEvent } from "axios";
import type { ListEnvelope } from "@/api/models/common";
import type { User, UserLogin } from "@/api/models/community";
import type { UserSettings } from "@/api/models/community/user-settings";
import communityApi from "@/api/requests/communityApi";
import { useUserStore } from "@/stores/user";
import { useUiStore } from "@/stores/ui";

export type UpdateUserPayload = {
  status?: string;
  name?: string;
  location?: string;
  skype?: string;
  icq?: string;
  info?: string;
  ratingDisabled?: boolean;
  settings?: Partial<UserSettings>;
};

export const useCommunityStore = defineStore("community", () => {
  const { user: currentUser } = storeToRefs(useUserStore());
  const users = ref<ListEnvelope<User> | null>(null);

  async function fetchUsers(number: number) {
    const size = currentUser.value?.settings?.pagingLimits?.entitiesPerPage;
    const { data } = await communityApi.getUsers({ number, size });
    users.value = data;
  }

  const selectedUser = ref<User | null>(null);
  const loadingProfile = ref(false);

  async function trySelectProfile(login: UserLogin) {
    loadingProfile.value = true;
    selectedUser.value = null;

    const { data, error } = await communityApi.getUser(login);
    loadingProfile.value = false;

    if (error) return false;

    selectedUser.value = data!.resource;
    return true;
  }

  const editableUser = ref<User | null>(null);

  async function fetchEditableUser(login: UserLogin) {
    const { data } = await communityApi.getUserForUpdate(login);
    editableUser.value = data?.resource ?? null;
  }

  async function updateUser(login: UserLogin, updates: UpdateUserPayload) {
    const { data, error } = await communityApi.updateUser(
      login,
      updates as any,
    );
    if (!error && data) {
      selectedUser.value = data.resource;
      editableUser.value = null;

      // If current user updated their own settings, apply theme
      if (currentUser.value?.login === login && updates.settings?.colorSchema) {
        const { updateTheme } = useUiStore();
        updateTheme(updates.settings.colorSchema);
      }
    }
    return { data, error };
  }

  async function uploadPicture(
    login: UserLogin,
    files: FormData,
    onProgress?: (e: AxiosProgressEvent) => void,
  ) {
    const { data, error } = await communityApi.uploadUserPicture(
      login,
      files,
      onProgress ?? (() => {}),
    );
    if (!error && data) {
      selectedUser.value = data.resource;
    }
    return { data, error };
  }

  return {
    users,
    fetchUsers,
    selectedUser,
    loadingProfile,
    trySelectProfile,
    editableUser,
    fetchEditableUser,
    updateUser,
    uploadPicture,
  };
});

import { defineStore, storeToRefs } from "pinia";
import { ref } from "vue";
import type { ListEnvelope } from "@/api/models/common";
import type { User, UserLogin } from "@/api/models/community";
import { UserActivityFilter } from "@/api/models/community";
import type { UserSettings } from "@/api/models/community/user-settings";
import communityApi from "@/api/requests/communityApi";
import { useUserStore } from "@/stores/user";
import { useUiStore } from "@/stores/ui";

export type UpdateUserPayload = {
  status?: string;
  name?: string;
  location?: string;
  info?: string;
  ratingDisabled?: boolean;
  avatarUploadId?: string;
  settings?: Partial<UserSettings>;
  contacts?: Array<{ contactType: string; contactValue: string; sortOrder: number }>;
};

export const useCommunityStore = defineStore("community", () => {
  const { user: currentUser } = storeToRefs(useUserStore());
  const users = ref<ListEnvelope<User> | null>(null);
  const usersError = ref<number | null>(null);

  async function fetchUsers(number: number, filter: UserActivityFilter = UserActivityFilter.Active) {
    const size = currentUser.value?.settings?.pagingLimits?.entitiesPerPage;
    const { data, error } = await communityApi.getUsers({ number, size, filter });

    if (error?.status === 403) {
      users.value = null;
      usersError.value = 403;
      return;
    }

    usersError.value = null;
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

    selectedUser.value = data?.resource ?? null;
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

  return {
    users,
    usersError,
    fetchUsers,
    selectedUser,
    loadingProfile,
    trySelectProfile,
    editableUser,
    fetchEditableUser,
    updateUser,
  };
});

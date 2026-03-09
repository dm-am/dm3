// Community store
// Migrated from shared/stores/community.ts

import { defineStore, storeToRefs } from "pinia";
import { ref } from "vue";
import type { ListEnvelope } from "@/shared/api/models/common";
import type { User, Username } from "./types";
import { UserActivityFilter } from "./types";
import { CommunityApi } from "@/shared/api";
import { useUserStore } from "./store";

export const useCommunityStore = defineStore("community", () => {
  const { user: currentUser } = storeToRefs(useUserStore());
  const users = ref<ListEnvelope<User> | null>(null);
  const usersError = ref<number | null>(null);

  async function fetchUsers(number: number, filter: UserActivityFilter = UserActivityFilter.Active) {
    const size = currentUser.value?.settings?.paging?.entitiesPerPage;
    const { data, error } = await CommunityApi.getUsers({ number, size, filter });

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

  async function trySelectProfile(username: Username) {
    loadingProfile.value = true;
    selectedUser.value = null;

    const { data, error } = await CommunityApi.getUser(username);
    loadingProfile.value = false;

    if (error) return false;

    selectedUser.value = data ?? null;
    return true;
  }

  const editableUser = ref<User | null>(null);

  async function fetchEditableUser(username: Username) {
    const { data } = await CommunityApi.getUserForUpdate(username);
    editableUser.value = data ?? null;
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
  };
});

// Re-export filter enum for convenience
export { UserActivityFilter };

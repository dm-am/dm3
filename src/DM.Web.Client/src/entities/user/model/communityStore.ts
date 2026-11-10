import { defineStore } from "pinia";
import { useCommunitySearch } from "./communitySearch";
import { useSelectedProfile } from "./selectedProfile";

export type { UserActivityChoice, UsersSearchParams } from "./communitySearch";

export const useCommunityStore = defineStore("community", () => {
  const search = useCommunitySearch();
  const profile = useSelectedProfile();

  return {
    selectedUser: profile.selectedUser,
    loadingProfile: profile.loadingProfile,
    trySelectProfile: profile.trySelectProfile,
    applySelectedUserPicture: profile.applySelectedUserPicture,
    searchResult: search.searchResult,
    searchLoading: search.searchLoading,
    searchError: search.searchError,
    searchUsers: search.searchUsers,
    prefetchPage: search.prefetchPage,
    clearSearchCache: search.clearSearchCache,
  };
});

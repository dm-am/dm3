export * from "./types";

// Session store, re-exported from shared — see ./store for why it lives there
export { useAuthStore } from "./store";

// Community-specific store (stays in entities). The query shape is part of its
// contract: the users table and the user filter build params for it, and
// reaching into the module for them was a deep import past this barrel.
export {
  useCommunityStore,
  UserActivityFilter,
  type UsersSearchParams,
} from "./communityStore";

// Display composable for user formatting
export { useUserDisplay } from "./useUserDisplay";

// Debounced username lookup shared by the user pickers
export { useUserSearch, type UseUserSearchOptions } from "./useUserSearch";

// Avatar upload/reset composable — the single source of upload/reset logic
// (drag-drop, paste, compression, progress). Used by the ProfilePictureUpload
// overlay on the profile page.
export { useAvatarUpload, AVATAR_ACCEPT } from "./useAvatarUpload";

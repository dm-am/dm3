export * from "./types";

// Auth store re-exported from shared for backward compatibility
// Use @/shared/stores directly for new code
export { useAuthStore, useUserStore } from "./store";

// Community-specific store (stays in entities)
export { useCommunityStore, UserActivityFilter } from "./communityStore";

// Display composable for user formatting
export { useUserDisplay } from "./useUserDisplay";

// Debounced username lookup shared by the user pickers
export { useUserSearch, type UseUserSearchOptions } from "./useUserSearch";

// Avatar upload/reset composable — the single source of upload/reset logic
// (drag-drop, paste, compression, progress). Used by the ProfilePictureUpload
// overlay on the profile page.
export { useAvatarUpload, AVATAR_ACCEPT } from "./useAvatarUpload";

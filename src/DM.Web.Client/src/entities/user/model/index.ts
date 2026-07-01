export * from "./types";

// Auth store re-exported from shared for backward compatibility
// Use @/shared/stores directly for new code
export { useAuthStore, useUserStore } from "./store";

// Community-specific store (stays in entities)
export { useCommunityStore, UserActivityFilter } from "./communityStore";

// Display composable for user formatting
export { useUserDisplay } from "./useUserDisplay";

// Avatar upload/reset composable — единый источник логики upload/reset
// (drag-drop, paste, compression, progress). Используется ProfilePicture
// overlay на странице профиля.
export { useAvatarUpload, AVATAR_ACCEPT } from "./useAvatarUpload";

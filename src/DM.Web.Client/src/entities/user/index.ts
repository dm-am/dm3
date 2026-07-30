/**
 * User entity
 * @module entities/user
 *
 * Public API for user entity.
 * Use this for importing user-related types, components, and API.
 */

// Model (types and store)
export * from "./model";
export { useCommunityStore } from "./model/communityStore";

// API: the user directory plus the viewer's own account, settings and blacklist
export {
  userApi,
  accountApi,
  personalApi,
  blacklistApi,
  type UpdateProfilePayload,
} from "./api";

// UI Components
export {
  UserLink,
  UserRating,
  AvatarImg,
  UserAutocomplete,
  UserMultiSelect,
  UsernameInput,
} from "./ui";

// Helpers
export * from "./lib";

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

// UI Components
export { UserLink, UserOnline, UserRating, AvatarImg } from "./ui";

// Helpers
export * from "./lib";

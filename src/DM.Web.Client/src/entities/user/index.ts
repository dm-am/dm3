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

// API
export { userApi } from "./api";
export type { BestPost } from "./api";

// UI Components
export { UserLink, UserOnline, UserRating } from "./ui";

// Helpers
export * from "./lib";

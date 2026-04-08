/**
 * User entity types
 * @module entities/user/model/types
 *
 * Re-exports API types from shared for FSD compliance.
 * Entities should not define API types - those belong in shared.
 */

// Re-export all user types from shared
export {
  // Base types
  type Username,
  type User,
  type UserPicture,
  type Rating,
  type UsernameHistoryEntry,
  UserRole,
  Gender,
  AccessPolicy,
  // Filter
  UserActivityFilter,
  // Profile types
  type Birthday,
  type VisibilitySettings,
  type Contact,
  type FeaturedPost,
  type BestPost,
  type BbText,
  type UserProfile,
  type PersonalProfile,
  // Notes and moderation
  type UserProfileNote,
  type UserProfileNoteRequest,
  type PublicWarning,
  type PublicBan,
} from "@/shared/api/models/community/users";

// Re-export UserRef from common (lightweight user reference)
export { type UserRef } from "@/shared/api/models/common/user";

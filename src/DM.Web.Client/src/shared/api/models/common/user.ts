/**
 * Base user types for shared usage across entities
 * @module shared/api/models/common/user
 *
 * These types are extracted from entities/user to allow other entities
 * to reference User without creating entities→entities imports.
 *
 * FSD Rule: entities should import from shared, not from each other.
 */

// ============================================================================
// Base Types
// ============================================================================

/**
 * Branded type for username (unique user identifier string)
 */
export type Username = string & { readonly __brand: unique symbol };

/**
 * User role enum (matches backend UserRole)
 */
export enum UserRole {
  Guest = "Guest",
  RegularUser = "RegularUser",
  Mentor = "Mentor",
  Moderator = "Moderator",
  SeniorModerator = "SeniorModerator",
  Admin = "Admin",
  /** System user (Robot Administrator), cannot login */
  System = "System",
}

// ============================================================================
// User Sub-Types
// ============================================================================

/**
 * User profile picture
 * @see src/DM.Web.API/Dto/Users/UserPicture.cs
 */
export type UserPicture = {
  /** Picture identifier (for deletion, only in PersonalProfile context) */
  id?: string;
  /** Small picture URL (100x100, used in lists) */
  smallUrl?: string;
  /** Medium picture URL (200x200, used in profiles) */
  mediumUrl?: string;
  /** Original picture URL (full size, only in PersonalProfile context) */
  originalUrl?: string;
};

/**
 * Rating information
 * @see src/DM.Web.API/Dto/Personal/Rating.cs
 */
export type Rating = {
  /** Total posts count (quantity rating) */
  totalPosts: number;
  /** Sum of post review scores received (quality rating) */
  postReviewScoreSum: number;
};

/**
 * Username history entry
 */
export type UsernameHistoryEntry = {
  id: string;
  oldUsername: string;
  newUsername: string;
  changedUtc: string;
  approvedByUsername?: string;
};

// ============================================================================
// User DTO (base for all user references)
// ============================================================================

/**
 * Base user DTO for lists, author references, mentions, and cards.
 * This is the minimal user type used across all entities.
 *
 * @see src/DM.Web.API/Dto/Users/User.cs
 *
 * Used in:
 * - post.author, comment.author, game.master, etc.
 * - Lists and cards
 * - GET /v1/users (list)
 * - GET /v1/users/{username}
 */
export type User = {
  /** User identifier */
  id: string;
  /** User's display name (unique username) */
  username: Username;
  /** History of username changes */
  usernameHistory: UsernameHistoryEntry[];
  /** User role (RegularUser, Mentor, Moderator, etc.) */
  role: UserRole;
  /** User roles array (for multiple roles check) */
  roles?: UserRole[];
  /** Honorary status (visual badge for former moderators, helpers, etc.) */
  isHonorary: boolean;
  /** Newbie status (less than 100 posts) */
  isNewbie: boolean;
  /** User rating information (null if user has disabled rating display) */
  rating: Rating | null;
  /** User profile picture */
  picture: UserPicture;
  /** Small picture URL shortcut */
  smallPictureUrl?: string;
  /** Last activity moment (UTC) */
  lastActivityUtc: string | null;
  /** User status message */
  status?: string;
  /** User settings (only for authenticated user) */
  settings?: import("@/shared/api/models/personal").UserSettings;
};

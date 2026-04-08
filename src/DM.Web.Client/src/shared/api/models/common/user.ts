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

/**
 * User gender enum
 */
export enum Gender {
  Unknown = "Unknown",
  Male = "Male",
  Female = "Female",
}

/**
 * Access policy enum (for moderation restrictions)
 */
export enum AccessPolicy {
  NotSpecified = "NotSpecified",
  DemocraticBan = "DemocraticBan",
  FullBan = "FullBan",
  GlobalChatBan = "GlobalChatBan",
  RestrictContentEditing = "RestrictContentEditing",
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
  /** Whether rating display is enabled */
  isEnabled?: boolean;
  /** Total rating score (for display) */
  totalRating?: number;
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
// Module Status Counts (for status breakdowns)
// ============================================================================

/**
 * Count breakdown by module status (games/blogs)
 */
export type ModuleStatusCounts = {
  /** Number of items in Draft status */
  draft: number;
  /** Number of items in Active status */
  active: number;
  /** Number of items in Closed status */
  closed: number;
};

// ============================================================================
// Lightweight User References
// ============================================================================

/**
 * Unified lightweight user reference for lists, tooltips, and participant info.
 * Replaces: User in lists, GameAssistantInfo, BlogAssistantInfo
 * Only 5 fields vs 12+ in full User.
 *
 * Use this DTO for:
 * - Game lists (master, mentor, assistants, players, readers)
 * - Blog lists (owner, assistants)
 * - Any context where only identity and online status are needed
 *
 * For full user information, use User DTO below.
 */
export type UserRef = {
  /** User identifier */
  id: string;
  /** User's display name */
  username: Username;
  /** Last activity moment (UTC) - for online indicators */
  lastActivityUtc: string | null;
  /** User role (for displaying role badges [А], [С], [М], [Н], [Р]) */
  role: UserRole;
  /** Whether user is a newbie (less than 100 posts) - affects name color */
  isNewbie: boolean;
  /** Honorary status (visual badge [П] for former staff) */
  isHonorary: boolean;
};

// ============================================================================
// User DTO (full user information)
// ============================================================================

/**
 * Base user DTO for lists, author references, mentions, and cards.
 * Extends UserRef with additional profile information.
 *
 * @see src/DM.Web.API/Dto/Users/User.cs
 *
 * Used in:
 * - post.author, comment.author, game.master, etc.
 * - Lists and cards
 * - GET /v1/users (list)
 * - GET /v1/users/{username}
 */
export interface User extends UserRef {
  // Inherits: id, username, lastActivityUtc, role, isNewbie, isHonorary from UserRef

  /** History of username changes */
  usernameHistory: UsernameHistoryEntry[];
  /** User roles array (for multiple roles check) */
  roles?: UserRole[];
  /** User rating information (null if user has disabled rating display) */
  rating: Rating | null;
  /** User profile picture */
  picture: UserPicture;
  /** Small picture URL shortcut */
  smallPictureUrl?: string;
  /** Medium picture URL (from picture.mediumUrl) */
  mediumPictureUrl?: string;
  /** Original picture URL (from picture.originalUrl) */
  originalPictureUrl?: string;
  /** User status message */
  status?: string;
  /** User access policy (moderation restrictions) */
  accessPolicy?: AccessPolicy;
  /** User settings (only for authenticated user) */
  settings?: import("@/shared/api/models/personal").UserSettings;

  // Extended profile fields (present in UserProfile responses)
  /** User-defined extended information (BB-code rendered) */
  info?: InfoBbText;
  /** User gender */
  gender?: Gender;
  /** User birthday date string */
  birthdayDate?: string;
  /** User real name */
  name?: string;
  /** User location */
  location?: string;
  /** User contact information */
  contacts?: Contact[];
  /** User registration date (UTC) */
  registrationUtc?: string;
  /** User email (only for own account) */
  email?: string;

  // ========== Statistics for community list ==========
  /** Registration date (UTC) - for community list */
  registeredUtc?: string;
  /** Number of games where user is master or assistant */
  gamesHosting?: number;
  /** Games hosting breakdown by status (for tooltips) */
  gamesHostingByStatus?: ModuleStatusCounts;
  /** Number of games where user is a player */
  gamesPlaying?: number;
  /** Games playing breakdown by status (for tooltips) */
  gamesPlayingByStatus?: ModuleStatusCounts;
  /** Number of blogs where user is owner or assistant */
  blogsHosting?: number;
  /** Blogs hosting breakdown by status (for tooltips) */
  blogsHostingByStatus?: ModuleStatusCounts;
  /** Number of post reviews given */
  reviewsGiven?: number;
  /** Number of post reviews received */
  reviewsReceived?: number;
  /** Number of subscribers following this user */
  subscribersCount?: number;
  /** Subscriber usernames for tooltip display (limited to first 20) */
  subscriberUsernames?: string[];
}

/**
 * BB-code rendered text with source
 */
export type InfoBbText = {
  /** Original BB-code source */
  source?: string;
  /** Rendered HTML */
  html?: string;
};

/**
 * User contact information
 */
export type Contact = {
  /** Contact type (e.g., "Telegram", "Discord", "Email") */
  contactType: string;
  /** Contact value (e.g., username, email address) */
  value: string;
};

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
}

// ============================================================================
// User Sub-Types
// ============================================================================

/**
 * User profile picture
 * @see src/DM.Web.API/Features/Community/Users/UserDtos.cs
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
  /**
   * Intrinsic width of `originalUrl` in pixels. Only the original carries a
   * pair: small and medium are square crops at the size they are asked for,
   * while the original keeps the uploaded aspect ratio. Absent for an upload
   * stored before the pipeline measured one.
   */
  originalWidth?: number;
  /** Intrinsic height of `originalUrl` in pixels; travels with `originalWidth`. */
  originalHeight?: number;
};

/**
 * Rating information
 * @see src/DM.Web.API/Features/Community/Users/UserDtos.cs
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
  /** User role (for displaying role badges "[А]", "[С]", "[М]", "[Н]") */
  role: UserRole;
  /** Whether user is a newbie (less than 100 posts) - affects name color */
  isNewbie: boolean;
};

// ============================================================================
// User DTO (full user information)
// ============================================================================

/**
 * Base user DTO for lists, author references, mentions, and cards.
 * Extends UserRef with additional profile information.
 *
 * @see src/DM.Web.API/Features/Community/Users/UserDtos.cs
 *
 * Used in:
 * - post.author, comment.author, game.master, etc.
 * - Lists and cards
 * - GET /v1/users (list)
 * - GET /v1/users/{username}
 */
export interface User extends UserRef {
  // Inherits: id, username, lastActivityUtc, role, isNewbie from UserRef

  /** History of username changes */
  usernameHistory: UsernameHistoryEntry[];
  /** User rating information (null if user has disabled rating display) */
  rating: Rating | null;
  /** User profile picture (SSOT — all URLs live only here). */
  picture: UserPicture;
  /** User status message */
  status?: string;
  /** User access policy (moderation restrictions) */
  // accessPolicy is deliberately absent: the API does not send it for the
  // current user, and the ban rule it would gate exempts your own game and your
  // own blog, which a flat client-side flag cannot express. The server decides.
  /** User settings (only for authenticated user) */
  settings?: import("@/shared/api/models/personal").UserSettings;
  /**
   * Whether this account's rank is withheld for want of a second factor.
   *
   * Present only where the response is about the viewer themselves — the
   * sign-in, the step that finishes it and the own profile. Absent everywhere
   * else, because whether somebody else's rank is withheld is theirs to know,
   * so `undefined` reads as "this answer is not about you", not as "no".
   *
   * `role` beside it stays the recorded one: the account stopped being able,
   * not being an administrator.
   */
  privilegeWithheld?: boolean;

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
  /** Number of endorsements written by this user (about others). */
  endorsementsGiven?: number;
  /** Number of endorsements received by this user. */
  endorsementsReceived?: number;
  /** Number of game reviews written by this user (whole games, not posts). */
  gameReviewsGiven?: number;
  /** Number of game reviews written about the games this user masters. */
  gameReviewsReceived?: number;
  /** Forum topics authored by this user. */
  topicsAuthored?: number;
  /** Comments authored by this user (polymorphic across all comment-host entities). */
  commentsAuthored?: number;
  /** Messages this user has posted in the global chat. */
  globalChatMessages?: number;
  /** Bans this user has received — drives the "резиновая уточка" achievement chain. */
  bansReceived?: number;
  /** Games voluntarily dropped (retired characters with IsPlayerLeft=true) — drives the "дропы" chain. */
  gameDrops?: number;
  /** Publications (blog articles) authored by this user — drives the "публикации" chain. */
  publicationsAuthored?: number;
  /** Total likes received across topics+publications+comments+messages — drives the "лайки" chain. */
  likesReceived?: number;
  /** How many subscribers each of the three profile categories has. */
  subscribersByCategory?: SubscriberCounts;
  /**
   * Subscriber refs for profile-page display: at most 20, most recently active
   * first. A sample of the subscribers and not the members of any one category
   * — the cap is taken before the categories are considered, so
   * `subscribersByCategory` is what says how many there are.
   */
  subscribers?: SubscriberRef[];
}

/** Subscriber totals per profile category. */
export type SubscriberCounts = {
  games: number;
  blogs: number;
  topics: number;
};

/**
 * Lightweight subscriber reference: just enough to style + link, PLUS
 * the subscription settings bitmask so the profile UI can filter the
 * subscribers list per active tab ("subscribed to games / blogs / topics")
 * without a second round-trip. The bitmask matches the server-side
 * SubscriptionSettings [Flags] enum.
 */
export type SubscriberRef = {
  username: Username;
  lastActivityUtc: string | null;
  /** SubscriptionSettings flags (numeric bitmask). */
  settings: number;
};

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

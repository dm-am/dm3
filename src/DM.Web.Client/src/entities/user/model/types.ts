/**
 * User entity types
 * @module entities/user/model/types
 *
 * Base types (User, Username, UserRole, etc.) are re-exported from shared
 * to allow other entities to import them without entities→entities dependency.
 */

// ============================================================================
// Re-export base types from shared (for FSD compliance)
// ============================================================================

export {
  type Username,
  type User,
  type UserPicture,
  type Rating,
  type UsernameHistoryEntry,
  UserRole,
} from "@/shared/api/models/common/user";

// ============================================================================
// Entity-specific Types (not used by other entities)
// ============================================================================

export enum Gender {
  Unknown = "Unknown",
  Male = "Male",
  Female = "Female",
}

export enum AccessPolicy {
  NotSpecified = "NotSpecified",
  DemocraticBan = "DemocraticBan",
  FullBan = "FullBan",
  GlobalChatBan = "GlobalChatBan",
  RestrictContentEditing = "RestrictContentEditing",
}

export enum UserActivityFilter {
  Active = "Active",
  All = "All",
  Pending = "Pending",
}

/**
 * Birthday information
 * @see src/DM.Web.API/Dto/Personal/Birthday.cs
 */
export type Birthday = {
  /** Day of birth (1-31) */
  day: number;
  /** Month of birth (1-12) */
  month: number;
  /** Year of birth */
  year: number;
};

/**
 * User visibility settings
 * @see src/DM.Web.API/Dto/Users/VisibilitySettings.cs
 */
export type VisibilitySettings = {
  /** Whether birthday is visible to other users */
  showBirthday: boolean;
  /** Whether rating is visible in lists and cards */
  showRating: boolean;
};

/**
 * Contact information
 * @see src/DM.Web.API/Dto/Personal/Contact.cs
 */
export type Contact = {
  contactType: string;
  value: string;
};

/**
 * Featured/highlighted post for display in various contexts
 * @see src/DM.Web.API/Dto/Shared/FeaturedPost.cs
 */
export type FeaturedPost = {
  id: string;
  rating: number;
  gameId: string;
  gameTitle: string;
  roomId?: string;
  roomTitle?: string;
  authorUsername?: string;
  text?: string;
  reviewCount?: number;
  createdUtc?: string;
};

/**
 * BB-code rendered text (used for user info)
 */
export type BbText = {
  value: string;
};

/**
 * Public user profile DTO for profile pages
 * @see src/DM.Web.API/Dto/Users/UserProfile.cs
 *
 * Used for: GET /v1/users/{username}/profile
 */
export type UserProfile = User & {
  /** User-defined status message */
  status?: string;
  /** User-defined extended information (BB-code rendered) */
  info?: BbText;
  /** User gender */
  gender: Gender;
  /** User birthday information (null if user chose to hide birthday) */
  birthday?: Birthday;
  /** User real name */
  name?: string;
  /** User location */
  location?: string;
  /** User contact information */
  contacts: Contact[];
  /** User registration date (UTC) */
  registeredAtUtc: string;
  /** User's featured post (highest rated) */
  featuredPost?: FeaturedPost;
  /** Number of post reviews given to other users */
  postReviewsGiven: number;
  /** Number of post reviews received from other users */
  postReviewsReceived: number;
};

/**
 * Own profile DTO for account owner
 * @see src/DM.Web.API/Dto/Users/PersonalProfile.cs
 *
 * Used for: GET /v1/users/me/profile
 * Only the account owner can access this.
 */
export type PersonalProfile = Omit<UserProfile, "birthday"> & {
  /** User email address (only visible to account owner) */
  email: string;
  /** User's full birthday (always includes year if set) */
  birthday?: Birthday;
  /** Visibility settings (only visible to account owner) */
  visibility: VisibilitySettings;
};

// ============================================================================
// Other Types
// ============================================================================

/**
 * Personal note about another user
 */
export type UserProfileNote = {
  id: string;
  username: string;
  text: string;
  createdAt: string;
  updatedAt?: string | null;
};

/**
 * Request to create or update a user profile note
 */
export type UserProfileNoteRequest = {
  text: string;
};

/**
 * Public warning (shown on user profile)
 */
export type PublicWarning = {
  id: string;
  moderatorUsername: string;
  text: string;
  points: number;
  createdUtc: string;
  expiresUtc?: string;
};

/**
 * Public ban (shown on user profile)
 */
export type PublicBan = {
  id: string;
  moderatorUsername: string;
  reason: string;
  startUtc: string;
  endUtc?: string;
  isPermanent: boolean;
  isActive: boolean;
};

/**
 * User types for community API
 * @module shared/api/models/community/users
 *
 * All user-related API types that are needed by communityApi and other shared modules.
 * Entities should re-export from shared for FSD compliance.
 */

// Import for local use
import type { User as BaseUser } from "../common/user";
import { Gender as GenderEnum } from "../common/user";

// Re-export base types from common
export {
  Gender,
  type User,
  type Username,
  type UserPicture,
  type Rating,
  type UsernameHistoryEntry,
  UserRole,
  AccessPolicy,
} from "../common/user";

// User activity filter for lists (matches backend UserActivityFilter enum)
export enum UserActivityFilter {
  Active = "Active",
  All = "All",
  Pending = "Pending",
  Inactive = "Inactive",
}

/**
 * Birthday information
 * @see src/DM.Web.API/Dto/Personal/Birthday.cs
 */
export type Birthday = {
  day: number;
  month: number;
  year: number;
};

/**
 * User visibility settings
 * @see src/DM.Web.API/Dto/Users/VisibilitySettings.cs
 */
export type VisibilitySettings = {
  showBirthday: boolean;
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
 * BB-code rendered text
 */
export type BbText = {
  value: string;
};

/**
 * Public user profile DTO for profile pages
 * @see src/DM.Web.API/Dto/Users/UserProfile.cs
 */
export type UserProfile = BaseUser & {
  status?: string;
  info?: BbText;
  gender: GenderEnum;
  birthday?: Birthday;
  name?: string;
  location?: string;
  contacts: Contact[];
  registeredUtc: string;
  /** Number of PostReviews given by this user */
  postReviewsGiven: number;
  /** Number of PostReviews received by this user */
  postReviewsReceived: number;
};

/**
 * Own profile DTO for account owner
 * @see src/DM.Web.API/Dto/Users/PersonalProfile.cs
 */
export type PersonalProfile = Omit<UserProfile, "birthday"> & {
  email: string;
  birthday?: Birthday;
  visibility: VisibilitySettings;
};

/**
 * Personal note about another user
 */
export type UserProfileNote = {
  id: string;
  username: string;
  text: string;
  createdUtc: string;
  updatedUtc?: string | null;
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

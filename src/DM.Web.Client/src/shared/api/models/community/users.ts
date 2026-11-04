import type { Contact, User as BaseUser } from "../common/user";
import { Gender as GenderEnum } from "../common/user";

export {
  Gender,
  type User,
  type Username,
  type UserPicture,
  type Rating,
  type Contact,
  UserRole,
} from "../common/user";

// Matches the backend UserActivityFilter enum.
export enum UserActivityFilter {
  Active = "Active",
  All = "All",
  Inactive = "Inactive",
}

/** @see src/DM.Web.API/Features/Community/Users/UserDtos.cs */
export type Birthday = {
  day: number;
  month: number;
  year: number;
};

/** @see src/DM.Web.API/Features/Personal/Profiles/ProfileDtos.cs */
type VisibilitySettings = {
  showBirthday: boolean;
  /** Whether the rating is shown in the lists and cards; the profile shows it always. */
  showRating: boolean;
};

/**
 * The public profile.
 * @see src/DM.Web.API/Features/Community/Users/UserDtos.cs
 */
export type UserProfile = BaseUser & {
  status?: string;
  /** "О себе" rendered; as typed for its author on the author_edit audience. */
  info?: string;
  gender: GenderEnum;
  birthday?: Birthday;
  name?: string;
  location?: string;
  contacts: Contact[];
  registeredUtc: string;
};

/**
 * The profile of the account owner.
 * @see src/DM.Web.API/Features/Personal/Profiles/ProfileDtos.cs
 */
export type PersonalProfile = Omit<UserProfile, "birthday"> & {
  email: string;
  birthday?: Birthday;
  visibility: VisibilitySettings;
};

export type UserProfileNote = {
  id: string;
  username: string;
  text: string;
  createdUtc: string;
  modifiedUtc?: string | null;
};

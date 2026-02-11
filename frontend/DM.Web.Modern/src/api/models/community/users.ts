import type { Id, Served } from "@/api/models";
import type { UserSettings } from "@/api/models/community/user-settings";

export type Rating = {
  isEnabled: boolean;
  totalRating: number;
  totalPosts: number;
};

export type UserLogin = Id<string>;

export type UserContact = {
  title: string;
  value: string;
};

export enum Gender {
  Unknown = "Unknown",
  Male = "Male",
  Female = "Female",
}

export type User = {
  login: Served<UserLogin>;
  roles: Served<UserRole[]>;
  isHonorary: Served<boolean>;
  isNewbie: Served<boolean>;
  gender: Served<Gender>;
  birthdayDate: Served<string | null>;
  mediumPictureUrl: Served<string>;
  smallPictureUrl: Served<string>;
  rating: Served<Rating>;
  lastActivityUtc: Served<string | null>;
  status: string;
  name: string;
  location: string;
  contacts: Served<UserContact[]>;

  originalPictureUrl: Served<string>;
  info: string;
  registrationDateUtc: Served<string | null>;
  settings: UserSettings;
  accessPolicy: Served<AccessPolicy>;

  /** Email address (only available for current user's own profile) */
  email?: Served<string>;
};

export enum UserRole {
  Guest = "Guest",
  RegularUser = "RegularUser",
  Mentor = "Mentor",
  Moderator = "Moderator",
  SeniorModerator = "SeniorModerator",
  Admin = "Admin",
}

export type LoginHistoryEntry = {
  id: string;
  oldLogin: string;
  newLogin: string;
  changedUtc: string;
  approvedByLogin?: string;
};

export type BestPost = {
  id: string;
  text: string;
  gameTitle: string;
  gameId: string;
  roomTitle: string;
  roomId: string;
  authorLogin: string;
  rating: number;
  createdUtc: string;
};

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

export type ProfileNote = {
  text: string;
  updatedUtc?: string;
};

export type PublicWarning = {
  id: string;
  moderatorLogin: string;
  text: string;
  points: number;
  createdUtc: string;
  expiresUtc?: string;
};

export type PublicBan = {
  id: string;
  moderatorLogin: string;
  reason: string;
  startUtc: string;
  endUtc?: string;
  isPermanent: boolean;
  isActive: boolean;
}

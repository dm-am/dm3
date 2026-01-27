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
  onlineUtc: Served<string | null>;
  status: string;
  name: string;
  location: string;
  contacts: Served<UserContact[]>;

  originalPictureUrl: Served<string>;
  info: string;
  registrationDateUtc: Served<string | null>;
  settings: UserSettings;
  accessPolicy: Served<AccessPolicy>;
};

export enum UserRole {
  Guest = "Guest",
  RegularUser = "RegularUser",
  Mentor = "Mentor",
  Moderator = "Moderator",
  SeniorModerator = "SeniorModerator",
  Admin = "Admin",
}

export enum AccessPolicy {
  NotSpecified = "NotSpecified",
  DemocraticBan = "DemocraticBan",
  FullBan = "FullBan",
  ChatBan = "ChatBan",
  RestrictContentEditing = "RestrictContentEditing",
}

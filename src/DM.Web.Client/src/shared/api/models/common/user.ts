// In shared so that entities reference User without importing each other.
import type { UserSettings } from "../personal";

export type Username = string & { readonly __brand: unique symbol };

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

export enum Gender {
  Unknown = "Unknown",
  Male = "Male",
  Female = "Female",
}

/** @see src/DM.Web.API/Features/Community/Users/UserDtos.cs */
export type UserPicture = {
  /** Only in the PersonalProfile context, for deletion. */
  id?: string;
  /** 100x100, in lists */
  smallUrl?: string;
  /** 200x200, in profiles */
  mediumUrl?: string;
  /** Only in the PersonalProfile context. */
  originalUrl?: string;
  /**
   * Only the original keeps the uploaded aspect ratio, so only it carries a
   * size. Absent for an upload stored before the pipeline measured one.
   */
  originalWidth?: number;
  /** Travels with originalWidth. */
  originalHeight?: number;
};

/** @see src/DM.Web.API/Features/Community/Users/UserDtos.cs */
export type Rating = {
  /** The quantity rating. */
  totalPosts: number;
  /** The quality rating: the sum of post review scores received. */
  postReviewScoreSum: number;
  isEnabled?: boolean;
  totalRating?: number;
};

type UsernameHistoryEntry = {
  id: string;
  oldUsername: string;
  newUsername: string;
  changedUtc: string;
  approvedByUsername?: string;
};

/** Count breakdown by module status (games/blogs) */
type ModuleStatusCounts = {
  draft: number;
  active: number;
  closed: number;
};

/** The lightweight user of lists, tooltips and participants. */
export type UserRef = {
  id: string;
  username: Username;
  /** For the online indicator. */
  lastActivityUtc: string | null;
  /** For the role badges, which shared/config/roles.ts defines. */
  role: UserRole;
  /** Fewer than 100 posts: colours the name. */
  isNewbie: boolean;
};

/**
 * The user of lists, author references, mentions and cards.
 * @see src/DM.Web.API/Features/Community/Users/UserDtos.cs
 */
export interface User extends UserRef {
  usernameHistory: UsernameHistoryEntry[];
  /** Null if the user has disabled rating display. */
  rating: Rating | null;
  /** All URLs of the picture live only here. */
  picture: UserPicture;
  status?: string;
  // accessPolicy is absent: the ban rule it would gate exempts the reader's own
  // game and blog, which a flat client-side flag cannot express.
  /** Only for the authenticated user. */
  settings?: UserSettings;

  // Present in UserProfile responses.
  /** Display HTML from the server's BbConverter, or the BBCode source in edit mode. */
  info?: string;
  gender?: Gender;
  name?: string;
  location?: string;
  contacts?: Contact[];
  /** Only for the reader's own account. */
  email?: string;

  // The statistics of the community list.
  registeredUtc?: string;
  /** Games where the user is master or assistant. */
  gamesHosting?: number;
  gamesHostingByStatus?: ModuleStatusCounts;
  gamesPlaying?: number;
  gamesPlayingByStatus?: ModuleStatusCounts;
  /** Blogs where the user is owner or assistant. */
  blogsHosting?: number;
  blogsHostingByStatus?: ModuleStatusCounts;
  /** Post reviews. */
  reviewsGiven?: number;
  reviewsReceived?: number;
  endorsementsGiven?: number;
  endorsementsReceived?: number;
  /** Reviews of whole games, not posts. */
  gameReviewsGiven?: number;
  /** Reviews of the games this user masters. */
  gameReviewsReceived?: number;
  topicsAuthored?: number;
  /** Across every entity that hosts comments. */
  commentsAuthored?: number;
  globalChatMessages?: number;
  /** Drives the "резиновая уточка" achievement chain. */
  bansReceived?: number;
  /** Characters retired with IsPlayerLeft; drives the "дропы" chain. */
  gameDrops?: number;
  /** Drives the "публикации" chain. */
  publicationsAuthored?: number;
  /** On topics, publications, comments and messages; drives the "Нравится" chain. */
  likesReceived?: number;
  subscribersByCategory?: SubscriberCounts;
  /**
   * At most 20, most recently active first, capped before the categories:
   * subscribersByCategory says how many there are.
   */
  subscribers?: SubscriberRef[];
  /**
   * Whether this user is on the viewer's blacklist (profile answers only).
   * The other direction is not sent.
   */
  blockedByViewer?: boolean;
  /**
   * What the viewer may do with this user (profile answers only). Absent on
   * "Аккаунт удален" and in the answers of the moderation.
   */
  viewer?: ProfileViewer;
  /** The viewer's note about this user (profile answers only). */
  personalNote?: PersonalNote | null;
}

/** Mirrors PersonalNote. */
export type PersonalNote = {
  id: string;
  text: string;
  createdUtc: string;
  modifiedUtc?: string | null;
};

/** Mirrors ProfileViewer: each flag asked of the code that refuses its write. */
type ProfileViewer = {
  /** Signed in, not his own profile, and not blacklisted with messages blocked. */
  canMessage: boolean;
};

type SubscriberCounts = {
  games: number;
  blogs: number;
  topics: number;
};

/**
 * Carries the SubscriptionSettings bitmask, so the profile filters subscribers
 * per tab without a second request.
 */
export type SubscriberRef = {
  username: Username;
  lastActivityUtc: string | null;
  settings: number;
};

export type Contact = {
  /** "Telegram", "Discord", "Email" */
  contactType: string;
  value: string;
};

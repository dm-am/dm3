export type UserIpInfo = {
  ipAddress: string;
  firstSeenUtc: string;
  lastSeenUtc: string;
  loginsCount: number;
};

export type LoginRecord = {
  loginUtc: string;
  ipAddress: string;
  userAgent?: string;
  isSuccessful: boolean;
};

export type LinkedProfile = {
  userId: string;
  username: string;
  sharedIpsCount: number;
  lastSharedLoginUtc: string;
};

export type ModNote = {
  id: string;
  authorUsername: string;
  authorId: string;
  text: string;
  createdUtc: string;
  modifiedUtc?: string;
  canEdit: boolean;
  canDelete: boolean;
};

export type ViolationSummary = {
  totalWarnings: number;
  activeWarningPoints: number;
  totalBans: number;
  isCurrentlyBanned: boolean;
  currentBanEndUtc?: string;
  currentBanReason?: string;
};

export type ModerationPermissions = {
  canViewEmail: boolean;
  canViewIpAddresses: boolean;
  canViewLoginHistory: boolean;
  canViewLinkedProfiles: boolean;
  canViewModNotes: boolean;
  canCreateModNote: boolean;
  canIssueWarning: boolean;
  canIssueBan: boolean;
  canLiftBan: boolean;
  /** Can switch the moderation watch on this user (Moderator+). */
  canSetModerationWatch: boolean;
};

/**
 * Moderated profile extends UserProfile with moderation-specific data.
 * Inherits: id, username, role, rating, picture, status, info, gender,
 * birthday, name, location, contacts, registeredAtUtc, etc.
 */
export type ModeratedProfile = {
  // Inherited from UserProfile
  id: string;
  username: string;
  role: string;
  rating?: { totalPosts: number; postReviewScoreSum: number };
  picture?: { smallUrl?: string; mediumUrl?: string };
  status?: string;
  info?: string;
  gender?: string;
  birthday?: { day: number; month: number; year?: number };
  name?: string;
  location?: string;
  contacts?: { contactType: string; value: string }[];
  registeredUtc: string;
  isNewbie?: boolean;
  postReviewsGiven?: number;
  postReviewsReceived?: number;

  // Moderation-specific fields
  email?: string;
  ipAddresses?: UserIpInfo[];
  loginHistory?: LoginRecord[];
  linkedProfiles: LinkedProfile[];
  moderatorNotes: ModNote[];
  violations: ViolationSummary;
  /**
   * Moderation is watching what this user creates: while it is on, every game
   * and blog they start begins in premoderation, the way a newbie's does.
   * Set and cleared by hand, unlike the violators list, which is recomputed
   * from active warnings and bans and lapses when they expire.
   */
  isUnderModerationWatch: boolean;
  permissions: ModerationPermissions;
};

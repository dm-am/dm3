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

/** The profile as the moderation reads it. */
export type ModeratedProfile = {
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

  // Moderation-specific fields
  email?: string;
  ipAddresses?: UserIpInfo[];
  loginHistory?: LoginRecord[];
  linkedProfiles: LinkedProfile[];
  moderatorNotes: ModNote[];
  violations: ViolationSummary;
  /**
   * While on, every game and blog the user starts begins in premoderation. Set
   * by hand, unlike the violators list, which lapses with the warnings and bans.
   */
  isUnderModerationWatch: boolean;
  permissions: ModerationPermissions;
};

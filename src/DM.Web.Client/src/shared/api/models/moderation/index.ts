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
};

/**
 * Moderated profile extends UserProfile with moderation-specific data.
 * Inherits: id, username, role, rating, picture, status, info, gender,
 * birthday, name, location, contacts, registeredAtUtc, featuredPost, etc.
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
  isHonorary?: boolean;
  isNewbie?: boolean;
  featuredPost?: {
    id: string;
    gameId: string;
    roomId: string;
    gameTitle: string;
    roomTitle: string;
    text: string;
    rating: number;
  };
  postReviewsGiven?: number;
  postReviewsReceived?: number;

  // Moderation-specific fields
  email?: string;
  ipAddresses?: UserIpInfo[];
  loginHistory?: LoginRecord[];
  linkedProfiles: LinkedProfile[];
  moderatorNotes: ModNote[];
  violations: ViolationSummary;
  permissions: ModerationPermissions;
};

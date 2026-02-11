export type UserIpInfo = {
  ipAddress: string;
  firstSeenUtc: string;
  lastSeenUtc: string;
  loginCount: number;
};

export type LoginRecord = {
  loginUtc: string;
  ipAddress: string;
  userAgent?: string;
  isSuccessful: boolean;
};

export type LinkedProfile = {
  userId: string;
  login: string;
  sharedIpCount: number;
  lastSharedLoginUtc: string;
};

export type ModNote = {
  noteId: string;
  authorLogin: string;
  authorId: string;
  text: string;
  createdUtc: string;
  updatedUtc?: string;
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

export type ModerationProfile = {
  login: string;
  userId: string;
  email?: string;
  registrationDateUtc: string;
  ipAddresses?: UserIpInfo[];
  loginHistory?: LoginRecord[];
  linkedProfiles: LinkedProfile[];
  moderatorNotes: ModNote[];
  violations: ViolationSummary;
  permissions: ModerationPermissions;
};

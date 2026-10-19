import type { User } from "../common";
import type { Preferences } from "../personal";

/** A letter with a link went out; the receipt says how long the link lives. */
export type MailedLink = {
  linkLifetimeHours: number;
};

/** The first step of a registration; the name is chosen after the address is confirmed. */
export type RegisterCredentials = {
  email: string;
  password: string;
  acceptedRules: boolean;
  website?: string; // Honeypot field for bot protection
};

/**
 * The second step of a registration. The account starts in the device's zone;
 * absent where the browser will not say, and the account is then UTC.
 */
export type ActivationRequest = {
  username: string;
  timeZoneId?: string;
};

export type PendingInfo = {
  /** "ready" = token valid, "expired" = need resend */
  status: "ready" | "expired";
  email: string;
};

export type UsernameAvailability = {
  isAvailable: boolean;
  /**
   * "Taken" | "InvalidFormat". A name a former user left is "Taken" too: the
   * reader does the same thing either way.
   */
  reason?: string;
};

export type LoginCredentials = {
  email: string;
  password: string;
  website?: string; // Honeypot field for bot protection
  /** True keeps the session cookie past browser close; both lifetimes come from server configuration */
  rememberMe?: boolean;
};

/**
 * Mirrors LoginResponse: the viewer arrives wrapped with the preferences of the
 * first screen. Absent on twoFactorRequired, where no session exists yet.
 */
export type LoginResponse = {
  user?: User;
  preferences?: Preferences;
  /** The password was accepted and the second factor is still owed. */
  twoFactorRequired?: boolean;
};

/** Second step of a login: the code that answers the challenge. */
export type TwoFactorLoginRequest = {
  /** Six digits from the authenticator app, or one of the recovery codes. */
  code: string;
};

/** The identity is taken from the session. */
export type ChangeEmailRequest = {
  password: string;
  email: string;
};

export type RecoveryRequest = {
  email: string;
};

export type RecoveryResponse = {
  status: "PasswordResetSent" | "ActivationResent" | "NotFound";
  email: string;
  /** Absent when no letter went out. */
  linkLifetimeHours?: number;
};

/**
 * A removed account's address answers "Taken" like a live one: a guest at the
 * registration form is not owed that decision.
 */
export type EmailAvailability = {
  isAvailable: boolean;
  reason?: "Taken" | "PendingActivation";
};

export type PasswordResetTokenInfo = {
  /** "ready" = token valid, "expired" = need new reset request */
  status: "ready" | "expired";
};

export type SessionInfo = {
  id: string;
  isCurrent: boolean;
  persistent: boolean;
  expirationUtc: string;
  createdUtc: string;
  deviceInfo?: string;
  ipAddress?: string;
};

/** requestedUsername is set only after approval (POST /username-change/complete). */
export type UsernameChangeRequest = {
  id: string;
  currentUsername: string;
  requestedUsername?: string;
  reason: string;
  // Reached by the flow itself, not by a moderator; the latest request
  // arrives whatever state it is in.
  status: "Pending" | "Approved" | "Rejected" | "Completed" | "Expired";
  // An expiry means either nobody read the request or an approval went
  // unused; set by the server for "Expired" only.
  expiryReason?: "Unreviewed" | "ApprovalLapsed";
  createdUtc: string;
  approvalTokenExpiresUtc?: string;
  resolvedUtc?: string;
  resolvedByUsername?: string;
  resolverComment?: string;
};

/**
 * What a moderator's approval link is still good for; a value no request was
 * issued for is a 404, not a status here.
 */
export type UsernameChangeApprovalInfo = {
  status: "ready" | "expired" | "used";
  /** The name being changed. Sent for "ready" only. */
  currentUsername?: string;
  /** How long a link of this kind lives: the expired page says it. */
  linkLifetimeHours: number;
};

/** A request for permission; the new name is chosen later (POST /username-change/complete). */
export type CreateUsernameChangeRequest = {
  reason: string;
};

export type NotificationCategory =
  | "Messages"
  | "Forum"
  | "Games"
  | "Subscriptions"
  | "Security"
  | "Moderation"
  | "Blog";

type BotConnection = {
  connected: boolean;
  enabled: boolean;
  enabledCategories: NotificationCategory[];
};

export type NotificationSettings = {
  discord?: BotConnection;
  telegram?: BotConnection;
};

type UpdateBotConnection = {
  enabled?: boolean;
  enabledCategories?: NotificationCategory[];
};

export type UpdateNotificationSettingsRequest = {
  discord?: UpdateBotConnection;
  telegram?: UpdateBotConnection;
};

export type BotLinkResult = {
  code: string;
  expiresUtc: string;
};

export type SecurityEventType =
  | "LoginSuccess"
  | "LoginFailure"
  | "Logout"
  | "PasswordChange"
  | "EmailChange"
  | "SessionTerminated"
  | "LogoutElsewhere"
  | "PasswordResetRequest"
  | "PasswordResetComplete"
  | "AccountLocked"
  | "SuspiciousLogin"
  // Everything that happens to the second factor; unlisted, each printed as
  // the fallback line of an unknown type.
  | "TwoFactorEnabled"
  | "TwoFactorDisabled"
  | "TwoFactorRecoveryCodeUsed"
  | "TwoFactorRecoveryCodesReissued"
  | "TwoFactorRemovalScheduled"
  | "TwoFactorRemovalCancelled"
  | "TwoFactorRemovedByAdmin"
  | "TwoFactorSetupFailure";

/** Mirrors SecurityLogType on the server, which refuses anything else. */
export type SecurityLogType = "login" | "password" | "session" | "twofactor";

export type SecurityEvent = {
  id: string;
  eventType: SecurityEventType;
  timestampUtc: string;
  ipAddress?: string;
  deviceInfo?: string;
  details?: string;
};

/** No secret, no code, no hash: only what the owner needs to decide. */
export type TwoFactorStatus = {
  enabled: boolean;
  /** Since when it has been on. Absent while it is off. */
  enabledUtc?: string;
  /** Last time it was passed, by a code or by a recovery code. */
  lastVerifiedUtc?: string;
  recoveryCodesLeft: number;
  /** When a removal asked for by mail takes effect, if one is pending. */
  removalDueUtc?: string;
};

/** The secret, handed over once: asking again before confirming replaces it. */
export type TwoFactorSetup = {
  /** Base32, for typing into the app by hand. */
  secret: string;
  /** The same secret as an otpauth URI, for the QR code. */
  otpAuthUri: string;
};

/** A set of recovery codes, returned once and readable nowhere afterwards. */
export type RecoveryCodes = {
  codes: string[];
};

/**
 * Switching the factor off and reissuing the codes both cost the password and a
 * passed second factor, a recovery code included.
 */
export type TwoFactorConfirmedAction = {
  password: string;
  code: string;
};

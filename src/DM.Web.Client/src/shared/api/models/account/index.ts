import type { User } from "../common";
import type { Preferences } from "../personal";

/**
 * Credentials for new user registration (Step 1)
 * Username is chosen after email verification
 */
export type RegisterCredentials = {
  email: string;
  password: string;
  acceptedRules: boolean;
  website?: string; // Honeypot field for bot protection
};

/**
 * Response from activation token check
 */
export type PendingInfo = {
  /** "ready" = token valid, "expired" = need resend */
  status: "ready" | "expired";
  /** Email for display in UI */
  email: string;
};

/**
 * Result of username availability check
 */
export type UsernameAvailability = {
  isAvailable: boolean;
  /** Reason if not available: "taken" | "reserved" | "invalid_format" */
  reason?: string;
};

export type LoginCredentials = {
  /** User email address */
  email: string;
  password: string;
  website?: string; // Honeypot field for bot protection
  /** True keeps the session cookie past browser close; both lifetimes come from server configuration */
  rememberMe?: boolean;
};

/**
 * What POST /v1/account/login answers with.
 *
 * Mirrors DM.Web.API.Features.Account.Authentication.LoginResponse. The viewer
 * arrives wrapped, together with the preferences the interface needs to render
 * the first screen. Typed as a bare User here, the whole envelope went into the
 * store as though it were the viewer, and every field read off it was
 * undefined until the next boot reconciled the store against the server.
 *
 * The viewer is optional because one successful answer carries none:
 * `twoFactorRequired` means the password was accepted and the login is not
 * finished, so there is no session yet and nothing to describe. Typed as
 * required, that answer read as "signed in as nobody".
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

/**
 * Request to change email address
 * Note: user identity is taken from authenticated session
 */
export type ChangeEmailRequest = {
  password: string;
  email: string;
};

/**
 * Request for unified account recovery
 */
export type RecoveryRequest = {
  email: string;
};

/**
 * Response from recovery endpoint
 */
export type RecoveryResponse = {
  status: "PasswordResetSent" | "ActivationResent" | "NotFound";
  email: string;
};

/**
 * Response from email availability check
 */
export type EmailAvailability = {
  isAvailable: boolean;
  reason?: "Taken" | "PendingActivation";
};

/**
 * Response from password reset token check
 */
export type PasswordResetTokenInfo = {
  /** "ready" = token valid, "expired" = need new reset request */
  status: "ready" | "expired";
};

/**
 * Information about an active session
 */
export type SessionInfo = {
  id: string;
  isCurrent: boolean;
  persistent: boolean;
  expirationUtc: string;
  createdUtc: string;
  deviceInfo?: string;
  ipAddress?: string;
};

// === Username Change ===

/**
 * Username change request (requires moderation)
 * Note: requestedUsername is only set after approval via POST /username-change/complete
 */
export type UsernameChangeRequest = {
  id: string;
  currentUsername: string;
  requestedUsername?: string;
  reason: string;
  // Completed and Expired are reached by the flow itself rather than by a
  // moderator — one when the name is chosen, one when a deadline passes — and
  // GET /account/username-change returns the latest request whatever state it
  // is in, so both arrive here.
  status: "Pending" | "Approved" | "Rejected" | "Completed" | "Expired";
  // Expiry has two roads into one status and they mean opposite things: nobody
  // read the request, or an approval went unused. Set only when status is
  // "Expired", and computed by the server so the client never reads it out of
  // the resolution comment.
  expiryReason?: "Unreviewed" | "ApprovalLapsed";
  createdUtc: string;
  approvalTokenExpiresUtc?: string;
  resolvedUtc?: string;
  resolvedByUsername?: string;
  resolverComment?: string;
};

/**
 * What the approval link a moderator issued is still good for.
 *
 * Four answers, because they call for four different things from the reader: a
 * live approval opens the form, a closed window sends them back for a new
 * request, a link already spent asks nothing of them, and a value no request
 * was issued for (a 404, so not a status here) is a broken link.
 */
export type UsernameChangeApprovalInfo = {
  status: "ready" | "expired" | "used";
  /** The name being changed. Sent for "ready" only. */
  currentUsername?: string;
};

/**
 * Request to create username change request.
 * Note: User does NOT specify desired username here.
 * This is a request for permission to change username.
 * The new username is chosen later via POST /username-change/complete.
 */
export type CreateUsernameChangeRequest = {
  reason: string;
};

// === Notification Settings ===

/**
 * Category of notifications
 */
export type NotificationCategory =
  | "Messages"
  | "Forum"
  | "Games"
  | "Subscriptions"
  | "Security"
  | "Moderation"
  | "Blog";

/**
 * Bot connection for a notification channel (Telegram/Discord)
 */
export type BotConnection = {
  connected: boolean;
  enabled: boolean;
  enabledCategories: NotificationCategory[];
};

/**
 * All notification settings
 */
export type NotificationSettings = {
  discord?: BotConnection;
  telegram?: BotConnection;
};

/**
 * Update for a single bot connection
 */
export type UpdateBotConnection = {
  enabled?: boolean;
  enabledCategories?: NotificationCategory[];
};

/**
 * Request to update notification settings
 */
export type UpdateNotificationSettingsRequest = {
  discord?: UpdateBotConnection;
  telegram?: UpdateBotConnection;
};

// Backwards compatibility aliases
export type NotificationPreferences = NotificationSettings;
export type UpdateNotificationPreferences = UpdateNotificationSettingsRequest;

// === Bot Links ===

/**
 * Result of generating bot link code
 */
export type BotLinkResult = {
  code: string;
  expiresUtc: string;
};

// === Security History ===

/**
 * Type of security event
 */
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
  // Everything that happens to the second factor. The server has written
  // these nine since the factor shipped; unlisted here, all nine printed as
  // the one fallback line the journal shows an unknown type.
  | "TwoFactorEnabled"
  | "TwoFactorDisabled"
  | "TwoFactorRecoveryCodeUsed"
  | "TwoFactorRecoveryCodesReissued"
  | "TwoFactorRemovalScheduled"
  | "TwoFactorRemovalCancelled"
  | "TwoFactorRemovedByAdmin"
  | "TwoFactorRemovalRefused"
  | "TwoFactorSetupFailure";

/**
 * Which slice of the security journal to ask for. Mirrors SecurityLogType on
 * the server, which refuses anything else.
 */
export type SecurityLogType = "login" | "password" | "session" | "twofactor";

/**
 * Security audit log event
 */
export type SecurityEvent = {
  id: string;
  eventType: SecurityEventType;
  timestampUtc: string;
  ipAddress?: string;
  deviceInfo?: string;
  details?: string;
};

// === Two-factor authentication ===

/**
 * State of the second factor, as GET /v1/account/two-factor reports it.
 *
 * Nothing here can be used to pass the factor: no secret, no code, no hash.
 * The dates and the count are what the owner needs in order to decide whether
 * to reissue the codes or to switch the factor off.
 */
export type TwoFactorStatus = {
  enabled: boolean;
  /** Since when it has been on. Absent while it is off. */
  enabledUtc?: string;
  /** Last time it was passed, by a code or by a recovery code. */
  lastVerifiedUtc?: string;
  recoveryCodesLeft: number;
  /** When a removal asked for by mail takes effect, if one is pending. */
  removalDueUtc?: string;
  /** Whether this account's rank owes a factor. */
  required: boolean;
  /** Whether the rank is withheld for want of a factor. */
  privilegeWithheld: boolean;
};

/**
 * The secret, handed over once.
 *
 * It exists in this one answer and nowhere else: asking again before the
 * factor is confirmed replaces it rather than repeating it.
 */
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
 * Switching the factor off and reissuing the recovery codes cost the same: the
 * current password and a passed second factor, where a recovery code counts as
 * the second factor.
 */
export type TwoFactorConfirmedAction = {
  password: string;
  code: string;
};

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
  /** Remember session for 1 year (true) or 24 hours (false) */
  rememberMe?: boolean;
};

/**
 * Request to initiate password reset flow
 */
export type ResetPasswordRequest = {
  username: string;
  email: string;
};

/**
 * Request to change password (either with old password or reset token)
 * Note: user identity is taken from session (oldPassword flow) or token (reset flow)
 */
export type ChangePasswordRequest = {
  oldPassword?: string;
  newPassword: string;
  token?: string; // For reset flow (GUID)
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
 * Request to resend activation email
 */
export type ResendActivationRequest = {
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
 * Note: requestedUsername is only set after approval via POST /username-change/{token}
 */
export type UsernameChangeRequest = {
  id: string;
  currentUsername: string;
  reason: string;
  status: "Pending" | "Approved" | "Rejected";
  createdUtc: string;
  resolvedUtc?: string;
  resolvedByUsername?: string;
  resolverComment?: string;
};

/**
 * Request to create username change request.
 * Note: User does NOT specify desired username here.
 * This is a request for permission to change username.
 * The new username is chosen later via POST /username-change/{token}.
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
  | "Moderation";

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
  | "SuspiciousLogin";

/**
 * Security audit log event
 */
export type SecurityEvent = {
  id: string;
  eventType: SecurityEventType;
  description: string;
  timestampUtc: string;
  ipAddress?: string;
  deviceInfo?: string;
  details?: string;
};

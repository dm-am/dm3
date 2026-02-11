/**
 * Credentials for new user registration (Step 1)
 * Login is chosen after email verification
 */
export type RegisterCredentials = {
  email: string;
  password: string;
  acceptedRules: boolean;
  website?: string; // Honeypot field for bot protection
};

/**
 * Request to complete activation with login selection (Step 2)
 */
export type ActivationRequest = {
  token: string;
  login: string;
  expectedEmail?: string; // For idempotent retry detection
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
 * Result of login availability check
 */
export type LoginAvailability = {
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
  login: string;
  email: string;
};

/**
 * Request to change password (either with old password or reset token)
 */
export type ChangePasswordRequest = {
  login: string;
  oldPassword?: string;
  newPassword: string;
  token?: string; // For reset flow
};

/**
 * Request to change email address
 */
export type ChangeEmailRequest = {
  login: string;
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
  available: boolean;
  reason?: "Taken" | "PendingActivation";
};

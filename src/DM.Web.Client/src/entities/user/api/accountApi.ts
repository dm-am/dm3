import type { ListEnvelope, User } from "@/shared/api/models/common";
import type {
  LoginCredentials,
  LoginResponse,
  RegisterCredentials,
  ChangeEmailRequest,
  PendingInfo,
  UsernameAvailability,
  RecoveryRequest,
  RecoveryResponse,
  EmailAvailability,
  PasswordResetTokenInfo,
  SessionInfo,
  UsernameChangeRequest,
  CreateUsernameChangeRequest,
  NotificationPreferences,
  UpdateNotificationPreferences,
  BotLinkResult,
  SecurityEvent,
  SecurityLogType,
} from "@/shared/api/models/account";
import { Api, X_DM_ACCOUNT_TOKEN } from "@/shared/api";

/**
 * The viewer's own account: how they get in (registration, activation,
 * recovery, password, email), which devices hold a session, and how the site
 * reaches them (notification channels and bots).
 */
export default new (class AccountApi {
  /**
   * Register new user (Step 1 of email-first flow)
   * Creates pending registration and sends activation email.
   * Returns 201 on success with no body.
   */
  public register(credentials: RegisterCredentials) {
    return Api.post<void>("account/register", credentials);
  }

  /**
   * Get activation token status
   * Returns pending info if token exists
   */
  public getActivationInfo(token: string) {
    return Api.get<PendingInfo>("account/activation", undefined, undefined, {
      headers: { [X_DM_ACCOUNT_TOKEN]: token },
    });
  }

  /**
   * Complete activation with username selection (Step 2)
   * Creates user account and logs in automatically
   */
  public activate(
    token: string,
    // retryEmail, spelled the way ActivationRequest binds it. It used to be
    // sent as expectedEmail: no JsonPropertyName stands between them and the
    // serializer is camelCase, so the value arrived null on every request and
    // the idempotent-retry branch it feeds answered 410 to a repeat of an
    // activation that had already succeeded.
    request: { username: string; retryEmail?: string },
  ) {
    return Api.post<User>("account/activation", request, {
      headers: { [X_DM_ACCOUNT_TOKEN]: token },
    });
  }

  /**
   * Check if username is available for registration
   * Rate limited: 20 requests per minute
   */
  public checkUsername(username: string) {
    // In the body, not the query: the value is exactly what must not end up in
    // an access log, and the request line is logged with its query string.
    return Api.post<UsernameAvailability>("account/check-username", {
      username,
    });
  }

  /**
   * Unified account recovery (password reset or activation resend)
   * Detects what action is needed based on email status
   */
  public recover(email: string) {
    const request: RecoveryRequest = { email };
    return Api.post<RecoveryResponse>("account/recovery", request);
  }

  /**
   * Check if email is available for registration
   * Rate limited: 20 requests per minute
   */
  public checkEmail(email: string) {
    // Body, for the same reason as checkUsername above.
    return Api.post<EmailAvailability>("account/check-email", { email });
  }

  /**
   * Sign in with email and password (cookie-based)
   *
   * The refusal here is the answer to the sign-in form: a 403 names the state
   * of the account — banned, removed, locked out after too many attempts — and
   * the form shows that sentence under the password field. So this request
   * takes the refusal over and the response interceptor stays quiet about it.
   */
  public signIn(credentials: LoginCredentials) {
    return Api.post<LoginResponse>("account/login", credentials, {
      ownsRefusal: true,
    });
  }

  // Dropping the viewer is the session module's job: updateUser(null) owns the
  // persisted copy, and clearing it here as well made the transport a second
  // writer of the same key.
  public async signOut() {
    return Api.delete("account/login");
  }

  // Invitations live on personalApi, which types the same endpoint by what it
  // actually answers (ReceivedInvitation: entityId / entityType / entityTitle).
  // Two clients for one route is how the account page came to read gameId and
  // gameTitle out of a payload that carries neither.

  // Password management
  /**
   * Check password reset token validity
   */
  public getPasswordResetTokenInfo(token: string) {
    return Api.get<PasswordResetTokenInfo>(
      "account/password-reset",
      undefined,
      undefined,
      { headers: { [X_DM_ACCOUNT_TOKEN]: token } },
    );
  }

  /**
   * Change password (authenticated user)
   */
  public changePassword(request: { oldPassword: string; newPassword: string }) {
    return Api.post<User>("account/password", request);
  }

  /**
   * Complete password reset using token
   */
  public completePasswordReset(token: string, newPassword: string) {
    return Api.post<User>(
      "account/password-reset",
      { newPassword },
      { headers: { [X_DM_ACCOUNT_TOKEN]: token } },
    );
  }

  /**
   * Request email change (sends confirmation to new email)
   */
  public changeEmail(request: ChangeEmailRequest) {
    return Api.post<User>("account/email-change", request);
  }

  /**
   * Confirm email change via token
   */
  public confirmEmailChange(token: string) {
    return Api.post("account/email-change/confirm", undefined, {
      headers: { [X_DM_ACCOUNT_TOKEN]: token },
    });
  }

  // Session management
  /**
   * Get all active sessions
   */
  public getSessions() {
    return Api.get<ListEnvelope<SessionInfo>>("account/sessions");
  }

  /**
   * Terminate a specific session
   */
  public terminateSession(sessionId: string) {
    return Api.delete(`account/sessions/${sessionId}`);
  }

  /**
   * Logout from all devices except current
   */
  public logoutAll() {
    return Api.delete("account/sessions/others");
  }

  // ========== Username Change ==========

  /**
   * Get current user's username change request (if exists)
   * Returns 204 No Content if no request exists
   */
  public getUsernameChangeRequest() {
    return Api.get<UsernameChangeRequest | null>("account/username-change");
  }

  /**
   * Create a username change request
   */
  public createUsernameChangeRequest(request: CreateUsernameChangeRequest) {
    return Api.post<UsernameChangeRequest>("account/username-change", request);
  }

  // ========== Notification Preferences ==========

  /**
   * Get notification preferences for all channels
   */
  public getNotificationPreferences() {
    return Api.get<NotificationPreferences>("users/me/notifications/settings");
  }

  /**
   * Update notification preferences
   */
  public updateNotificationPreferences(request: UpdateNotificationPreferences) {
    return Api.patch<NotificationPreferences>(
      "users/me/notifications/settings",
      request,
    );
  }

  // ========== Bot Links ==========

  /**
   * Generate a code to link notification bot
   * Code valid for 10 minutes
   * @param type Bot type: "telegram" or "discord"
   */
  public generateBotCode(type: "telegram" | "discord") {
    return Api.post<BotLinkResult>(`users/me/notifications/bots/${type}`);
  }

  /**
   * Disconnect notification bot
   * @param type Bot type: "telegram" or "discord"
   */
  public disconnectBot(type: "telegram" | "discord") {
    return Api.delete(`users/me/notifications/bots/${type}`);
  }

  // ========== Security History ==========

  /**
   * Get security event history (logins, logouts, password changes, etc.)
   *
   * GET v1/account/logs. The address used to be `account/security`, which no
   * controller serves, and the cap used to be `limit`, which this endpoint does
   * not take — the section rendered empty and said nothing, because its loader
   * leaves the list alone on error.
   *
   * @param take Maximum number of events, 1..100 (default 50)
   * @param type Optional filter
   */
  public getSecurityHistory(take = 50, type?: SecurityLogType) {
    return Api.get<ListEnvelope<SecurityEvent>>("account/logs", {
      take,
      ...(type ? { type } : {}),
    });
  }

  /**
   * Get login history only (successful and failed logins)
   * @param take Maximum number of events (default 20)
   */
  public getLoginHistory(take = 20) {
    return this.getSecurityHistory(take, "login");
  }
})();

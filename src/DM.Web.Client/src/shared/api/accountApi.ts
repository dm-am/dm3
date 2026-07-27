import type { ListEnvelope, User } from "./models/common";
import type {
  LoginCredentials,
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
} from "./models/account";
import type { Invitation } from "./models/game";
import Api from "./client";

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
    return Api.get<PendingInfo>(`account/activation/${token}`);
  }

  /**
   * Complete activation with username selection (Step 2)
   * Creates user account and logs in automatically
   */
  public activate(
    token: string,
    request: { username: string; expectedEmail?: string },
  ) {
    return Api.post<User>(`account/activation/${token}`, request);
  }

  /**
   * Check if username is available for registration
   * Rate limited: 20 requests per minute
   */
  public checkUsername(username: string) {
    return Api.get<UsernameAvailability>(
      `account/check-username?username=${encodeURIComponent(username)}`,
    );
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
    return Api.get<EmailAvailability>(
      `account/check-email?email=${encodeURIComponent(email)}`,
    );
  }

  /**
   * Sign in with email/username and password (cookie-based)
   */
  public signIn(credentials: LoginCredentials) {
    return Api.post<User>("account/login", credentials);
  }

  public async signOut() {
    const result = await Api.delete("account/login");
    Api.logout();
    return result;
  }
  public isAuthenticated(): boolean {
    return Api.isAuthenticated();
  }

  // Invitations
  public getMyInvitations() {
    return Api.get<ListEnvelope<Invitation>>("users/me/invitations");
  }

  public acceptInvitation(tokenId: string) {
    return Api.post(`users/me/invitations/${tokenId}/accept`);
  }

  public rejectInvitation(tokenId: string) {
    return Api.post(`users/me/invitations/${tokenId}/reject`);
  }

  // Password management
  /**
   * Check password reset token validity
   */
  public getPasswordResetTokenInfo(token: string) {
    return Api.get<PasswordResetTokenInfo>(`account/password-reset/${token}`);
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
    return Api.post<User>(`account/password-reset/${token}`, { newPassword });
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
    return Api.post(`account/email-change/${token}`);
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

  // Mirror management
  /**
   * Get list of available mirrors
   */
  public getMirrors() {
    return Api.get<{
      currentMirrorId: string;
      mirrors: Array<{
        id: string;
        name: string;
        webUrl: string;
        isCurrent: boolean;
      }>;
    }>("mirrors");
  }

  /**
   * Get transfer token for switching to another mirror
   * @param targetMirror Target mirror ID
   * @param returnUrl Optional URL to redirect to after transfer
   */
  public getTransferToken(targetMirror: string, returnUrl?: string) {
    let url = `mirrors/transfer?targetMirror=${encodeURIComponent(targetMirror)}`;
    if (returnUrl) {
      url += `&returnUrl=${encodeURIComponent(returnUrl)}`;
    }
    return Api.get<{ transferUrl: string | null }>(url);
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
   * @param limit Maximum number of events (default 50)
   * @param type Optional filter: "logins", "password", "sessions"
   */
  public getSecurityHistory(limit = 50, type?: string) {
    const params = new URLSearchParams({ limit: String(limit) });
    if (type) params.append("type", type);
    return Api.get<ListEnvelope<SecurityEvent>>(`account/security?${params}`);
  }

  /**
   * Get login history only (successful and failed logins)
   * @param limit Maximum number of events (default 20)
   */
  public getLoginHistory(limit = 20) {
    return this.getSecurityHistory(limit, "logins");
  }
})();

import type { Envelope, ListEnvelope, User } from "@/shared/api/models/common";
import type {
  LoginCredentials,
  LoginResponse,
  TwoFactorLoginRequest,
  TwoFactorStatus,
  TwoFactorSetup,
  TwoFactorConfirmedAction,
  RecoveryCodes,
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
  UsernameChangeApprovalInfo,
  CreateUsernameChangeRequest,
  NotificationPreferences,
  UpdateNotificationPreferences,
  BotLinkResult,
  SecurityEvent,
  SecurityLogType,
} from "@/shared/api/models/account";
import { Api, X_DM_ACCOUNT_TOKEN } from "@/shared/api";

/**
 * A code on its way to the server, with every space taken out of it.
 *
 * The server trims the ends and nothing else, and it tells a code from the
 * device apart from a recovery code by shape: six digits or sixteen symbols. A
 * value pasted from a password manager as "123 456" is neither, so it goes into
 * the recovery-code branch and comes back refused - over a code that was right.
 * Done here rather than at the four call sites, because a call site that
 * forgets produces exactly that refusal and nothing points at the cause.
 */
const withoutSpaces = (code: string) => code.replace(/\s+/g, "");

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
    return Api.post<Envelope<User>>("account/activation", request, {
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

  /**
   * Finish a sign-in the first step left owing a second factor.
   *
   * The challenge is not in this request: it travels in the short-lived
   * `dm_2fa` cookie the first step set, which the browser sends by itself and
   * no script can read.
   *
   * Enveloped, unlike the first step of the same flow — the bare body there is
   * inherited debt the server declined to add to. A 403 belongs to the form
   * for the same reason it does on the first step: the account was banned or
   * removed in the minutes between the two, and that sentence is the answer to
   * the submit.
   */
  public completeTwoFactorLogin(request: TwoFactorLoginRequest) {
    return Api.post<Envelope<LoginResponse>>(
      "account/login/two-factor",
      { code: withoutSpaces(request.code) },
      { ownsRefusal: true },
    );
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

  /**
   * State of the approval token from the letter, before the form is shown.
   * 404 means no request was ever issued for it.
   */
  public getUsernameChangeApproval(token: string) {
    return Api.get<UsernameChangeApprovalInfo>(
      "account/username-change/approval",
      undefined,
      undefined,
      { headers: { [X_DM_ACCOUNT_TOKEN]: token } },
    );
  }

  /**
   * Take the approved change with the chosen name.
   * 409 when the name went to someone else while the approval waited.
   */
  public completeUsernameChange(token: string, username: string) {
    return Api.post<UsernameChangeRequest>(
      "account/username-change/complete",
      { username },
      { headers: { [X_DM_ACCOUNT_TOKEN]: token } },
    );
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

  // ========== Two-factor authentication ==========

  /** State of the viewer's own second factor. */
  public getTwoFactorStatus() {
    return Api.get<Envelope<TwoFactorStatus>>("account/two-factor");
  }

  /**
   * Ask for a secret to set the factor up with.
   *
   * The password is not a formality: a session lives a year, and without it a
   * stolen one would be enough to put somebody else's factor on the account.
   */
  public setupTwoFactor(password: string) {
    return Api.post<Envelope<TwoFactorSetup>>("account/two-factor/setup", {
      password,
    });
  }

  /**
   * Confirm the issued secret with the first code from the device.
   *
   * Answers with the recovery codes, which exist in this answer once and never
   * again, and ends every other session of the account.
   */
  public confirmTwoFactor(code: string) {
    return Api.post<Envelope<RecoveryCodes>>("account/two-factor/confirm", {
      code: withoutSpaces(code),
    });
  }

  /** Switch the factor off. Costs the password and a passed second factor. */
  public disableTwoFactor(request: TwoFactorConfirmedAction) {
    return Api.post<void>("account/two-factor/disable", {
      ...request,
      code: withoutSpaces(request.code),
    });
  }

  /** Reissue the recovery codes. The previous set stops working whole. */
  public reissueRecoveryCodes(request: TwoFactorConfirmedAction) {
    return Api.post<Envelope<RecoveryCodes>>(
      "account/two-factor/recovery-codes",
      { ...request, code: withoutSpaces(request.code) },
    );
  }

  /**
   * Ask, from the mailbox, for the factor to be taken off.
   *
   * Anonymous: the person who needs it cannot sign in. The server answers the
   * same for every address, so there is one result screen for every case.
   */
  public requestTwoFactorRemoval(email: string) {
    return Api.post<void>("account/two-factor/removal", { email });
  }

  /**
   * Follow the link from the letter: schedule the removal.
   *
   * Ends every session of the account and starts the waiting period. There is
   * no "check this token" call to ask first, so a dead link is learnt from the
   * answer to this one.
   */
  public scheduleTwoFactorRemoval(token: string) {
    return Api.post<void>("account/two-factor/removal/confirm", undefined, {
      headers: { [X_DM_ACCOUNT_TOKEN]: token },
    });
  }

  /** Follow the second link: call the scheduled removal off. */
  public cancelTwoFactorRemoval(token: string) {
    return Api.post<void>("account/two-factor/removal/cancel", undefined, {
      headers: { [X_DM_ACCOUNT_TOKEN]: token },
    });
  }

  /**
   * Take a colleague's factor off, as the second administrator.
   *
   * Hands the caller nothing: no session of the other account and none of its
   * rights, only a way in by password for its owner and the end of every
   * session it had.
   */
  public clearTwoFactorFor(username: string) {
    return Api.delete(`account/two-factor/users/${username}`);
  }
})();

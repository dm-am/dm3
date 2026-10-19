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
  ActivationRequest,
  ChangeEmailRequest,
  MailedLink,
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
  NotificationSettings,
  UpdateNotificationSettingsRequest,
  BotLinkResult,
  SecurityEvent,
  SecurityLogType,
} from "@/shared/api/models/account";
import { Api, X_DM_ACCOUNT_TOKEN } from "@/shared/api";
import { OWNS_REFUSAL, type RequestOptions } from "@/shared/api/interceptor";

// The server tells a code by its shape, and "123 456" from a password manager has neither.
const withoutSpaces = (code: string) => code.replace(/\s+/g, "");

// The token of a mailed link travels in a header: an address is written into the access log.
const byToken = (token: string) => ({
  headers: { [X_DM_ACCOUNT_TOKEN]: token },
});

type BotType = "telegram" | "discord";

export default new (class AccountApi {
  public register(credentials: RegisterCredentials) {
    return Api.post<Envelope<MailedLink>>(
      "account/register",
      credentials,
      OWNS_REFUSAL,
    );
  }

  public getActivationInfo(token: string) {
    return Api.get<PendingInfo>(
      "account/activation",
      undefined,
      undefined,
      byToken(token),
    );
  }

  /** Creates the account and signs it in. */
  public activate(token: string, request: ActivationRequest) {
    return Api.post<Envelope<User>>(
      "account/activation",
      request,
      byToken(token),
    );
  }

  public checkUsername(username: string) {
    // In the body: the request line is logged with its query string.
    return Api.post<UsernameAvailability>("account/check-username", {
      username,
    });
  }

  // A password reset or a resent activation; the activation page sets no flags.
  public recover(email: string, options?: RequestOptions) {
    const request: RecoveryRequest = { email };
    return Api.post<RecoveryResponse>("account/recovery", request, options);
  }

  public checkEmail(email: string) {
    // In the body, as in checkUsername.
    return Api.post<EmailAvailability>(
      "account/check-email",
      { email },
      OWNS_REFUSAL,
    );
  }

  // A 403 names the state of the account, which the form shows under the password.
  public signIn(credentials: LoginCredentials) {
    return Api.post<LoginResponse>("account/login", credentials, OWNS_REFUSAL);
  }

  // The challenge travels in the dm_2fa cookie, which no script can read.
  public completeTwoFactorLogin(request: TwoFactorLoginRequest) {
    return Api.post<Envelope<LoginResponse>>(
      "account/login/two-factor",
      { code: withoutSpaces(request.code) },
      OWNS_REFUSAL,
    );
  }

  // The session module drops the viewer: updateUser(null) owns the persisted copy.
  public signOut() {
    return Api.delete("account/login");
  }

  public getPasswordResetTokenInfo(token: string) {
    return Api.get<PasswordResetTokenInfo>(
      "account/password-reset",
      undefined,
      undefined,
      byToken(token),
    );
  }

  public changePassword(request: { oldPassword: string; newPassword: string }) {
    return Api.post<User>("account/password", request);
  }

  public completePasswordReset(token: string, newPassword: string) {
    return Api.post<User>(
      "account/password-reset",
      { newPassword },
      byToken(token),
    );
  }

  /** Sends the confirmation to the new address. */
  public changeEmail(request: ChangeEmailRequest) {
    return Api.post<Envelope<MailedLink>>("account/email-change", request);
  }

  public confirmEmailChange(token: string) {
    return Api.post("account/email-change/confirm", undefined, byToken(token));
  }

  public getSessions(options?: RequestOptions) {
    return Api.get<ListEnvelope<SessionInfo>>(
      "account/sessions",
      undefined,
      undefined,
      options,
    );
  }

  public terminateSession(sessionId: string) {
    return Api.delete(`account/sessions/${sessionId}`);
  }

  /** Every device but this one. */
  public logoutAll() {
    return Api.delete("account/sessions/others");
  }

  /** 204 when there is none; the profile page reads it without flags. */
  public getUsernameChangeRequest(options?: RequestOptions) {
    return Api.get<UsernameChangeRequest | null>(
      "account/username-change",
      undefined,
      undefined,
      options,
    );
  }

  public createUsernameChangeRequest(request: CreateUsernameChangeRequest) {
    return Api.post<UsernameChangeRequest>("account/username-change", request);
  }

  /** 404 when no request was ever issued for the token. */
  public getUsernameChangeApproval(token: string) {
    return Api.get<UsernameChangeApprovalInfo>(
      "account/username-change/approval",
      undefined,
      undefined,
      byToken(token),
    );
  }

  /** 409 when the name went to someone else while the approval waited. */
  public completeUsernameChange(token: string, username: string) {
    return Api.post<UsernameChangeRequest>(
      "account/username-change/complete",
      { username },
      byToken(token),
    );
  }

  public getNotificationPreferences(options?: RequestOptions) {
    return Api.get<NotificationSettings>(
      "users/me/notifications/settings",
      undefined,
      undefined,
      options,
    );
  }

  public updateNotificationPreferences(
    request: UpdateNotificationSettingsRequest,
  ) {
    return Api.patch<NotificationSettings>(
      "users/me/notifications/settings",
      request,
    );
  }

  /** The code is valid for ten minutes. */
  public generateBotCode(type: BotType) {
    return Api.post<BotLinkResult>(`users/me/notifications/bots/${type}`);
  }

  public disconnectBot(type: BotType) {
    return Api.delete(`users/me/notifications/bots/${type}`);
  }

  /** @param take 1..100. */
  public getSecurityHistory(
    take = 50,
    type?: SecurityLogType,
    options?: RequestOptions,
  ) {
    return Api.get<ListEnvelope<SecurityEvent>>(
      "account/logs",
      { take, ...(type ? { type } : {}) },
      undefined,
      options,
    );
  }

  public getTwoFactorStatus(options?: RequestOptions) {
    return Api.get<Envelope<TwoFactorStatus>>(
      "account/two-factor",
      undefined,
      undefined,
      options,
    );
  }

  // A session lives a year, so the password keeps a stolen one from setting a factor.
  public setupTwoFactor(password: string) {
    return Api.post<Envelope<TwoFactorSetup>>("account/two-factor/setup", {
      password,
    });
  }

  /** The recovery codes exist in this answer once; every other session ends. */
  public confirmTwoFactor(code: string) {
    return Api.post<Envelope<RecoveryCodes>>("account/two-factor/confirm", {
      code: withoutSpaces(code),
    });
  }

  /** Costs the password and a passed second factor. */
  public disableTwoFactor(request: TwoFactorConfirmedAction) {
    return Api.post<void>("account/two-factor/disable", {
      ...request,
      code: withoutSpaces(request.code),
    });
  }

  /** The previous set stops working whole. */
  public reissueRecoveryCodes(request: TwoFactorConfirmedAction) {
    return Api.post<Envelope<RecoveryCodes>>(
      "account/two-factor/recovery-codes",
      { ...request, code: withoutSpaces(request.code) },
    );
  }

  /** Anonymous, and answered alike for every address. */
  public requestTwoFactorRemoval(email: string, options?: RequestOptions) {
    return Api.post<Envelope<MailedLink>>(
      "account/two-factor/removal",
      { email },
      options,
    );
  }

  /** Ends every session and starts the wait; a dead link is learnt from the answer. */
  public scheduleTwoFactorRemoval(token: string, options?: RequestOptions) {
    return Api.post<void>("account/two-factor/removal/confirm", undefined, {
      ...byToken(token),
      ...options,
    });
  }

  public cancelTwoFactorRemoval(token: string) {
    return Api.post<void>(
      "account/two-factor/removal/cancel",
      undefined,
      byToken(token),
    );
  }

  /** Hands the caller nothing of the other account. */
  public clearTwoFactorFor(username: string, options?: RequestOptions) {
    return Api.delete(
      `account/two-factor/users/${username}`,
      undefined,
      options,
    );
  }
})();

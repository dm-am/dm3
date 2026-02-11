import type { Envelope, ListEnvelope } from "@/api/models/common";
import type { User } from "@/api/models/community";
import type {
  LoginCredentials,
  RegisterCredentials,
  ResetPasswordRequest,
  ChangePasswordRequest,
  ChangeEmailRequest,
  ActivationRequest,
  PendingInfo,
  LoginAvailability,
  ResendActivationRequest,
  RecoveryRequest,
  RecoveryResponse,
  EmailAvailability,
} from "@/api/models/account";
import type { Invitation } from "@/api/models/game";
import Api from "@/api";

export default new (class AccountApi {
  /**
   * Register new user (Step 1 of email-first flow)
   * Creates pending registration and sends activation email.
   * Returns 201 on success with no body.
   */
  public register(credentials: RegisterCredentials) {
    return Api.post<void>("account", credentials);
  }

  /**
   * Get activation token status
   * Returns pending info if token exists
   */
  public getActivationInfo(token: string) {
    return Api.get<PendingInfo>(`account/activate/${token}`);
  }

  /**
   * Complete activation with login selection (Step 2)
   * Creates user account and logs in automatically
   */
  public activate(request: ActivationRequest) {
    return Api.post<Envelope<User>>("account/activate", request);
  }

  /**
   * Check if login is available for registration
   * Rate limited: 20 requests per minute
   */
  public checkLogin(login: string) {
    return Api.get<LoginAvailability>(`account/check-login?login=${encodeURIComponent(login)}`);
  }

  /**
   * Resend activation email for pending registration
   * Always returns 200 regardless of whether email exists (security)
   */
  public resendActivation(email: string) {
    const request: ResendActivationRequest = { email };
    return Api.post("account/activation/resend", request);
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
    return Api.get<EmailAvailability>(`account/check-email?email=${encodeURIComponent(email)}`);
  }

  /**
   * Sign in with email/username and password (cookie-based)
   */
  public signIn(credentials: LoginCredentials) {
    return Api.post<Envelope<User>>("account/login", credentials);
  }

  public async fetchUser() {
    return await Api.get<Envelope<User>>("account");
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
   * Request password reset email
   */
  public resetPassword(request: ResetPasswordRequest) {
    return Api.post("account/password", request);
  }

  /**
   * Change password (using old password or reset token)
   */
  public changePassword(request: ChangePasswordRequest) {
    return Api.patch("account/password", request);
  }

  /**
   * Change email address
   */
  public changeEmail(request: ChangeEmailRequest) {
    return Api.patch("account/email", request);
  }

  /**
   * Confirm email change via token
   */
  public confirmEmailChange(token: string) {
    return Api.post(`account/email/${token}`);
  }

  // Session management
  /**
   * Logout from all devices except current
   */
  public logoutAll() {
    return Api.delete("account/login/all");
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
    }>("mirror");
  }

  /**
   * Get transfer token for switching to another mirror
   * @param targetMirror Target mirror ID
   * @param returnUrl Optional URL to redirect to after transfer
   */
  public getTransferToken(targetMirror: string, returnUrl?: string) {
    let url = `mirror/transfer?targetMirror=${encodeURIComponent(targetMirror)}`;
    if (returnUrl) {
      url += `&returnUrl=${encodeURIComponent(returnUrl)}`;
    }
    return Api.get<{ transferUrl: string | null }>(url);
  }
})();

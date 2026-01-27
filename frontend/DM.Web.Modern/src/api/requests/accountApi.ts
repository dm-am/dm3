import type { Envelope, ListEnvelope, BadRequestError } from "@/api/models/common";
import type { User } from "@/api/models/community";
import type {
  LoginCredentials,
  RegisterCredentials,
} from "@/api/models/account";
import type { Invitation } from "@/api/models/gaming";
import Api from "@/api";

interface OAuthSignInResult {
  success: boolean;
  user?: User;
  error?: BadRequestError;
}

export default new (class AccountApi {
  public register(credentials: RegisterCredentials) {
    return Api.post<Envelope<User>>("account", credentials);
  }
  public activate(token: string) {
    return Api.put<Envelope<User>>(`account/${token}`);
  }

  /**
   * Sign in using OAuth2 password grant flow
   */
  public async signInOAuth(credentials: LoginCredentials): Promise<OAuthSignInResult> {
    try {
      // Step 1: Get OAuth2 tokens
      const tokens = await Api.fetchOAuthToken(credentials.login, credentials.password);

      // Step 2: Store tokens
      Api.updateTokens(tokens);

      // Step 3: Fetch user profile
      const { data, error } = await Api.get<Envelope<User>>("account");

      if (data && data.resource) {
        return {
          success: true,
          user: data.resource,
        };
      } else if (error && "errors" in error) {
        return {
          success: false,
          error: error as BadRequestError,
        };
      }

      return {
        success: false,
        error: {
          type: "UnknownError",
          title: "Failed to fetch user profile",
          status: 500,
          traceId: "",
          errors: {},
        } as BadRequestError,
      };
    } catch (err) {
      console.error("OAuth sign-in error:", err);
      return {
        success: false,
        error: {
          type: "AuthenticationFailed",
          title: err instanceof Error ? err.message : "Authentication failed",
          status: 401,
          traceId: "",
          errors: {
            credentials: [err instanceof Error ? err.message : "Invalid credentials"],
          },
        } as BadRequestError,
      };
    }
  }

  /**
   * Legacy sign-in endpoint (for backward compatibility)
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
    return Api.get<ListEnvelope<Invitation>>("invitations");
  }

  public acceptInvitation(tokenId: string) {
    return Api.put(`invitations/${tokenId}/accept`);
  }

  public rejectInvitation(tokenId: string) {
    return Api.put(`invitations/${tokenId}/reject`);
  }
})();

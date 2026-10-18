import type {
  PersonalProfile,
  Gender,
  Contact,
} from "@/shared/api/models/community/users";
import type {
  Preferences,
  ReceivedInvitation,
} from "@/shared/api/models/personal";
import type { ListEnvelope } from "@/shared/api/models/common";
import { Api } from "@/shared/api";

/**
 * Profile update payload for PATCH /users/me/profile
 */
export type UpdateProfilePayload = {
  status?: string;
  name?: string;
  location?: string;
  gender?: Gender;
  birthday?: { day: number; month: number; year?: number } | null;
  info?: string;
  contacts?: Contact[];
  avatarUploadId?: string | null;
  visibility?: {
    showBirthday?: boolean;
    showRating?: boolean;
  };
};

/**
 * The refusal a caller takes over because its own screen carries it: the
 * window that asks before the avatar is reset prints a refusal in itself, and
 * the response interceptor has to stay quiet or the same sentence stands in
 * two places. Every call that leaves it out is unchanged.
 *
 * Declared here rather than imported: `shared/api` keeps its own copy of the
 * type to itself, and the client reads the two fields off the request by name,
 * so a structural twin carries them just as well.
 */
type RefusalFlags = {
  /** The screen shows a 403 and a 5xx itself. */
  ownsRefusal?: boolean;
  /** The screen shows a 429 itself, the wait included. */
  ownsRateLimit?: boolean;
};

/**
 * Personal API - manages current user's profile and preferences
 *
 * Endpoints:
 * - GET/PATCH /users/me/profile - profile data (status, name, info, contacts)
 * - GET/PATCH /users/me/preferences - display settings (theme, paging)
 */
export default new (class PersonalApi {
  /**
   * Get current user's full profile
   * Returns PersonalProfile including private fields (email, visibility settings)
   *
   * `probesSession` is for the one call that asks whether anybody is signed in
   * at all: its 401 means a guest, not an expired session.
   */
  public getMyProfile(options?: { probesSession?: boolean }) {
    return Api.get<PersonalProfile>(
      "users/me/profile",
      undefined,
      undefined,
      options,
    );
  }

  /**
   * Update current user's profile
   * All fields are optional - PATCH updates only provided fields
   */
  public updateMyProfile(profile: UpdateProfilePayload) {
    return Api.patch<PersonalProfile>("users/me/profile", profile);
  }

  /**
   * Reset my avatar. Idempotent: safe to call on a user
   * without an avatar.
   */
  public removeMyAvatar(options?: RefusalFlags) {
    return Api.delete("users/me/profile/avatar", undefined, options);
  }

  /**
   * Get current user's display preferences
   * Returns theme and paging settings
   */
  public getMyPreferences() {
    return Api.get<Preferences>("users/me/preferences");
  }

  /**
   * Update current user's display preferences
   */
  public updateMyPreferences(preferences: Partial<Preferences>) {
    return Api.patch<Preferences>("users/me/preferences", preferences);
  }

  // Invitations

  /**
   * Get invitations received by the current user
   */
  public getMyInvitations() {
    return Api.get<ListEnvelope<ReceivedInvitation>>("users/me/invitations");
  }

  /**
   * Accept an invitation
   */
  public acceptInvitation(invitationId: string) {
    return Api.post(`users/me/invitations/${invitationId}/accept`);
  }

  /**
   * Reject an invitation
   */
  public rejectInvitation(invitationId: string) {
    return Api.post(`users/me/invitations/${invitationId}/reject`);
  }
})();

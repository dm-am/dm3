import type { PersonalProfile, Gender, Contact } from "./models/community/users";
import type { Preferences, ReceivedInvitation } from "./models/personal";
import type { ListEnvelope } from "./models/common";
import Api from "./client";

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
   */
  public getMyProfile() {
    return Api.get<PersonalProfile>("users/me/profile");
  }

  /**
   * Update current user's profile
   * All fields are optional - PATCH updates only provided fields
   */
  public updateMyProfile(profile: UpdateProfilePayload) {
    return Api.patch<PersonalProfile>("users/me/profile", profile);
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

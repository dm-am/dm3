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
import { Api, RENDER_AUDIENCE } from "@/shared/api";
import type { RequestOptions } from "@/shared/api/interceptor";

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

export default new (class PersonalApi {
  /** With probesSession a 401 means a guest, not an expired session. */
  public getMyProfile(options?: RequestOptions) {
    return Api.get<PersonalProfile>(
      "users/me/profile",
      undefined,
      undefined,
      options,
    );
  }

  /** For the edit mode: "О себе" as its author typed it (the author_edit audience). */
  public getMyProfileForEdit() {
    return Api.get<PersonalProfile>(
      "users/me/profile",
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  }

  public updateMyProfile(profile: UpdateProfilePayload) {
    return Api.patch<PersonalProfile>("users/me/profile", profile);
  }

  /** Idempotent. */
  public removeMyAvatar(options?: RequestOptions) {
    return Api.delete("users/me/profile/avatar", undefined, options);
  }

  /** With probesSession a 401 means a guest, not an expired session. */
  public getMyPreferences(options?: RequestOptions) {
    return Api.get<Preferences>(
      "users/me/preferences",
      undefined,
      undefined,
      options,
    );
  }

  public updateMyPreferences(preferences: Partial<Preferences>) {
    return Api.patch<Preferences>("users/me/preferences", preferences);
  }

  public getMyInvitations(options?: RequestOptions) {
    return Api.get<ListEnvelope<ReceivedInvitation>>(
      "users/me/invitations",
      undefined,
      undefined,
      options,
    );
  }

  public acceptInvitation(invitationId: string) {
    return Api.post(`users/me/invitations/${invitationId}/accept`);
  }

  public rejectInvitation(invitationId: string) {
    return Api.post(`users/me/invitations/${invitationId}/reject`);
  }
})();

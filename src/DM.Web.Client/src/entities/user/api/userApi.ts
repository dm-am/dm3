/**
 * User entity API
 * @module entities/user/api
 */

import type { ListEnvelope, PagingQuery, ApiResult } from "@/shared/api/models/common";
import type {
  User,
  UserProfile,
  Username,
  UserRole,
  UsernameHistoryEntry,
  UserProfileNote,
  PublicWarning,
  PublicBan,
  UserActivityFilter,
} from "../model/types";
import { Api } from "@/shared/api";
import { BbRenderMode } from "@/shared/api";

/**
 * Best post for user profile
 */
export type BestPost = {
  id: string;
  gameId: string;
  gameTitle: string;
  roomId?: string;
  roomTitle?: string;
  text: string;
  rating: number;
  reviewCount: number;
  createdUtc: string;
};

class UserApi {
  /**
   * Get paginated list of users
   */
  public getUsers(q: PagingQuery & { filter?: UserActivityFilter; search?: string }) {
    return Api.get<ListEnvelope<User>>("users", q);
  }

  /**
   * Search users by username
   */
  public searchUsers(search: string, size: number = 10) {
    return Api.get<ListEnvelope<User>>("users", { search, size });
  }

  /**
   * Get users by role
   */
  public getUsersByRole(role: UserRole) {
    return Api.get<ListEnvelope<User>>("users", { role, take: 100 });
  }

  /**
   * Get user by username
   */
  public getUser(username: Username) {
    return Api.get<User>(`users/${username}`);
  }

  /**
   * Get user profile
   */
  public getUserProfile(username: Username) {
    return Api.get<UserProfile>(`users/${username}/profile`);
  }

  /**
   * Get user for editing (BB-code mode)
   */
  public getUserForUpdate(username: Username) {
    return Api.get<User>(
      `users/${username}`,
      undefined,
      BbRenderMode.Bb,
    );
  }

  /**
   * Get username change history
   */
  public getUsernameHistory(username: Username) {
    return Api.get<ListEnvelope<UsernameHistoryEntry>>(
      `users/${username}/username-history`,
    );
  }

  /**
   * Get user's best post
   */
  public getBestPost(username: Username) {
    return Api.get<BestPost>(`users/${username}/best-post`);
  }

  /**
   * Get personal note about a user (viewer's own note)
   */
  public getUserProfileNote(username: Username) {
    return Api.get<UserProfileNote>(`users/me/notes/${username}`);
  }

  /**
   * Create or update personal note about a user
   */
  public upsertUserProfileNote(username: Username, text: string) {
    return Api.put<UserProfileNote>(`users/me/notes/${username}`, { text });
  }

  /**
   * Delete personal note about a user
   */
  public deleteUserProfileNote(username: Username) {
    return Api.delete(`users/me/notes/${username}`);
  }

  /**
   * Get public warnings for a user
   */
  public getWarnings(username: Username) {
    return Api.get<ListEnvelope<PublicWarning>>(`users/${username}/warnings`);
  }

  /**
   * Get public bans for a user
   */
  public getBans(username: Username) {
    return Api.get<ListEnvelope<PublicBan>>(`users/${username}/bans`);
  }
}

export const userApi = new UserApi();
export default userApi;

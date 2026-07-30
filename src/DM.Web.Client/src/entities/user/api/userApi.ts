import type { ListEnvelope, PagingQuery } from "@/shared/api/models/common";
import type {
  User,
  UserProfile,
  Username,
  UserRole,
  UsernameHistoryEntry,
  UserProfileNote,
  UserEndorsement,
  UserEndorsementId,
  CreateUserEndorsementRequest,
  UpdateUserEndorsementRequest,
} from "@/shared/api/models/community";
import { UserActivityFilter } from "@/shared/api/models/community";
import { Api } from "@/shared/api";

/**
 * The user directory and everything hanging off a public profile: the user
 * record itself, past usernames, the viewer's private note about them, and
 * endorsements written about or by them.
 *
 * What is NOT here: the viewer's own account and settings (accountApi,
 * personalApi), the moderation view of a user (entities/moderation), and the
 * sitewide counters that used to travel with this client (entities/statistics).
 */
export default new (class UserApi {
  /**
   * Fetch users with the full search/filter/sort param set.
   *
   * The store (communityStore) builds backend-shaped params (q, activity,
   * role, isOnline, isNewbie, rating/games/blogs ranges,
   * registeredFrom/To, sort, sortOrder, take/skip) and passes them straight
   * through here — we must NOT drop unknown keys (a prior version cherry-picked
   * only a handful and silently lost every filter). Mirrors how
   * gameApi.searchGames forwards its params.
   *
   * A 1-indexed `number` is still accepted for convenience and converted to
   * `skip`; an explicit `skip` wins if both are present.
   */
  public getUsers(
    q: Record<string, string | number | boolean | undefined> & {
      number?: number;
      take?: number;
      skip?: number;
    },
  ) {
    const { number, ...rest } = q;
    const queryParams: Record<string, string | number | boolean | undefined> = {
      ...rest,
    };

    const pageSize = typeof q.take === "number" ? q.take : 20;
    queryParams.take = pageSize;

    if (queryParams.skip == null && number && number > 1) {
      queryParams.skip = (number - 1) * pageSize;
    }

    return Api.get<ListEnvelope<User>>("users", queryParams);
  }

  public searchUsers(
    search: string,
    size: number = 10,
    activity?: UserActivityFilter,
  ) {
    // activity omitted from the query when undefined - the backend then
    // applies its default Active-only filter
    return Api.get<ListEnvelope<User>>("users", {
      q: search,
      take: size,
      activity,
    });
  }

  /**
   * Get users by role via the dedicated endpoint.
   * Unlike GET /users (defaults to active users only), this endpoint
   * has no activity filter, so inactive staff are included.
   */
  public getUsersByRole(role: UserRole) {
    return Api.get<ListEnvelope<User>>(`users/by-role/${role}`);
  }

  public getUser(username: Username) {
    return Api.get<User>(`users/${username}`);
  }

  public getUserProfile(username: Username) {
    return Api.get<UserProfile>(`users/${username}/profile`);
  }

  public getUsernameHistory(username: Username) {
    return Api.get<ListEnvelope<UsernameHistoryEntry>>(
      `users/${username}/username-history`,
    );
  }

  /** Get personal note about a user (viewer's own note) */
  public getUserProfileNote(username: Username) {
    return Api.get<UserProfileNote>(`users/me/notes/${username}`);
  }

  /** Create or update personal note about a user */
  public upsertUserProfileNote(username: Username, text: string) {
    return Api.put<UserProfileNote>(`users/me/notes/${username}`, { text });
  }

  /** Delete personal note about a user */
  public deleteUserProfileNote(username: Username) {
    return Api.delete(`users/me/notes/${username}`);
  }

  /** Get endorsements written about a user (they are the recipient). */
  public getUserEndorsements(
    username: Username,
    q?: PagingQuery & {
      search?: string;
      sortBy?: "created" | "author";
      sortOrder?: "asc" | "desc";
    },
  ) {
    return Api.get<ListEnvelope<UserEndorsement>>(
      `users/${username}/endorsements`,
      this.buildEndorsementParams(q),
    );
  }

  /** Get endorsements written BY a user (they are the author). */
  public getWrittenUserEndorsements(
    username: Username,
    q?: PagingQuery & {
      search?: string;
      sortBy?: "created" | "author";
      sortOrder?: "asc" | "desc";
    },
  ) {
    return Api.get<ListEnvelope<UserEndorsement>>(
      `users/${username}/written-endorsements`,
      this.buildEndorsementParams(q),
    );
  }

  /**
   * One builder for the query string of both endorsement endpoints
   * (received / given) — when a new param is added on the BE,
   * only one place changes.
   */
  private buildEndorsementParams(
    q?: PagingQuery & {
      search?: string;
      sortBy?: "created" | "author";
      sortOrder?: "asc" | "desc";
    },
  ) {
    const params: Record<string, string | number | undefined> = {};
    if (!q) return params;
    if (q.skip != null) params.skip = q.skip;
    if (q.take != null) params.take = q.take;
    if (q.number != null) params.number = q.number;
    if (q.search) params.search = q.search;
    if (q.sortBy) params.sortBy = q.sortBy;
    if (q.sortOrder) params.sortOrder = q.sortOrder;
    return params;
  }

  /** Create endorsement for a user */
  public createUserEndorsement(
    username: Username,
    request: CreateUserEndorsementRequest,
  ) {
    return Api.post<UserEndorsement>(`users/${username}/endorsements`, request);
  }

  /** Update an existing endorsement */
  public updateUserEndorsement(
    id: UserEndorsementId,
    request: UpdateUserEndorsementRequest,
  ) {
    return Api.patch<UserEndorsement>(`endorsements/${id}`, request);
  }

  /** Delete an endorsement */
  public deleteUserEndorsement(id: UserEndorsementId) {
    return Api.delete(`endorsements/${id}`);
  }
})();

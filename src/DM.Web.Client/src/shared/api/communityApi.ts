import type { ListEnvelope, PagingQuery } from "./models/common";
import type {
  WebsiteTestimonial,
  WebsiteTestimonialId,
  WebsiteTestimonialsQuery,
  CreateWebsiteTestimonialRequest,
  UpdateWebsiteTestimonialRequest,
  // Legacy aliases
  TestimonialId,
  User,
  UserProfile,
  Username,
  UserRole,
  UsernameHistoryEntry,
  LiveStats,
  UserProfileNote,
  PublicWarning,
  PublicBan,
  UserEndorsement,
  UserEndorsementId,
  CreateUserEndorsementRequest,
  UpdateUserEndorsementRequest,
} from "./models/community";
import { UserActivityFilter } from "./models/community";
import Api from "./client";
import { RENDER_AUDIENCE } from "./audience";

export default new (class CommunityApi {
  // Note: Poll methods moved to @/entities/poll/api/pollApi.ts

  /**
   * Fetch users with the full search/filter/sort param set.
   *
   * The store (communityStore) builds backend-shaped params (q, activity,
   * role, isOnline, isHonorary, isNewbie, rating/games/blogs ranges,
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
  public getUserForUpdate(username: Username) {
    return Api.get<User>(
      `users/${username}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  }

  /**
   * Get list of website testimonials
   * @param q Query parameters with paging
   * @returns List of website testimonials
   */
  public getTestimonials(
    q: WebsiteTestimonialsQuery & { number?: number; size?: number },
  ) {
    // Convert number/size to skip/take for API
    const queryParams: Record<string, string | number | undefined> = {
      search: q.search,
      sortBy: q.sortBy,
      sortOrder: q.sortOrder,
    };

    const pageSize = q.size ?? q.take ?? 10;
    queryParams.take = pageSize;

    // Convert page number to skip (number is 1-indexed page)
    const pageNumber = q.number ?? 1;
    if (pageNumber > 1) {
      queryParams.skip = (pageNumber - 1) * pageSize;
    }

    return Api.get<ListEnvelope<WebsiteTestimonial>>(
      "testimonials",
      queryParams,
    );
  }

  /**
   * Create a new website testimonial
   * @param request Testimonial creation request (text only, 10-1000 chars)
   * @returns Created testimonial
   */
  public createTestimonial(request: CreateWebsiteTestimonialRequest) {
    return Api.post<WebsiteTestimonial>("testimonials", request);
  }

  /**
   * Update an existing website testimonial
   * @param id Testimonial identifier
   * @param request Update request (text only)
   * @returns Updated testimonial
   */
  public updateTestimonial(
    id: WebsiteTestimonialId,
    request: UpdateWebsiteTestimonialRequest,
  ) {
    return Api.patch<WebsiteTestimonial>(`testimonials/${id}`, request);
  }

  /**
   * Delete a website testimonial
   * @param id Testimonial identifier
   */
  public deleteTestimonial(id: WebsiteTestimonialId) {
    return Api.delete(`testimonials/${id}`);
  }

  // Legacy aliases for backwards compatibility
  /** @deprecated Use createTestimonial instead */
  public postTestimonial(testimonial: {
    text: string;
    authorUsername?: string;
  }) {
    return this.createTestimonial({ text: testimonial.text });
  }

  /** @deprecated Use deleteTestimonial instead */
  public removeTestimonial(id: TestimonialId) {
    return this.deleteTestimonial(id);
  }

  public getUsernameHistory(username: Username) {
    return Api.get<ListEnvelope<UsernameHistoryEntry>>(
      `users/${username}/username-history`,
    );
  }

  /** Get live community statistics (public endpoint, no auth needed) */
  public getLiveStats() {
    return Api.get<LiveStats>("stats", undefined, undefined, {
      skipAuth: true,
    });
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

  /** Get public warnings for a user */
  public getWarnings(username: Username) {
    return Api.get<ListEnvelope<PublicWarning>>(`users/${username}/warnings`);
  }

  /** Get public bans for a user */
  public getBans(username: Username) {
    return Api.get<ListEnvelope<PublicBan>>(`users/${username}/bans`);
  }

  /** Get endorsements written about a user (он — recipient). */
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

  /** Get endorsements written BY a user (он — author). */
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
   * Один builder для query-string'а обоих endpoint'ов рекомендаций
   * (полученные / написанные) — при добавлении нового параметра на BE
   * правится одно место.
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

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
  BestPost,
  LiveStats,
  UserProfileNote,
  PublicWarning,
  PublicBan,
} from "./models/community";
import { UserActivityFilter } from "./models/community";
import Api from "./client";
import { BbRenderMode } from "./bbRenderMode";

export default new (class CommunityApi {
  // Note: Poll methods moved to @/entities/poll/api/pollApi.ts

  public getUsers(
    q: PagingQuery & { filter?: UserActivityFilter; search?: string },
  ) {
    // Convert page number to skip/take for backend
    const pageSize = q.take ?? 20;
    const { number, skip, take, filter, search } = q;
    const queryParams: Record<string, string | number | boolean | undefined> = {
      filter,
      q: search,
      take: pageSize,
    };

    if (number && number > 1) {
      queryParams.skip = (number - 1) * pageSize;
    } else if (skip) {
      queryParams.skip = skip;
    }

    return Api.get<ListEnvelope<User>>("users", queryParams);
  }
  public searchUsers(search: string, size: number = 10) {
    return Api.get<ListEnvelope<User>>("users", { q: search, take: size });
  }
  public getUsersByRole(role: UserRole) {
    return Api.get<ListEnvelope<User>>("users", { role, take: 100 });
  }

  public getUser(username: Username) {
    return Api.get<User>(`users/${username}`);
  }
  public getUserProfile(username: Username) {
    return Api.get<UserProfile>(`users/${username}/profile`);
  }
  public getUserForUpdate(username: Username) {
    return Api.get<User>(`users/${username}`, undefined, BbRenderMode.Bb);
  }

  /**
   * Get list of website testimonials
   * @param q Query parameters with paging
   * @returns List of website testimonials
   */
  public getTestimonials(q: WebsiteTestimonialsQuery & { number?: number; size?: number }) {
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

    return Api.get<ListEnvelope<WebsiteTestimonial>>("testimonials", queryParams);
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
  public postTestimonial(testimonial: { text: string; authorUsername?: string }) {
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
  public getBestPost(username: Username) {
    return Api.get<BestPost>(`users/${username}/best-post`);
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
})();

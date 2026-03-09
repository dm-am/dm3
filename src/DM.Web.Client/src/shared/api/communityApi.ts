import type {
  ListEnvelope,
  PagingQuery,
  ApiResult,
} from "./models/common";
import type {
  Poll,
  PollId,
  PollOptionId,
  WebsiteReview,
  WebsiteReviewId,
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
import type { Patch, Post } from "./models";

export default new (class CommunityApi {
  public getPolls(q: PagingQuery, onlyActive: boolean) {
    return Api.get<ListEnvelope<Poll>>("polls", {
      ...q,
      onlyActive,
    });
  }
  public postPollVote(pollId: PollId, optionId: PollOptionId) {
    return Api.post<Poll>(
      `polls/${pollId}/vote?optionId=${optionId}`,
    );
  }
  public deletePollVote(pollId: PollId) {
    return Api.delete(`polls/${pollId}/vote`) as Promise<
      ApiResult<Poll>
    >;
  }
  public postPoll(poll: Post<Poll>) {
    return Api.post<Poll>("polls/global", poll);
  }
  public patchPoll(pollId: PollId, poll: Patch<Poll>) {
    return Api.patch<Poll>(`polls/${pollId}`, poll);
  }

  public getUsers(q: PagingQuery & { filter?: UserActivityFilter; search?: string }) {
    return Api.get<ListEnvelope<User>>("users", q);
  }
  public searchUsers(search: string, size: number = 10) {
    return Api.get<ListEnvelope<User>>("users", { search, size });
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
    return Api.get<User>(
      `users/${username}`,
      undefined,
      BbRenderMode.Bb,
    );
  }

  public getWebsiteReviews(q: PagingQuery, onlyApproved: boolean) {
    return Api.get<ListEnvelope<WebsiteReview>>("reviews/platform", {
      ...q,
      approved: onlyApproved || undefined,
    });
  }
  public postWebsiteReview(review: { text: string; authorUsername: string }) {
    return Api.post<WebsiteReview>("reviews/platform", review);
  }
  public updateWebsiteReview(
    id: WebsiteReviewId,
    review: Patch<WebsiteReview>,
  ) {
    return Api.patch<WebsiteReview>(`reviews/${id}`, review);
  }
  public removeWebsiteReview(id: WebsiteReviewId) {
    return Api.delete(`reviews/${id}`);
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
    return Api.get<LiveStats>("stats", undefined, undefined, { skipAuth: true });
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

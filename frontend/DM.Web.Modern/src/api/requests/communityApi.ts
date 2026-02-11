import type {
  ListEnvelope,
  Envelope,
  PagingQuery,
  ApiResult,
} from "@/api/models/common";
import type {
  Poll,
  PollId,
  PollOptionId,
  WebsiteReview,
  WebsiteReviewId,
  User,
  UserLogin,
  UserRole,
  LoginHistoryEntry,
  BestPost,
  LiveStats,
  ProfileNote,
  PublicWarning,
  PublicBan,
} from "@/api/models/community";
import { UserActivityFilter } from "@/api/models/community";
import Api from "@/api";
import { BbRenderMode } from "../bbRenderMode";
import type { Patch, Post } from "@/api/models";

export default new (class CommunityApi {
  public getPolls(q: PagingQuery, onlyActive: boolean) {
    return Api.get<ListEnvelope<Poll>>("polls", {
      ...q,
      onlyActive,
    });
  }
  public postPollVote(pollId: PollId, optionId: PollOptionId) {
    return Api.post<Envelope<Poll>>(
      `polls/${pollId}/vote?optionId=${optionId}`,
    );
  }
  public deletePollVote(pollId: PollId) {
    return Api.delete(`polls/${pollId}/vote`) as Promise<
      ApiResult<Envelope<Poll>>
    >;
  }
  public postPoll(poll: Post<Poll>) {
    return Api.post<Envelope<Poll>>("polls/global", poll);
  }
  public patchPoll(pollId: PollId, poll: Patch<Poll>) {
    return Api.patch<Envelope<Poll>>(`polls/${pollId}`, poll);
  }

  public getUsers(q: PagingQuery & { filter?: UserActivityFilter; search?: string }) {
    return Api.get<ListEnvelope<User>>("users", q);
  }
  public searchUsers(search: string, size: number = 10) {
    return Api.get<ListEnvelope<User>>("users", { search, size });
  }
  public getUsersByRole(role: UserRole) {
    return Api.get<ListEnvelope<User>>(`users/by-role/${role}`);
  }

  public getUser(login: UserLogin) {
    return Api.get<Envelope<User>>(`users/${login}/details`);
  }
  public getUserForUpdate(login: UserLogin) {
    return Api.get<Envelope<User>>(
      `users/${login}/details`,
      undefined,
      BbRenderMode.Bb,
    );
  }
  public updateUser(login: UserLogin, user: Patch<User>) {
    return Api.patch<Envelope<User>>(`users/${login}/details`, user);
  }

  public getWebsiteReviews(q: PagingQuery, onlyApproved: boolean) {
    return Api.get<ListEnvelope<WebsiteReview>>("reviews/platform", {
      ...q,
      approved: onlyApproved || undefined,
    });
  }
  public postWebsiteReview(review: { text: string; authorLogin: string }) {
    return Api.post<Envelope<WebsiteReview>>("reviews/platform", review);
  }
  public updateWebsiteReview(
    id: WebsiteReviewId,
    review: Patch<WebsiteReview>,
  ) {
    return Api.patch<Envelope<WebsiteReview>>(`reviews/${id}`, review);
  }
  public removeWebsiteReview(id: WebsiteReviewId) {
    return Api.delete(`reviews/${id}`);
  }

  public getLoginHistory(login: UserLogin) {
    return Api.get<ListEnvelope<LoginHistoryEntry>>(
      `users/${login}/login-history`,
    );
  }
  public getBestPost(login: UserLogin) {
    return Api.get<Envelope<BestPost>>(`users/${login}/best-post`);
  }

  /** Get live community statistics (public endpoint, no auth needed) */
  public getLiveStats() {
    return Api.get<Envelope<LiveStats>>("stats", undefined, undefined, { skipAuth: true });
  }

  /** Get personal note about a user (viewer's own note) */
  public getProfileNote(login: UserLogin) {
    return Api.get<Envelope<ProfileNote>>(`users/${login}/notes`);
  }

  /** Create or update personal note about a user */
  public upsertProfileNote(login: UserLogin, text: string) {
    return Api.put<Envelope<ProfileNote>>(`users/${login}/notes`, { text });
  }

  /** Delete personal note about a user */
  public deleteProfileNote(login: UserLogin) {
    return Api.delete(`users/${login}/notes`);
  }

  /** Get public warnings for a user */
  public getWarnings(login: UserLogin) {
    return Api.get<ListEnvelope<PublicWarning>>(`users/${login}/warnings`);
  }

  /** Get public bans for a user */
  public getBans(login: UserLogin) {
    return Api.get<ListEnvelope<PublicBan>>(`users/${login}/bans`);
  }
})();

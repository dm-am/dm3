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
} from "@/api/models/community";
import Api from "@/api";
import { BbRenderMode } from "../bbRenderMode";
import type { AxiosProgressEvent } from "axios";
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

  public getUsers(q: PagingQuery) {
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
  public uploadUserPicture(
    login: UserLogin,
    files: FormData,
    progressCallback: (event: AxiosProgressEvent) => void,
  ) {
    return Api.postFile<Envelope<User>>(
      `users/${login}/uploads`,
      files,
      progressCallback,
    );
  }

  public getWebsiteReviews(q: PagingQuery, onlyApproved: boolean) {
    return Api.get<ListEnvelope<WebsiteReview>>("websitereviews", {
      ...q,
      onlyApproved,
    });
  }
  public postWebsiteReview(review: { text: string; authorLogin: string }) {
    return Api.post<Envelope<WebsiteReview>>("websitereviews", review);
  }
  public updateWebsiteReview(
    id: WebsiteReviewId,
    review: Patch<WebsiteReview>,
  ) {
    return Api.patch<Envelope<WebsiteReview>>(`websitereviews/${id}`, review);
  }
  public removeWebsiteReview(id: WebsiteReviewId) {
    return Api.delete(`websitereviews/${id}`);
  }
})();

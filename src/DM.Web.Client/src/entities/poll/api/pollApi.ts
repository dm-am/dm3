import type { ListEnvelope, ApiResult } from "@/shared/api/models/common";
import type {
  Poll,
  PollId,
  PollOptionId,
  PollsSearchParams,
} from "../model/types";
import { Api } from "@/shared/api";
import type { Patch, Post } from "@/shared/api/models";

export default new (class PollApi {
  public getPolls(params: PollsSearchParams) {
    const queryParams: Record<string, string | number | boolean | undefined> =
      {};

    if (params.status) queryParams.status = params.status;
    if (params.isAnonymous !== undefined)
      queryParams.isAnonymous = params.isAnonymous;
    if (params.search) queryParams.search = params.search;
    if (params.startsFromUtc) queryParams.startsFromUtc = params.startsFromUtc;
    if (params.startsToUtc) queryParams.startsToUtc = params.startsToUtc;
    if (params.endsFromUtc) queryParams.endsFromUtc = params.endsFromUtc;
    if (params.endsToUtc) queryParams.endsToUtc = params.endsToUtc;
    if (params.sortBy) queryParams.sortBy = params.sortBy;
    if (params.sortOrder) queryParams.sortOrder = params.sortOrder;

    // Page size (defaults to 20 if not specified)
    const pageSize = params.size || 20;
    queryParams.take = pageSize;

    // Convert page number to skip (number is 1-indexed page)
    if (params.number && params.number > 1) {
      queryParams.skip = (params.number - 1) * pageSize;
    }

    return Api.get<ListEnvelope<Poll>>("polls", queryParams);
  }

  /** Fetch active polls for sidebar (max 3) */
  public getActivePolls() {
    return Api.get<ListEnvelope<Poll>>("polls", {
      status: "Active",
      take: 3,
      sortBy: "ends",
      sortOrder: "asc",
    });
  }

  public postPollVote(pollId: PollId, optionId: PollOptionId) {
    return Api.post<Poll>(`polls/${pollId}/vote?optionId=${optionId}`);
  }

  public deletePollVote(pollId: PollId) {
    return Api.delete(`polls/${pollId}/vote`) as Promise<ApiResult<Poll>>;
  }

  public postPoll(poll: Post<Poll>) {
    return Api.post<Poll>("polls", poll);
  }

  public patchPoll(pollId: PollId, poll: Patch<Poll>) {
    return Api.patch<Poll>(`polls/${pollId}`, poll);
  }
})();

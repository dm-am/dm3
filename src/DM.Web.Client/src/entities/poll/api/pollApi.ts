import type {
  Envelope,
  ListEnvelope,
  ApiResult,
} from "@/shared/api/models/common";
import type {
  CreatePollRequest,
  Poll,
  PollId,
  PollOptionId,
  PollsSearchParams,
  UpdatePollRequest,
} from "../model/types";
import { Api } from "@/shared/api";

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

    const pageSize = params.size || 20;
    queryParams.take = pageSize;

    if (params.number && params.number > 1) {
      queryParams.skip = (params.number - 1) * pageSize;
    }

    return Api.get<ListEnvelope<Poll>>("polls", queryParams);
  }

  public getActivePolls() {
    return Api.get<ListEnvelope<Poll>>("polls", {
      status: "Active",
      take: 3,
      sortBy: "ends",
      sortOrder: "asc",
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

  public postPoll(poll: CreatePollRequest) {
    return Api.post<Envelope<Poll>>("polls", poll);
  }

  public patchPoll(pollId: PollId, poll: UpdatePollRequest) {
    return Api.patch<Envelope<Poll>>(`polls/${pollId}`, poll);
  }
})();

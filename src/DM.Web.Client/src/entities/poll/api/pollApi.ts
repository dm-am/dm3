import type {
  ListEnvelope,
  PagingQuery,
  ApiResult,
} from "@/shared/api/models/common";
import type { Poll, PollId, PollOptionId } from "../model/types";
import { Api } from "@/shared/api";
import type { Patch, Post } from "@/shared/api/models";

export default new (class PollApi {
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
})();

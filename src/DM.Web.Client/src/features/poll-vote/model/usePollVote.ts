import { usePollsStore } from "@/entities/poll";
import type { PollId, PollOptionId } from "@/entities/poll";
import { notifyFailure } from "@/shared/lib/errors";

export function usePollVote() {
  const { vote, unvote } = usePollsStore();

  async function voteForOption(pollId: PollId, optionId: PollOptionId) {
    const { error } = await vote(pollId, optionId);
    if (error) notifyFailure(error, "Не удалось проголосовать в опросе");
  }

  async function cancelVote(pollId: PollId) {
    const { error } = await unvote(pollId);
    if (error) notifyFailure(error, "Не удалось отменить голос");
  }

  return { voteForOption, cancelVote };
}

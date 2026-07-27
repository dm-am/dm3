import { usePollsStore } from "@/entities/poll";
import type { PollId, PollOptionId } from "@/entities/poll";
import { useToast } from "@/shared/lib/composables/useToast";

/**
 * Poll voting actions. Wraps the entity store's vote / unvote with the
 * user-facing toast on failure, so the poll display stays presentational.
 */
export function usePollVote() {
  const { vote, unvote } = usePollsStore();
  const toast = useToast();

  async function voteForOption(pollId: PollId, optionId: PollOptionId) {
    const { error } = await vote(pollId, optionId);
    if (error) toast.error("Не удалось проголосовать");
  }

  async function cancelVote(pollId: PollId) {
    await unvote(pollId);
  }

  return { voteForOption, cancelVote };
}

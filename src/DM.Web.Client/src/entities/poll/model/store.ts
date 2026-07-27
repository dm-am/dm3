import { defineStore } from "pinia";
import { ref } from "vue";
import type { Poll, PollId, PollOptionId, PollsSearchParams } from "./types";
import type { ListEnvelope } from "@/shared/api/models/common";
import type { Patch, Post } from "@/shared/api/models";
import pollApi from "../api/pollApi";
import { useApiList } from "@/shared/lib/composables/useApiResource";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";

export const usePollsStore = defineStore("polls", () => {
  // Sidebar active polls with caching (60s TTL)
  const active = useApiList<Poll>(() => pollApi.getActivePolls(), {
    cacheMs: 60_000,
  });

  // Paginated polls list (no caching - always fresh for polls page)
  const polls = ref<ListEnvelope<Poll> | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  // Request guard to discard stale out-of-order responses
  const requestGuard = createRequestGuard();

  async function fetchPolls(params: PollsSearchParams) {
    const requestId = requestGuard.next();

    // Reset error up front so a stale error never survives a later,
    // still-in-flight request winning the race.
    error.value = null;
    loading.value = true;

    const { data, error: apiError } = await pollApi.getPolls(params);

    // Ignore stale responses
    if (!requestGuard.isCurrent(requestId)) {
      return;
    }

    polls.value = data ?? null;
    if (apiError) error.value = "Не удалось загрузить опросы";
    loading.value = false;
  }

  function updatePoll(poll: Poll) {
    // Update in active polls cache
    const matchingActivePoll = active.data.value?.find((p) => p.id === poll.id);
    if (matchingActivePoll) Object.assign(matchingActivePoll, poll);

    // Update in paginated polls
    const matchingPoll = polls.value?.resources.find((p) => p.id === poll.id);
    if (matchingPoll) Object.assign(matchingPoll, poll);
  }

  async function createPoll(poll: Post<Poll>) {
    const { data, error } = await pollApi.postPoll(poll);
    if (data && polls.value) {
      polls.value.resources.unshift(data);
    }
    return { data, error };
  }

  async function editPoll(pollId: PollId, poll: Patch<Poll>) {
    const { data, error } = await pollApi.patchPoll(pollId, poll);
    if (data) updatePoll(data);
    return { data, error };
  }

  async function vote(pollId: PollId, optionId: PollOptionId) {
    const { data, error } = await pollApi.postPollVote(pollId, optionId);
    if (data) updatePoll(data);
    return { error };
  }

  async function unvote(pollId: PollId) {
    const { data } = await pollApi.deletePollVote(pollId);
    if (data) updatePoll(data);
  }

  return {
    // Active polls (sidebar)
    activePolls: active.data,
    activePollsLoading: active.loading,
    activePollsError: active.error,
    fetchActivePolls: active.fetch,

    // Paginated polls (polls page)
    polls,
    pollsLoading: loading,
    pollsError: error,
    fetchPolls,

    // Mutations
    createPoll,
    editPoll,
    vote,
    unvote,
  };
});

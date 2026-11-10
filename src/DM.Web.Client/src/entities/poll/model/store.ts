import { defineStore } from "pinia";
import { ref } from "vue";
import type {
  CreatePollRequest,
  Poll,
  PollId,
  PollOptionId,
  PollsSearchParams,
  UpdatePollRequest,
} from "./types";
import type { ListEnvelope } from "@/shared/api/models/common";
import pollApi from "../api/pollApi";
import { unwrapResource } from "@/shared/api";
import { useApiList } from "@/shared/lib/composables/useApiResource";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";

export const usePollsStore = defineStore("polls", () => {
  const active = useApiList<Poll>(() => pollApi.getActivePolls(), {
    cacheMs: 60_000,
  });

  // Not cached: the polls page always reads fresh.
  const polls = ref<ListEnvelope<Poll> | null>(null);
  const loading = ref(false);
  const error = ref<string | null>(null);

  const requestGuard = createRequestGuard();

  async function fetchPolls(params: PollsSearchParams) {
    const requestId = requestGuard.next();

    error.value = null;
    loading.value = true;

    const { data, error: apiError } = await pollApi.getPolls(params);

    if (!requestGuard.isCurrent(requestId)) return;

    polls.value = data ?? null;
    if (apiError) error.value = "Не удалось загрузить опросы";
    loading.value = false;
  }

  function updatePoll(poll: Poll) {
    const matchingActivePoll = active.data.value?.find((p) => p.id === poll.id);
    if (matchingActivePoll) Object.assign(matchingActivePoll, poll);

    const matchingPoll = polls.value?.resources.find((p) => p.id === poll.id);
    if (matchingPoll) Object.assign(matchingPoll, poll);
  }

  async function createPoll(poll: CreatePollRequest) {
    const { data, error } = await pollApi.postPoll(poll);
    const created = unwrapResource<Poll>(data);
    if (created && polls.value) polls.value.resources.unshift(created);
    return { data: created, error };
  }

  async function editPoll(pollId: PollId, poll: UpdatePollRequest) {
    const { data, error } = await pollApi.patchPoll(pollId, poll);
    const updated = unwrapResource<Poll>(data);
    if (updated) updatePoll(updated);
    return { data: updated, error };
  }

  async function vote(pollId: PollId, optionId: PollOptionId) {
    const { data, error } = await pollApi.postPollVote(pollId, optionId);
    const voted = unwrapResource<Poll>(data);
    if (voted) updatePoll(voted);
    return { error };
  }

  async function unvote(pollId: PollId) {
    const { data, error } = await pollApi.deletePollVote(pollId);
    const unvoted = unwrapResource<Poll>(data);
    if (unvoted) updatePoll(unvoted);
    return { error };
  }

  return {
    activePolls: active.data,
    activePollsLoading: active.loading,
    activePollsError: active.error,
    fetchActivePolls: active.fetch,

    polls,
    pollsLoading: loading,
    pollsError: error,
    fetchPolls,

    createPoll,
    editPoll,
    vote,
    unvote,
  };
});

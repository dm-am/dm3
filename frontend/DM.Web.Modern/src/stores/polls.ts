import { defineStore } from "pinia";
import { ref } from "vue";
import type { Poll, PollId, PollOptionId } from "@/api/models/community";
import type { ListEnvelope } from "@/api/models/common";
import type { Patch, Post } from "@/api/models";
import communityApi from "@/api/requests/communityApi";

export const usePollsStore = defineStore("polls", () => {
  const activePolls = ref<Poll[] | null>(null);
  async function fetchActivePolls() {
    const { data } = await communityApi.getPolls({ size: 3, skip: 0 }, true);
    activePolls.value = data?.resources ?? null;
  }
  const polls = ref<ListEnvelope<Poll> | null>(null);
  async function fetchPolls(number: number, onlyActive: boolean) {
    const { data } = await communityApi.getPolls({ number }, onlyActive);
    polls.value = data ?? null;
  }

  function updatePoll(poll: Poll) {
    const matchingActivePoll = activePolls.value?.find((p) => p.id === poll.id);
    if (matchingActivePoll) Object.assign(matchingActivePoll, poll);

    const matchingPoll = polls.value?.resources.find((p) => p.id === poll.id);
    if (matchingPoll) Object.assign(matchingPoll, poll);
  }

  async function createPoll(poll: Post<Poll>) {
    const { data, error } = await communityApi.postPoll(poll);
    if (data && polls.value) {
      polls.value.resources.unshift(data.resource);
    }
    return { data, error };
  }

  async function editPoll(pollId: PollId, poll: Patch<Poll>) {
    const { data, error } = await communityApi.patchPoll(pollId, poll);
    if (data) updatePoll(data.resource);
    return { data, error };
  }

  async function vote(pollId: PollId, optionId: PollOptionId) {
    const { data } = await communityApi.postPollVote(pollId, optionId);
    if (data) updatePoll(data.resource);
  }

  async function unvote(pollId: PollId) {
    const { data } = await communityApi.deletePollVote(pollId);
    if (data) updatePoll(data.resource);
  }

  return { fetchActivePolls, activePolls, fetchPolls, polls, createPoll, editPoll, vote, unvote };
});

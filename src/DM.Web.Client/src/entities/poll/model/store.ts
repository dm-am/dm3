import { defineStore } from "pinia";
import { ref } from "vue";
import type { Poll, PollId, PollOptionId } from "./types";
import type { ListEnvelope } from "@/shared/api/models/common";
import type { Patch, Post } from "@/shared/api/models";
import pollApi from "../api/pollApi";

export const usePollsStore = defineStore("polls", () => {
  const activePolls = ref<Poll[] | null>(null);
  async function fetchActivePolls() {
    const { data } = await pollApi.getPolls({ size: 3, skip: 0 }, true);
    activePolls.value = data?.resources ?? null;
  }
  const polls = ref<ListEnvelope<Poll> | null>(null);
  async function fetchPolls(number: number, onlyActive: boolean) {
    const { data } = await pollApi.getPolls({ number }, onlyActive);
    polls.value = data ?? null;
  }

  function updatePoll(poll: Poll) {
    const matchingActivePoll = activePolls.value?.find((p) => p.id === poll.id);
    if (matchingActivePoll) Object.assign(matchingActivePoll, poll);

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
    const { data } = await pollApi.postPollVote(pollId, optionId);
    if (data) updatePoll(data);
  }

  async function unvote(pollId: PollId) {
    const { data } = await pollApi.deletePollVote(pollId);
    if (data) updatePoll(data);
  }

  return { fetchActivePolls, activePolls, fetchPolls, polls, createPoll, editPoll, vote, unvote };
});

<script setup lang="ts">
import { watch } from "vue";
import { useRoute } from "vue-router";
import { usePollsStore } from "@/entities/poll";
import { PollsFilter, usePollsFilter } from "@/features/poll-filter";
import { CreatePollForm } from "@/features/create-poll";
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import PollsList from "./PollsList.vue";

const route = useRoute();
const { fetchPolls } = usePollsStore();
const { searchParams } = usePollsFilter();

watch(
  () => [searchParams.value, route.query.number],
  () => {
    fetchPolls(searchParams.value);
  },
  { immediate: true },
);
</script>

<template>
  <page-title>Опросы</page-title>
  <LeadText>Запланированные, текущие и завершенные опросы сообщества</LeadText>

  <!-- Draws itself for moderators only -->
  <CreatePollForm />

  <PollsFilter />

  <PollsList />
</template>

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

// Fetch on initial load and when filters/page change
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

  <!-- Create poll form (moderators only) -->
  <CreatePollForm />

  <!-- Filter -->
  <PollsFilter />

  <!-- List + paging -->
  <PollsList />
</template>

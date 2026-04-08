<script setup lang="ts">
import { watch } from "vue";
import { useRoute } from "vue-router";
import { usePollsStore } from "@/entities/poll";
import { usePollsFilter } from "@/features/poll-filter";
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
  { immediate: true }
);
</script>

<template>
  <PollsList />
</template>

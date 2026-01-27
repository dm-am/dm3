<script setup lang="ts">
import { useRoute } from "vue-router";
import { usePollsStore } from "@/stores/polls";
import { extractNumberParam } from "@/router";
import { useFetchData } from "@/composables/useFetchData";

const route = useRoute();
const { fetchPolls } = usePollsStore();

useFetchData(
  () => fetchPolls(extractNumberParam(route.params.n), false),
  [
    {
      param: (p) => p.n,
      callback: (n) => fetchPolls(extractNumberParam(n), false),
    },
  ],
);
</script>

<template>
  <router-view />
</template>

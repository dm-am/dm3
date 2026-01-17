<script setup lang="ts">
import { useRoute } from "vue-router";
import { useWebsiteReviewStore } from "@/stores";
import { extractNumberParam } from "@/router";
import { useFetchData } from "@/composables/useFetchData";

const route = useRoute();
const { fetchWebsiteReviews } = useWebsiteReviewStore();

useFetchData(
  () => fetchWebsiteReviews(extractNumberParam(route.params.n)),
  [
    {
      param: (p) => p.n,
      callback: (n) => fetchWebsiteReviews(extractNumberParam(n)),
    },
  ],
);
</script>

<template>
  <page-title>Наши пользователи о нас</page-title>
  <router-view />
</template>

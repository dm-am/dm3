<script setup lang="ts">
/**
 * Dynamic error page for /error/:code routes and the catch-all 404.
 * Resolves the HTTP code from route params (falls back to 404 for
 * the catch-all route where :code is absent).
 */
import { computed } from "vue";
import { useRoute } from "vue-router";
import { extractNumberParam } from "@/app/providers/router";
import { ErrorPage } from "@/shared/ui/ErrorPage";

const route = useRoute();
const code = computed(() => extractNumberParam(route.params.code, 404));
</script>

<template>
  <ErrorPage :code="code" />
</template>

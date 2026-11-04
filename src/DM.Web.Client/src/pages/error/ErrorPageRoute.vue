<script setup lang="ts">
/** /error/:code and the catch-all: a numeric code is the status, anything else is 404. */
import { computed } from "vue";
import { useRoute } from "vue-router";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import { ErrorPage, getErrorConfig } from "@/shared/ui/ErrorPage";

const route = useRoute();

// Raw code from the path param first, then the ?code= query fallback.
const rawCode = computed(() => {
  const fromParam = Array.isArray(route.params.code)
    ? route.params.code[0]
    : route.params.code;
  const fromQuery = Array.isArray(route.query.code)
    ? route.query.code[0]
    : route.query.code;
  return String(fromParam || fromQuery || "");
});

const code = computed(() => {
  const numeric = Number(rawCode.value);
  return Number.isFinite(numeric) && numeric > 0 ? numeric : 404;
});

useDocumentTitle(() => getErrorConfig(code.value).title);
</script>

<template>
  <ErrorPage :code="code" />
</template>

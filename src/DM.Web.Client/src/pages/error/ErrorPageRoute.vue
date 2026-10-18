<script setup lang="ts">
/**
 * Dynamic error page for /error/:code routes and the catch-all 404.
 *
 * The code segment resolves in two ways:
 *  - a numeric HTTP status (/error/404, /error/403…);
 *  - absent, unreadable or not a number (the catch-all route) -> 404.
 *
 * Non-numeric codes used to resolve through a table of symbolic ones borrowed
 * from an external sign-in the server never grew. Nothing redirected here with
 * one, so the three entries only gave three addresses a page of their own that
 * no link led to.
 */
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

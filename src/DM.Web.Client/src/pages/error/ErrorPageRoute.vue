<script setup lang="ts">
/**
 * Dynamic error page for /error/:code routes and the catch-all 404.
 *
 * The code segment resolves in three ways (doc 4.2.3.1.6):
 *  - a numeric HTTP status (/error/404, /error/403…);
 *  - a symbolic OAuth-style code (/error/access_denied or /error?code=…),
 *    which borrows a matching HTTP illustration and carries its own message;
 *  - absent (the catch-all route) → 404.
 */
import { computed } from "vue";
import { useRoute } from "vue-router";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import {
  ErrorPage,
  getErrorConfig,
  getSymbolicError,
} from "@/shared/ui/ErrorPage";

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

const symbolic = computed(() => getSymbolicError(rawCode.value));

const code = computed(() => {
  if (symbolic.value) return symbolic.value.code;
  const numeric = Number(rawCode.value);
  return Number.isFinite(numeric) && numeric > 0 ? numeric : 404;
});

useDocumentTitle(() =>
  symbolic.value ? symbolic.value.title : getErrorConfig(code.value).title,
);
</script>

<template>
  <ErrorPage
    :code="code"
    :title="symbolic?.title"
    :description="symbolic?.description"
  />
</template>

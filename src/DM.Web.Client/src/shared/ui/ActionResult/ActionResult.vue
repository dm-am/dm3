<script setup lang="ts">
import { computed } from "vue";
import type { ActionResult } from "@/shared/lib/composables/useActionResult";

const props = withDefaults(
  defineProps<{
    result?: ActionResult;
    /** The line itself, for a place that keeps its own refusal. */
    text?: string | null;
  }>(),
  { result: undefined, text: null },
);

const line = computed(() => props.result?.refusal.value || props.text || "");
</script>

<template>
  <p v-if="line" class="action-result" role="alert">
    {{ line }}
  </p>
</template>

<style scoped lang="sass">
// A custom property, not the caller's margin: two rules of one weight would hang on stylesheet order.
.action-result
  margin: var(--action-result-margin, #{$small} 0 0)
  color: $accent-red
  font-size: $secondary-font-size
</style>

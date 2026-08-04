<script setup lang="ts">
/**
 * Bold "- <link>" forward link used at the bottom of sidebar list blocks
 * (e.g. "Все игры с набором", "Все блоги"). Extracted from the six
 * sidebar blocks that hand-rolled the same "- " prefix + bold link markup.
 */
import type { RouteLocationRaw } from "vue-router";

defineProps<{
  to: RouteLocationRaw;
}>();
</script>

<template>
  <li class="forward-link">
    <span class="muted" aria-hidden="true">- </span>
    <router-link class="forward" :to="to"><slot /></router-link>
  </li>
</template>

<style scoped lang="sass">
.muted
  color: $text-muted

.forward
  font-weight: bold
</style>

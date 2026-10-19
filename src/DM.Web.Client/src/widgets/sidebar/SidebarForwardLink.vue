<script setup lang="ts">
/** The bold "- <link>" at the bottom of a sidebar list. */
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
.forward
  font-weight: bold
</style>

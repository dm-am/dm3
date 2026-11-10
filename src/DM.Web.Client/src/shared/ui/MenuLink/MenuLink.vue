<script setup lang="ts">
// The content of a list item, not the item: the caller keeps its <li>. The mark is the row's alone:
// a router-link would set aria-current="page" by itself on an exact match.
import type { RouteLocationRaw } from "vue-router";

defineProps<{
  to: RouteLocationRaw;
  /**
   * Set on the row where the reader is, with the word for a screen reader
   * (see above). Left out on every other row.
   */
  current?: "page" | "true";
}>();
</script>

<template>
  <span class="menu-link"
    ><span class="dash" aria-hidden="true">- </span
    ><router-link
      :to="to"
      :class="{ current: !!current }"
      :aria-current="current"
      ><slot /></router-link
    ><slot name="counter"
  /></span>
</template>

<style scoped lang="sass">
.dash
  color: $text-muted

// The color stays the link's: the weight alone says where the reader is, as
// the open tab of the profile does.
.current
  font-weight: bold
</style>

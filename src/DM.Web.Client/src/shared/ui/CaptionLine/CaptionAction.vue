<script setup lang="ts">
/**
 * An action of a caption line ("Редактировать", "Удалить", "Снять бан"): a
 * word of the line and not a control drawn over it. It takes the line's font,
 * size and colour, and answers the pointer the way a grey link of the site
 * does: $link-hover and the underline (owner's standard, 27 September 2026;
 * it used to take the underline alone, while the same action as a link turned
 * blue). It never turns red, a destructive action included - the confirmation
 * it opens is where the danger is spelled out.
 *
 * A button, or a link when `to` is given (the edit page of a publication).
 * Listeners and attributes (`disabled`, `aria-*`) fall through to either.
 */
import type { RouteLocationRaw } from "vue-router";

defineProps<{
  /** Navigates instead of acting: the action renders as a router link. */
  to?: RouteLocationRaw;
}>();
</script>

<template>
  <router-link v-if="to" :to="to" class="caption-action"><slot /></router-link>
  <button v-else type="button" class="caption-action"><slot /></button>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

// The link's behaviour in the colour of the line: the button reset, the eased
// $link-hover and underline, the inline ring and the site's disabled opacity
// all come from +inline-link-button, whose `font: inherit` takes the line's
// leading as well.
.caption-action
  margin: 0
  +inline-link-button(inherit)
</style>

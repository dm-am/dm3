<script setup lang="ts">
/**
 * CounterPair — the shared "(A/B)" counter widget used everywhere a pair of
 * related numbers is shown next to a title or field: games-table title cell,
 * blogs-table title cell, sidebar GameLink, sidebar BlogLink (both values as
 * links), and plain non-link counters like a textarea's remaining-chars
 * indicator (firstTo/secondTo omitted). One place owns the bracket/slash
 * markup and coloring so call sites can never drift apart again.
 *
 * Visual contract (matches HEAD):
 *   - Muted brackets "(" / ")" and muted "/" separator
 *   - Values render as links when firstTo/secondTo are given (each with its
 *     own destination + aria-label), otherwise as plain text
 *   - No tooltips (approved change — tooltips removed from these counters)
 */
import type { RouteLocationRaw } from "vue-router";

defineProps<{
  /** Left value (e.g. unread posts / publications) */
  firstValue: number | string;
  /** Route for the left value link; omit to render plain (non-link) text */
  firstTo?: RouteLocationRaw;
  /** aria-label for the left value link (used only when firstTo is set) */
  firstLabel?: string;
  /** Right value (e.g. unread comments) */
  secondValue: number | string;
  /** Route for the right value link; omit to render plain (non-link) text */
  secondTo?: RouteLocationRaw;
  /** aria-label for the right value link (used only when secondTo is set) */
  secondLabel?: string;
}>();
</script>

<template>
  <span class="counter-pair"
    ><span class="bracket">(</span
    ><router-link v-if="firstTo" :to="firstTo" :aria-label="firstLabel">{{
      firstValue
    }}</router-link
    ><span v-else>{{ firstValue }}</span
    ><span class="counter-sep">/</span
    ><router-link v-if="secondTo" :to="secondTo" :aria-label="secondLabel">{{
      secondValue
    }}</router-link
    ><span v-else>{{ secondValue }}</span
    ><span class="bracket">)</span></span
  >
</template>

<style scoped lang="sass">
.counter-pair
  white-space: nowrap
  a
    color: $link
    &:hover
      color: $link-hover

.bracket,
.counter-sep
  color: $text-muted
</style>

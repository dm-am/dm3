<script setup lang="ts">
/**
 * MenuLink — one row of a menu of links: "- " and the link, a counter after
 * it when the caller has one. The menu of moderation in the left sidebar is
 * built of it, and so is any other list that has to read as one of it.
 *
 * The row is the content of a list item, not the item: the caller keeps its
 * own <li>, so the template of a menu shows what its <ul> holds, and the
 * sidebar's check of that nesting reads it there. It copies as one line of
 * text, "- Общий (2)": the dash, the name and
 * the counter are text in a row with nothing but the dash's space between
 * them, and the counter slot is the caller's to fill with its own spacing.
 *
 * The row the reader is at is bold, in the color of a link, and says so to a
 * screen reader. Which word it says is the caller's: "page" when the row leads
 * to the very page that is open, "true" when the page that is open lies inside
 * the place the row leads to (a topic of the board the row names). The mark is
 * the row's alone: a router-link would set aria-current="page" by itself on an
 * exact match, and a row that is not marked is not to say it either way.
 */
import type { RouteLocationRaw } from "vue-router";
import { Tooltip } from "@/shared/ui/Tooltip";

defineProps<{
  to: RouteLocationRaw;
  /**
   * Set on the row where the reader is, with the word for a screen reader
   * (see above). Left out on every other row.
   */
  current?: "page" | "true";
  /** A tooltip on the link, such as the description of a board. */
  hint?: string;
}>();
</script>

<template>
  <span class="menu-link"
    ><span class="dash" aria-hidden="true">- </span
    ><Tooltip v-if="hint" :text="hint"
      ><router-link
        :to="to"
        :class="{ current: !!current }"
        :aria-current="current"
        ><slot /></router-link></Tooltip
    ><router-link
      v-else
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

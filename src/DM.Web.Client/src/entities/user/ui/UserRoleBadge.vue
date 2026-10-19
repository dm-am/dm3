<script setup lang="ts">
// Each place has drawn the badge in a markup of its own, and each keeps it.
import type { RoleBadge } from "@/shared/config/roles";
import { Tooltip } from "@/shared/ui/Tooltip";

defineProps<{
  badge: RoleBadge;
  /**
   * "link" after the name of a UserLink: grey brackets of their own, the
   * letter under its role's class, the badge unbroken after its space and its
   * tooltip reached by Tab. "caption" in a caption line: the brackets are the
   * line's text around the tooltip, after a space. "column" in the author
   * column of a comment: the brackets inside the letter, under the tooltip and
   * in its colour; the space before the badge is the column's own.
   */
  variant: "link" | "caption" | "column";
}>();
</script>

<template>
  <span v-if="variant === 'link'" class="role-badge"
    >{{ " " }}<span class="bracket">[</span
    ><Tooltip :text="badge.label" focusable
      ><span class="letter" :class="badge.cssClass">{{
        badge.letter
      }}</span></Tooltip
    ><span class="bracket">]</span></span
  >
  <template v-else-if="variant === 'caption'">
    [<Tooltip :text="badge.label"
      ><b class="role-letter">{{ badge.letter }}</b></Tooltip
    >]</template
  >
  <Tooltip v-else :text="badge.label"
    ><b class="role-letter">[{{ badge.letter }}]</b></Tooltip
  >
</template>

<style scoped lang="sass">
.role-badge
  white-space: nowrap

  .bracket
    color: $text-muted

// One letter in every markup: bold and green, the cursor asking for the tooltip.
.role-badge .letter,
.role-letter
  font-weight: bold
  color: $accent-green
  cursor: help
</style>

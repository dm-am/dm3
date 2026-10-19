<script setup lang="ts">
/**
 * The cross at the end of a row of a form that takes the row off. A link's
 * colour, not red, as RemoveButton; the words of the action are its name.
 */
import { symbols } from "@/shared/lib/utils/icons";

defineProps<{
  /** What the press does, said in words ("Удалить вариант"). */
  label: string;
}>();
</script>

<template>
  <button type="button" class="remove-cross" :aria-label="label">
    {{ symbols.close }}
  </button>
</template>

<style scoped lang="sass">
.remove-cross
  flex-shrink: 0
  padding: 0 $small
  background: none
  border: none
  color: $text-muted
  cursor: pointer
  // The sign is a thin glyph: at the size of a title it reads beside a field of text.
  font-size: $title-font-size

  &:hover
    color: $text
</style>

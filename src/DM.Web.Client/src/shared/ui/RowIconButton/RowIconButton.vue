<script setup lang="ts">
/**
 * RowIconButton — an action of a table row drawn as a sign: the pencil that
 * edits the row, the bin that removes it. The editor of a character sheet
 * set the look (AttributeSchemaEditor), and every table whose rows are added
 * and removed in place uses the same two signs from here.
 *
 * A sign alone tells a screen reader nothing, so the words of the action are
 * its accessible name. Removing is drawn as editing is: where a removal asks
 * first, the question carries the weight of the act, and no control of a
 * page is red.
 */
import { SvgIcon } from "@/shared/ui/Icon";
import type { IconName } from "@/shared/lib/utils/icons";

defineProps<{
  /** The sign: "pencil" for editing a row, "trash" for removing it. */
  icon: IconName;
  /** What the press does, said in words ("Удалить атрибут"). */
  label: string;
}>();
</script>

<template>
  <button type="button" class="icon-btn" :aria-label="label">
    <SvgIcon :name="icon" />
  </button>
</template>

<style scoped lang="sass">
.icon-btn
  padding: $tiny $small
  background: none
  border: none
  color: $text-muted
  cursor: pointer
  font-size: 1.1em

  &:hover:not(:disabled)
    color: $text

  &:disabled
    opacity: $disabled-opacity
    cursor: default
</style>

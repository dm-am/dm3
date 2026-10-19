<script setup lang="ts">
// The words of the action are the sign's accessible name. No control of a page is red.
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

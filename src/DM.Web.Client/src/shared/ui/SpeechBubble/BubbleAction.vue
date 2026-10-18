<template>
  <Tooltip :text="label"
    ><button
      type="button"
      class="bubble-action"
      :aria-label="label"
      @click="emit('click')"
    >
      <SvgIcon :name="icon" /></button
  ></Tooltip>
</template>

<script setup lang="ts">
/**
 * One icon control for a speech bubble's `actions` slot: the pencil or the
 * trash of the chat message toolbar, the same glyph and the same answer to
 * the pointer, without the toolbar's plate, because the bubble is the plate.
 *
 * The button carries no text, so the label is its accessible name and the
 * tooltip both. Who is shown the control is the caller's decision: the bubble
 * only gives it a corner.
 */
import SvgIcon from "@/shared/ui/Icon/SvgIcon.vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import type { IconName } from "@/shared/lib/utils/icons";

defineProps<{
  /** Glyph from icons.ts: "pencil" to edit, "trash" to delete. */
  icon: IconName;
  /** What the control does, read by a screen reader and shown on hover. */
  label: string;
}>();

const emit = defineEmits<{ click: [] }>();
</script>

<style scoped lang="sass">
@use "@/assets/styles/MessageToolbar" as *

// Muted over the green like the toolbar's icons over their plate. The glyph
// is 1em, so the font size is its size; the box around it is the pointer's
// target, and the focus ring follows its corners.
.bubble-action
  display: flex
  align-items: center
  justify-content: center
  width: $bubble-action-size
  height: $bubble-action-size
  padding: 0
  border: none
  border-radius: $border-radius
  background: transparent
  color: $text-muted
  font-size: $font-size
  cursor: pointer
  +toolbar-icon-motion
</style>

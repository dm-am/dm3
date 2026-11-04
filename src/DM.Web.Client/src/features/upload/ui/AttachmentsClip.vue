<script setup lang="ts">
// One root, so the host's scoped rule that positions it reaches it.
import { computed } from "vue";
import { SvgIcon } from "@/shared/ui/Icon";

const props = defineProps<{
  count: number;
  open: boolean;
  controls: string;
}>();

const emit = defineEmits<{ toggle: [] }>();

const label = computed(() =>
  props.count > 1 ? `Вложения: ${props.count}` : "Вложение",
);
</script>

<template>
  <button
    type="button"
    class="attachments-toggle"
    :class="{ counted: count > 1 }"
    :aria-expanded="open"
    :aria-controls="controls"
    :aria-label="label"
    @click="emit('toggle')"
  >
    <SvgIcon name="attachment" /><span
      v-if="count > 1"
      class="attachments-count"
      >{{ count }}</span
    >
  </button>
</template>

<style scoped lang="sass">
// A count is text, whose ink starts a hair inside its box.
.attachments-toggle
  display: flex
  align-items: baseline
  gap: $tiny
  margin: 0
  padding: 0
  border: none
  background: none
  font-family: inherit
  font-size: $tertiary-font-size
  // No taller than the glyph is wide: the glyph alone then sets the top the host positions.
  line-height: $secondary-font-size
  color: $text-muted
  cursor: pointer

  // The glyph keeps its size; the pointer target grows around it.
  &::after
    content: ""
    position: absolute
    top: 50%
    left: 50%
    width: max(100%, #{$tap-target-min})
    height: max(100%, #{$tap-target-min})
    transform: translate(-50%, -50%)

  :deep(.svg-icon)
    display: block
    width: $secondary-font-size
    height: auto

  &:hover .attachments-count
    text-decoration: underline
</style>

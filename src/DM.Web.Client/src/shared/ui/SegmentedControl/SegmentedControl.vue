<script setup lang="ts" generic="V extends string">
// Pass the group name as `ariaLabel`, not `aria-label`: the kebab spelling type-checks as the native
// attribute, dropping the required-prop check and the inference of V at the call site.
export interface SegmentedOption<V extends string = string> {
  value: V;
  label: string;
}

defineProps<{
  /** Selected option value (v-model). */
  modelValue: V;
  options: readonly SegmentedOption<V>[];
  /** aria-label for the group. */
  ariaLabel: string;
}>();

const emit = defineEmits<{
  "update:modelValue": [value: V];
}>();
</script>

<template>
  <div class="segmented-control" role="group" :aria-label="ariaLabel">
    <template v-for="(opt, index) in options" :key="opt.value">
      <span v-if="index > 0" class="copy-space">{{ " " }}</span>
      <button
        type="button"
        class="segment"
        :class="{ active: modelValue === opt.value }"
        :aria-pressed="modelValue === opt.value"
        @click="emit('update:modelValue', opt.value)"
      >
        {{ opt.label }}
      </button>
    </template>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Buttons" as *

// Inline-block, not flex, so a selection copies in one line.
.segmented-control
  display: inline-block
  white-space: nowrap
  vertical-align: middle
  height: $control-height
  box-sizing: border-box
  border: 1px solid $border
  border-radius: $border-radius
  overflow: hidden

.segment
  display: inline-block
  height: 100%
  vertical-align: top
  padding: 0 $medium
  background-color: $bg-element
  border: none
  font: inherit
  font-size: $secondary-font-size
  color: $text-muted
  cursor: pointer
  transition: color $transition-fast, background-color $transition-fast

  &:hover:not(:disabled)
    color: $text

  &.active
    background-color: $bg-element-accent
    color: $text
    font-weight: 700

  // A zero-width .copy-space sits between adjacent buttons.
  & ~ &
    border-left: 1px solid $border

  // Inward: the pill's overflow: hidden would clip a ring on the segment's edge.
  &:focus-visible
    +focus-ring($inward: true)

  +disabled-hatch
</style>

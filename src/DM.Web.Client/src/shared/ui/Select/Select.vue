<script setup lang="ts">
/**
 * Select — styled native <select> replacement.
 *
 * Uses the shared control chrome (+input()) instead of the browser default
 * so it matches text inputs/textareas site-wide; the native <select> stays
 * underneath (appearance: none) for full keyboard/a11y/mobile behavior,
 * with a chevron icon overlaid on top.
 */
import { SvgIcon } from "@/shared/ui/Icon";

export interface SelectOption {
  value: string;
  label: string;
  disabled?: boolean;
}

withDefaults(
  defineProps<{
    modelValue: string;
    options: SelectOption[];
    placeholder?: string;
    disabled?: boolean;
  }>(),
  {
    placeholder: undefined,
    disabled: false,
  },
);

defineEmits<{
  "update:modelValue": [value: string];
}>();
</script>

<template>
  <div class="select-control" :class="{ disabled }">
    <select
      class="select-input"
      :value="modelValue"
      :disabled="disabled"
      @change="
        $emit('update:modelValue', ($event.target as HTMLSelectElement).value)
      "
    >
      <option v-if="placeholder" value="" disabled>{{ placeholder }}</option>
      <option
        v-for="option in options"
        :key="option.value"
        :value="option.value"
        :disabled="option.disabled"
      >
        {{ option.label }}
      </option>
    </select>
    <SvgIcon name="chevronDown" class="select-chevron" />
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.select-control
  position: relative
  display: block
  width: 100%

  &.disabled
    opacity: $disabled-opacity

.select-input
  width: 100%
  appearance: none
  cursor: pointer
  +input()

  // Reserves the chevron's box. Overrides the padding shorthand from
  // +input(), so it must come after the mixin; the bare & block keeps it a
  // later same-specificity rule under CSS ordering semantics.
  &
    padding-right: $control-height

  &:disabled
    cursor: default

.select-chevron
  position: absolute
  right: $small
  top: 50%
  transform: translateY(-50%)
  width: 14px
  height: 14px
  color: $text-muted
  pointer-events: none
</style>

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

/**
 * A named set of options, rendered as an <optgroup>. Grouping is what keeps a
 * list readable when one control has to offer two different intents at once —
 * support and complaints on the ticket form.
 */
export interface SelectOptionGroup {
  label: string;
  options: SelectOption[];
}

withDefaults(
  defineProps<{
    modelValue: string;
    options: (SelectOption | SelectOptionGroup)[];
    placeholder?: string;
    disabled?: boolean;
    /**
     * Id for the native <select> underneath. An `id` attribute on this
     * component would land on the wrapper <div>, and a caller's <label for>
     * would then point at something no browser focuses.
     */
    id?: string;
    /**
     * Accessible name, for the places with no visible caption to point a
     * <label> at. Declared for the same reason as `id`: left to attribute
     * fallthrough it would name the wrapper <div> instead of the control.
     */
    ariaLabel?: string;
  }>(),
  {
    placeholder: undefined,
    disabled: false,
    id: undefined,
    ariaLabel: undefined,
  },
);

const isGroup = (
  entry: SelectOption | SelectOptionGroup,
): entry is SelectOptionGroup => "options" in entry;

defineEmits<{
  "update:modelValue": [value: string];
}>();
</script>

<template>
  <div class="select-control" :class="{ disabled }">
    <select
      :id="id"
      class="select-input"
      :value="modelValue"
      :disabled="disabled"
      :aria-label="ariaLabel"
      @change="
        $emit('update:modelValue', ($event.target as HTMLSelectElement).value)
      "
    >
      <option v-if="placeholder" value="" disabled>{{ placeholder }}</option>
      <template v-for="(entry, index) in options" :key="index">
        <optgroup v-if="isGroup(entry)" :label="entry.label">
          <option
            v-for="option in entry.options"
            :key="option.value"
            :value="option.value"
            :disabled="option.disabled"
          >
            {{ option.label }}
          </option>
        </optgroup>
        <option v-else :value="entry.value" :disabled="entry.disabled">
          {{ entry.label }}
        </option>
      </template>
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

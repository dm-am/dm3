<script setup lang="ts" generic="V extends string">
/**
 * SegmentedControl — the site's canonical mode/period toggle: a bordered
 * $control-height pill of mutually exclusive options, active = accent fill +
 * bold. SSOT for the idiom previously duplicated per page (statistics
 * granularity, profile role toggle, message-search scope/sort).
 *
 * Copy-safe: the pill is an inline-block with inline-block buttons and
 * zero-width .copy-space text nodes between them, so a selection copies as
 * "Месяц Год Все время" on one line (Chrome serializes flex items with
 * newlines — the same fix as Tabs/Paging). The hairline divider therefore
 * uses "& ~ &" (a span sits between adjacent buttons, breaking "& + &").
 *
 * Accessibility: aria-pressed marks the active option; the keyboard focus
 * ring is inset (outline-offset: -2px) because the pill's overflow: hidden
 * would clip the default outside ring. Pass the group name as `ariaLabel`,
 * NOT `aria-label` — the kebab spelling type-checks as a native ARIA
 * attribute instead of this prop, which both drops the required-prop check
 * and defeats the generic inference of V at the call site.
 */
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
// Bordered pill — inline-block (NOT flex) so a selection copies in one line;
// nowrap keeps the segments from ever breaking mid-pill.
.segmented-control
  display: inline-block
  white-space: nowrap
  vertical-align: middle
  height: $control-height
  box-sizing: border-box
  border: 1px solid $border
  border-radius: $button-border-radius
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

  &:hover
    color: $text

  &.active
    background-color: $bg-element-accent
    color: $text
    font-weight: 700

  // "& ~ &" (not "& + &"): a zero-width .copy-space span sits between
  // adjacent buttons, so they are never direct siblings.
  & ~ &
    border-left: 1px solid $border

  // Inset ring — the pill's overflow: hidden clips the default +2px ring.
  &:focus-visible
    outline: 2px solid $border-focus
    outline-offset: -2px
</style>

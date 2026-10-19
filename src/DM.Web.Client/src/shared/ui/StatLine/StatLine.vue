<script setup lang="ts">
// The variant colours the value alone; no italics anywhere, as on DM2.
import { computed } from "vue";
import type { RouteLocationRaw } from "vue-router";

const props = withDefaults(
  defineProps<{
    label: string;
    value?: string | number | null;
    variant?: "default" | "positive" | "negative" | "muted";
    to?: RouteLocationRaw;
    placeholder?: string;
    /** Bold for coloured variants; false for a short status word where the colour says it all. */
    emphasized?: boolean;
  }>(),
  {
    value: undefined,
    variant: "default",
    to: undefined,
    placeholder: "не указано",
    emphasized: true,
  },
);

const displayValue = computed(() => {
  if (props.value === null || props.value === undefined || props.value === "") {
    return null;
  }
  return String(props.value);
});
</script>

<template>
  <!-- Inline flow, not flex, with a real " " between: a selection copies "Label: value" on one line. -->
  <div class="stat-line">
    <span class="label">{{ label }}:</span>{{ " "
    }}<router-link
      v-if="to && displayValue !== null"
      :to="to"
      class="value as-link"
      :class="[variant, { 'value--plain': !emphasized }]"
      >{{ displayValue }}</router-link
    ><span
      v-else
      class="value"
      :class="[variant, { 'value--plain': !emphasized }]"
      >{{ displayValue ?? placeholder }}</span
    >
  </div>
</template>

<style scoped lang="sass">
// line-height 1.25, tighter than body text: stat blocks read like a table. EditableField matches it.
.stat-line
  display: block
  font-size: $font-size
  line-height: 1.25
  color: $text

.value
  color: $text
  overflow-wrap: anywhere

  &.positive
    color: $accent-green
    font-weight: bold

  &.negative
    color: $accent-red
    font-weight: bold

  &.muted
    color: $text-muted
    font-weight: bold

  &.as-link
    color: $link
    text-decoration: none

    &:hover
      color: $link-hover
      text-decoration: underline

    &.positive
      color: $accent-green

      &:hover
        color: $accent-green-hover
        text-decoration: underline

    &.negative
      color: $accent-red

      &:hover
        color: $accent-red-hover
        text-decoration: underline

    &.muted
      color: $text-muted

      &:hover
        color: $link-hover
        text-decoration: underline

// Last in the cascade, to take the bold off the coloured variants.
.value.value--plain
  font-weight: normal
</style>

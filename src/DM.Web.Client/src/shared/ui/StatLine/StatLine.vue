<script setup lang="ts">
/**
 * StatLine — single "Label: value" row. The atomic building block for every
 * label-value line on profile, post meta, sidebars, etc. Keeps the format,
 * spacing, and color contract in ONE place (SSOT).
 *
 * Visual contract (matches DM2):
 *   - Label and value share the same baseline color ($text)
 *   - No italics anywhere — even for empty/placeholder values
 *   - Inline label-value, single line
 *
 * Variants only color the VALUE (semantic — green for positive rating,
 * red for negative, muted gray for "not participating" / empty fallback).
 */
import { computed } from "vue";
import type { RouteLocationRaw } from "vue-router";

const props = withDefaults(
  defineProps<{
    label: string;
    value?: string | number | null;
    variant?: "default" | "positive" | "negative" | "muted";
    to?: RouteLocationRaw;
    placeholder?: string;
    /**
     * Bold weight for colored variants. Default true — for numeric
     * values (rating, endorsements) bold underlines importance.
     * false — for short status words ("онлайн") where the color already says it all.
     */
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

// Variant chooses the value color (positive=green, negative=red). The
// placeholder ("не указано" / "не участвует") stays in default $text color —
// not muted gray — because it's part of the normal content flow, not
// metadata. Only sidebars use gray for de-emphasized text.
const effectiveVariant = computed(() => props.variant);
</script>

<template>
  <!-- Inline text flow (NOT flex) so that:
       1. Browsers copy "Label: value" as a single line, not "Label:\nvalue"
       2. Long values wrap naturally inside the line box without forcing the
          label to a different row.
       The literal " " text node between label and value is preserved as a
       real space character in the DOM — Selection.toString() includes it,
       so copy yields "Label: value" not "Label:value". -->
  <div class="stat-line">
    <span class="label">{{ label }}:</span>{{ " "
    }}<router-link
      v-if="to && displayValue !== null"
      :to="to"
      class="value as-link"
      :class="[effectiveVariant, { 'value--plain': !emphasized }]"
      >{{ displayValue }}</router-link
    ><span
      v-else
      class="value"
      :class="[effectiveVariant, { 'value--plain': !emphasized }]"
      >{{ displayValue ?? placeholder }}</span
    >
  </div>
</template>

<style scoped lang="sass">
// Block-level container with inline children — label and value flow as a
// single text line. They wrap together if the line overflows, never apart.
// line-height 1.25 — intentionally tighter than regular body text: stat blocks
// read like a table, extra air between lines only pulls apart
// related "label: value" pairs. 1.25 still leaves enough room
// for the underline of the as-link variant.
// IMPORTANT: EditableField is synchronized to the same line height — otherwise
// in `.info-grid`, where they stack interleaved ("Имя"/"Местоположение" =
// EditableField, "Пол"/"День рождения" = StatLine), the rhythm jumps.
.stat-line
  display: block
  font-size: $font-size
  line-height: 1.25
  color: $text

.label
  color: $text

.value
  color: $text
  word-break: break-word

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
      font-weight: bold

      &:hover
        color: $accent-green-hover
        text-decoration: underline

    &.negative
      color: $accent-red
      font-weight: bold

      &:hover
        color: $accent-red-hover
        text-decoration: underline

    &.muted
      color: $text-muted
      font-weight: bold

      &:hover
        color: $link-hover
        text-decoration: underline

// emphasized=false at the usage site — keeps the variant color but removes
// bold. For short statuses ("онлайн") where bold is visually noisy.
// Placed at the end of the cascade to override font-weight: bold of
// positive/negative/muted variants.
.value.value--plain
  font-weight: normal
</style>

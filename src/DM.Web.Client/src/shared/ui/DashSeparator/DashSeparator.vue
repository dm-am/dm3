<script setup lang="ts">
// A literal dash line clipped by its container; with a label, two dashed rules around it.
withDefaults(
  defineProps<{
    /** "none" for callers that space the line themselves. */
    spacing?: "small" | "tiny" | "none";
    /** Optional centered label — switches to line-label-line layout */
    label?: string;
    /** Width of the plain dash line, e.g. "75%". Defaults to the full
     * container width. */
    width?: string;
  }>(),
  {
    spacing: "small",
    width: "100%",
  },
);

// Clipped by the container rather than measured: the count outlasts the widest viewport.
const DASH_LINE = "- ".repeat(450);
</script>

<template>
  <div
    v-if="label"
    class="dash-separator-labeled"
    :class="`spacing-${spacing}`"
    aria-hidden="true"
  >
    <div class="dash-label-line"></div>
    <span class="dash-label-text">{{ label }}</span>
    <div class="dash-label-line"></div>
  </div>
  <div
    v-else
    class="dash-separator"
    :class="`spacing-${spacing}`"
    :style="{ '--dash-width': width }"
    aria-hidden="true"
  >
    {{ DASH_LINE }}
  </div>
</template>

<style scoped lang="sass">
.dash-separator
  color: $text-muted
  white-space: nowrap
  overflow: hidden
  max-width: var(--dash-width, 100%)
  width: 0
  min-width: var(--dash-width, 100%)
  // The one non-selectable text: a select-all must not copy the dash line.
  user-select: none

.dash-separator-labeled
  display: flex
  align-items: center
  gap: $small

.dash-separator,
.dash-separator-labeled
  &.spacing-small
    margin: $small 0

  &.spacing-tiny
    margin: $tiny 0

  &.spacing-none
    margin: 0

.dash-label-line
  flex: 1
  height: 0
  border-top: 1px dashed
  border-color: $border

.dash-label-text
  flex-shrink: 0
  font-size: $secondary-font-size
  color: $text-muted
  font-weight: 400
  white-space: nowrap
</style>

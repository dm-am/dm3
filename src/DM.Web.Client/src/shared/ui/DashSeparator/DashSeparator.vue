<script setup lang="ts">
/**
 * Canonical ASCII dash separator ("- - - - ...").
 *
 * A single long literal dash line clipped by the container width via
 * white-space: nowrap + overflow: hidden - matches the copies previously
 * duplicated in pages/home/HomePage.vue (.separator) and
 * pages/about/TestimonialsPage.vue (.separator).
 *
 * When `label` is set, switches to the line-label-line layout used by the
 * chat date-separator (pages/global-chat/GlobalChatPage.vue .date-separator):
 * two 1px dashed border lines flanking a centered muted label, instead of
 * literal dash text.
 */
withDefaults(
  defineProps<{
    /** Vertical margin around the separator */
    spacing?: "small" | "tiny";
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

// Long "- " dash line, clipped by the container via CSS overflow rather than
// measured/generated to fit at runtime. The repeat count comfortably outlasts
// the widest supported viewport so the line always reaches the container edge.
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
  // Decorative horizontal dash line — like the sidebar "- " prefix, it is
  // non-selectable so a page select-all/drag does not pull the "- - - -"
  // line into the copied text.
  user-select: none

  &.spacing-small
    margin: $small 0

  &.spacing-tiny
    margin: $tiny 0

.dash-separator-labeled
  display: flex
  align-items: center
  gap: $small

  &.spacing-small
    margin: $small 0

  &.spacing-tiny
    margin: $tiny 0

.dash-label-line
  flex: 1
  height: 0
  border-top: 1px dashed
  border-color: $border

.dash-label-text
  flex-shrink: 0
  font-size: $secondary-font-size
  color: $text-muted
  font-weight: 500
  white-space: nowrap
</style>

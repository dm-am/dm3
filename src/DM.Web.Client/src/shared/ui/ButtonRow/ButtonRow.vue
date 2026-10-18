<script setup lang="ts">
/**
 * A row of buttons in one line, each at its own width: a phone shows the row
 * the full version shows, and the reader zooms in rather than the row turning
 * into a column or hiding a button into "…". Where the column is narrower than
 * the row (the middle column of a window of 1024 or 1280 under four buttons of
 * an administrator), the buttons that have no room go down a line, as text
 * does, rather than stand over the next column.
 *
 * The row hides nothing, cuts nothing and opens no menu; which buttons stand
 * in it, and in what order, is its owner's. The items are the elements of the
 * default slot. A child wrapping a control (a block with a button and its
 * dialog) is one item, and a panel an item hangs out of the flow counts for
 * nothing.
 */
withDefaults(
  defineProps<{
    /** The step between two buttons. */
    gap?: "small" | "medium";
    /** The element of the row: "span" where it stands inside a line of text. */
    tag?: "div" | "span";
  }>(),
  { gap: "small", tag: "div" },
);
</script>

<template>
  <component :is="tag" class="button-row" :class="`button-row--gap-${gap}`">
    <slot />
  </component>
</template>

<style scoped lang="sass">
// A line of buttons, each at its own width: a button never gives up width to
// its neighbours. What has no room in the line goes down to the next one.
.button-row
  display: flex
  flex-wrap: wrap
  align-items: center
  min-width: 0

  // No item is wider than the row. A line of words in it (the state a press
  // leaves) breaks between its words, and so does the caption of a button
  // that alone is wider than the row ("Отключить двухфакторную
  // аутентификацию" in the middle column of 1024), centred in its height. A
  // button that fits keeps its caption on one line.
  > :deep(*)
    flex-shrink: 0
    max-width: 100%

  :deep(button)
    max-width: 100%
    white-space: normal
    text-align: center

.button-row--gap-small
  gap: $small

.button-row--gap-medium
  gap: $medium
</style>

<script setup lang="ts">
/**
 * ChatStrip: the line at the top of the chat's frame, where the global chat
 * has its events and a correspondence says whom it is with.
 *
 * This is the shell: the surface, the dashed hand-over to the feed below, the
 * secondary size and the muted colour of the line. What the line says is the
 * caller's. Where the frame is narrower than the line, it wraps like any text
 * and the strip grows a line: nothing in it scrolls sideways or is cut.
 */
</script>

<template>
  <div class="chat-strip">
    <slot />
  </div>
</template>

<style scoped lang="sass">
// The dress of the strip, no rounding: information, not a control.
.chat-strip
  flex-shrink: 0
  min-width: 0
  padding: $small $medium
  line-height: 1.4
  font-size: $secondary-font-size
  color: $text-muted
  background-color: $bg-element
  border-bottom: 1px dashed $border
</style>

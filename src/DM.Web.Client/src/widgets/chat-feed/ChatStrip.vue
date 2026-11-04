<script setup lang="ts">
/** The line at the top of a chat's frame; what it says is the caller's. */
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

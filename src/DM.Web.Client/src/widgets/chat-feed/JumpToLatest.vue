<script setup lang="ts">
// "К последним сообщениям": the press falls through to the button as ChatFeed's own listener.
import { SvgIcon } from "@/shared/ui/Icon";
</script>

<template>
  <button
    type="button"
    class="chat-feed__latest"
    aria-label="К последним сообщениям"
  >
    <SvgIcon name="chevronDown" />
  </button>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *

// The idiom of the scroll-nav buttons, with a shadow since it floats over the messages.
.chat-feed__latest
  position: absolute
  bottom: $medium
  left: 50%
  transform: translateX(-50%)
  display: flex
  align-items: center
  justify-content: center
  width: $scroll-nav-width
  height: $scroll-nav-width
  padding: 0
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg-element
  color: $text-muted
  cursor: pointer
  transition: transform $transition-normal
  box-shadow: $shadow-small
  z-index: $z-chat-fab
  svg
    width: 16px
    height: 16px
  &:hover
    background-color: $bg-element-accent
    color: $text
    transform: translateX(-50%) scale(1.05)
</style>

<script setup lang="ts">
/**
 * NoChatSelected: the place of the correspondence while none is open, the
 * frame of a feed with nothing in it and the line that says where to go.
 */
</script>

<template>
  <div class="no-chat">
    <secondary-text class="no-chat__text"
      >Выберите переписку справа или найдите собеседника через
      поиск</secondary-text
    >
  </div>
</template>

<style scoped lang="sass">
// The dashed frame of a feed, about as tall as the feed it stands for, so
// opening a correspondence does not move the page under the reader.
.no-chat
  display: flex
  align-items: center
  min-height: 400px
  height: calc(100vh - 362px - #{$control-height})
  padding: $big
  border: 1px dashed $border
  box-sizing: border-box
</style>

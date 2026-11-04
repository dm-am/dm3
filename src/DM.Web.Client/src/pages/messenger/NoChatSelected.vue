<template>
  <div class="no-chat">
    <secondary-text class="no-chat__text"
      >Выберите переписку справа или найдите собеседника через
      поиск</secondary-text
    >
  </div>
</template>

<style scoped lang="sass">
// About as tall as the feed it stands for, so opening one does not move the page.
.no-chat
  display: flex
  align-items: center
  min-height: 400px
  height: calc(100vh - 362px - #{$control-height})
  padding: $big
  border: 1px dashed $border
  box-sizing: border-box
</style>

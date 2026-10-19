<script setup lang="ts">
/**
 * The open correspondence with the list of conversations at its right, where
 * the right column stands on other pages, always whole: pictures, names, dates,
 * excerpts and the search. The page scrolls as a whole. The route is
 * requiresAuth, so there is no guest branch; the heading carries no count, the
 * header does.
 */
import ChatsList from "./ChatsList.vue";
</script>

<template>
  <page-title>Личные сообщения</page-title>
  <div class="messenger">
    <div class="messenger__chat">
      <router-view />
    </div>
    <div class="messenger__edge" aria-hidden="true" />
    <aside class="messenger__list" aria-label="Переписки">
      <ChatsList />
    </aside>
  </div>
</template>

<style scoped lang="sass">
// The list stands as wide as the right column of the other pages: its 330 less the gutter.
$list-width: 298px

// The correspondence takes what the list leaves; their tops stand level.
.messenger
  display: flex
  align-items: flex-start

.messenger__chat
  flex: 1
  min-width: 0

// The right column's gap with the frame's dashed line down its middle.
.messenger__edge
  flex: none
  align-self: stretch
  position: relative
  width: $big
  &::before
    content: ""
    position: absolute
    top: 0
    bottom: 0
    left: 50%
    border-left: 1px dashed $border

.messenger__list
  flex: none
  width: $list-width
  min-width: 0
</style>

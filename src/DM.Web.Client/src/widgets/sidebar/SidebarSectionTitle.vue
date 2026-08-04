<script setup lang="ts">
// Heading of a section inside a sidebar panel ("Управление игрой",
// "Модерация блога"). A section heading reads as a content title: bold, body
// colour and body size, no uppercase and no leading dash. That is what tells
// it apart from the menu rows under it and from the panel's own uppercase
// heading (SidebarBlock).
//
// One owner for the look. The game panel and the blog panel each kept a copy
// and drifted: the blog's headings had turned into small muted capitals, so
// the same rank of heading read as two different things on two panels built
// to the same plan.
</script>

<template>
  <li class="sidebar-section-title"><slot /></li>
</template>

<style scoped lang="sass">
.sidebar-section-title
  margin: $small 0 $tiny
  font-size: $font-size
  font-weight: bold
  color: $text
</style>

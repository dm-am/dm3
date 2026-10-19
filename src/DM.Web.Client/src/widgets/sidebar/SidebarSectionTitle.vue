<script setup lang="ts">
// The heading of a section of a panel: bold body text, apart from the rows and the panel's capitals.
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

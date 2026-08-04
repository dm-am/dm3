<script setup lang="ts">
// Loading twin of PageTitle for a zone shell (game, blog): the page holds the
// height of its title while the entity is on the wire, instead of drawing
// nothing and then shoving everything down by an h1 when the name lands.
//
// Geometry contract, matched to PageTitle: the same margins, and a height
// equal to the h1 line box (20px title font x 1.3 line height = 26px).
</script>

<template>
  <div class="skeleton-title" aria-hidden="true" />
</template>

<style scoped lang="sass">
@import "@/assets/styles/Skeleton"

.skeleton-title
  width: 260px
  height: 26px
  margin: $medium 0 $small
  +skeleton-shimmer
</style>

<script setup lang="ts">
/**
 * Loading twin of CharacterCard for the game roster.
 *
 * Geometry contract, matched to the card's collapsed header: the portrait on
 * the left at the card's own size, the name line and one meta line on the
 * right, inside the same $bg-element card at $small padding — and laid out in
 * the roster's own auto-fill grid, so the page does not reflow when the cast
 * arrives. Before this the roster of a full game read "В этой игре пока нет
 * персонажей" for the length of the request.
 */
withDefaults(
  defineProps<{
    /** Number of skeleton cards to show */
    count?: number;
  }>(),
  { count: 4 },
);
</script>

<template>
  <div class="character-skeleton-grid" aria-hidden="true">
    <div v-for="i in count" :key="i" class="skeleton-card">
      <div class="skeleton-portrait" />
      <div class="skeleton-info">
        <div class="skeleton-name" />
        <div class="skeleton-meta" />
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Skeleton"

.character-skeleton-grid
  display: grid
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr))
  gap: $medium

.skeleton-card
  display: flex
  align-items: flex-start
  gap: $small
  padding: $small
  background-color: $bg-element
  border-radius: $border-radius

.skeleton-portrait
  flex-shrink: 0
  width: $grid-step * 15
  height: $grid-step * 15
  border-radius: $border-radius
  +skeleton-shimmer

.skeleton-info
  flex: 1
  min-width: 0
  display: flex
  flex-direction: column
  gap: $tiny

.skeleton-name
  width: 60%
  height: 18px
  +skeleton-shimmer

.skeleton-meta
  width: 40%
  height: 12px
  +skeleton-shimmer
</style>

<script setup lang="ts">
/**
 * The decisions under a moderation record, as a bulleted list: the warning and
 * the change of its points.
 *
 * Drawn as the reviews under a post are (GamePost.vue, .reviews-section), the
 * owner's word for it (28.09): "предупреждения, как и изменения баллов, должны
 * быть оформлены, как оценки постов". The list keeps the numbers of that one,
 * and recordVerdictGeometry.spec.ts holds the two equal: a copy of numbers
 * with only a comment pointing at the original drifts the first time the
 * reviews move.
 */
</script>

<template>
  <ul class="record-verdict-list">
    <slot />
  </ul>
</template>

<style scoped lang="sass">
// GamePost.vue .reviews-section: the disc, the 40 of the old site's list, and
// the small step down from the rectangle above.
.record-verdict-list
  list-style: disc
  margin: 0
  padding-top: $small
  padding-left: $big + $small
</style>

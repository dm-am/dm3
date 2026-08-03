<script setup lang="ts">
// The counter of a sidebar menu row: "&nbsp;(N)", brackets included, zero
// included. Muted whole — value and brackets alike — because a sidebar
// counter is not a link and must not read as one (CounterPair, whose values
// ARE links, is the pattern for the other case).
//
// The non-breaking space is part of the composition: the counter never wraps
// away from the row it counts, and the row still copies as one human line.
defineProps<{ value: number }>();
</script>

<template>
  <span class="counter">&nbsp;({{ value }})</span>
</template>

<style scoped lang="sass">
.counter
  color: $text-muted
</style>

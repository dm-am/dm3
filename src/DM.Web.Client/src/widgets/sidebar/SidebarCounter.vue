<script setup lang="ts">
// "&nbsp;(N)", muted whole: not a link; the space keeps it on its row's line.
defineProps<{ value: number }>();
</script>

<template>
  <span class="counter">&nbsp;({{ value }})</span>
</template>

<style scoped lang="sass">
.counter
  color: $text-muted
</style>

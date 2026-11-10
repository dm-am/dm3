<script setup lang="ts">
/**
 * BubblesRow - Container for active filter bubbles with clear link.
 *
 * Wraps filter bubbles and provides "Сбросить" link.
 */

const emit = defineEmits<{
  "clear-all": [];
}>();
</script>

<template>
  <div class="bubbles-row">
    <slot />
    <button type="button" class="clear-all-link" @click="emit('clear-all')">
      Сбросить
    </button>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/FilterBubbles" as *

+filter-bubbles
</style>

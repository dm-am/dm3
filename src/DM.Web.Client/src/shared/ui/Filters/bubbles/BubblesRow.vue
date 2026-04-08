<script setup lang="ts">
/**
 * BubblesRow - Container for active filter bubbles with clear link.
 *
 * Wraps filter bubbles and provides "Сбросить все" link.
 */
defineOptions({ name: "BubblesRow" });

withDefaults(
  defineProps<{
    /** Show clear all link */
    showClearAll?: boolean;
  }>(),
  {
    showClearAll: true,
  },
);

const emit = defineEmits<{
  "clear-all": [];
}>();
</script>

<template>
  <div class="bubbles-row">
    <slot />
    <span
      v-if="showClearAll"
      class="clear-all-link"
      @click="emit('clear-all')"
    >
      Сбросить все
    </span>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Filters"

+filter-bubbles
</style>

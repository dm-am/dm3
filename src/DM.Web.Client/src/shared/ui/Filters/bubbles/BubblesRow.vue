<script setup lang="ts">
/**
 * BubblesRow - Container for active filter bubbles with clear link.
 *
 * Wraps filter bubbles and provides "Сбросить" link.
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
    <button
      v-if="showClearAll"
      type="button"
      class="clear-all-link"
      @click="emit('clear-all')"
    >
      Сбросить
    </button>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Filters"

+filter-bubbles

// Keep the button visually identical to the former <span>
.clear-all-link
  background: none
  border: none
  font-family: inherit
  line-height: inherit
</style>

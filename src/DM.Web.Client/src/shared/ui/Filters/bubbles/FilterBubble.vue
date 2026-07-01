<script setup lang="ts">
/**
 * FilterBubble - Single active filter bubble with remove button.
 *
 * Displays a filter value with optional prefix and remove button.
 */
import { symbols } from "@/shared/lib/utils/icons";

defineOptions({ name: "FilterBubble" });

withDefaults(
  defineProps<{
    /** Prefix text (e.g., "Статус:") */
    prefix?: string;
    /** Display value */
    value: string;
  }>(),
  {
    prefix: undefined,
  },
);

const emit = defineEmits<{
  remove: [];
}>();
</script>

<template>
  <div class="bubble">
    <span v-if="prefix" class="bubble-prefix">{{ prefix }} </span>
    <span class="bubble-value-text">{{ value }}</span>
    <button
      type="button"
      class="bubble-remove-btn"
      @click.stop="emit('remove')"
    >
      {{ symbols.close }}
    </button>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Filters"

+filter-bubbles
</style>

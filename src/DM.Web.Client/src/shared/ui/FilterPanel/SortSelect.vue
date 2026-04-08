<script setup lang="ts">
import type { SortOption } from "./types";

const props = defineProps<{
  /** Available sort options */
  options: SortOption[];
  /** Current sort value */
  modelValue: string;
  /** Current sort direction */
  direction?: "asc" | "desc";
}>();

const emit = defineEmits<{
  "update:modelValue": [value: string];
  "update:direction": [direction: "asc" | "desc"];
}>();

function handleChange(event: Event) {
  const select = event.target as HTMLSelectElement;
  const option = props.options.find((o) => o.value === select.value);
  emit("update:modelValue", select.value);
  if (option?.defaultDirection) {
    emit("update:direction", option.defaultDirection);
  }
}
</script>

<template>
  <div class="sort-select">
    <select :value="modelValue" @change="handleChange">
      <option
        v-for="option in options"
        :key="option.value"
        :value="option.value"
      >
        {{ option.label }}
      </option>
    </select>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"

.sort-select
  display: inline-block
  position: relative

  select
    min-width: 150px
    cursor: pointer
    appearance: none
    padding-right: $big
    +input()

  // Arrow icon using mask-image for theme-aware color
  &::after
    content: ""
    position: absolute
    right: $small
    top: 50%
    transform: translateY(-50%)
    width: 12px
    height: 12px
    pointer-events: none
    background-color: $text-muted
    mask-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 12 12'%3E%3Cpath d='M6 8L1 3h10z'/%3E%3C/svg%3E")
    mask-repeat: no-repeat
    mask-position: center
</style>

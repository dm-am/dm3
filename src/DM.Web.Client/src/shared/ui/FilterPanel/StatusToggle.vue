<script setup lang="ts">
import { computed } from "vue";
import type { FilterState, ToggleOption } from "./types";

const props = defineProps<{
  /** Available options */
  options: ToggleOption[];
  /** Current filter states (key -> state) */
  modelValue: Record<string, FilterState>;
}>();

const emit = defineEmits<{
  "update:modelValue": [value: Record<string, FilterState>];
}>();

function cycleState(option: ToggleOption) {
  const currentState = props.modelValue[option.value] || "neutral";
  const nextState: FilterState =
    currentState === "neutral"
      ? "include"
      : currentState === "include"
        ? "exclude"
        : "neutral";

  const newValue = { ...props.modelValue };
  if (nextState === "neutral") {
    delete newValue[option.value];
  } else {
    newValue[option.value] = nextState;
  }

  emit("update:modelValue", newValue);
}

function getStateClass(option: ToggleOption): string {
  const state = props.modelValue[option.value];
  if (!state || state === "neutral") return "neutral";
  return state;
}

function getState(option: ToggleOption): FilterState {
  return props.modelValue[option.value] || "neutral";
}

const hasAnyActiveFilter = computed(() => {
  return Object.keys(props.modelValue).length > 0;
});
</script>

<template>
  <div class="status-toggle">
    <button
      v-for="option in options"
      :key="option.value"
      type="button"
      class="toggle-chip"
      :class="getStateClass(option)"
      @click="cycleState(option)"
    >
      <!-- Include checkmark SVG -->
      <svg
        v-if="getState(option) === 'include'"
        class="state-icon"
        viewBox="0 0 24 24"
        fill="currentColor"
        aria-hidden="true"
      >
        <path d="M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z" />
      </svg>
      <!-- Exclude X SVG -->
      <svg
        v-else-if="getState(option) === 'exclude'"
        class="state-icon"
        viewBox="0 0 24 24"
        fill="currentColor"
        aria-hidden="true"
      >
        <path
          d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"
        />
      </svg>
      {{ option.label }}
    </button>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.status-toggle
  display: flex
  flex-wrap: wrap
  gap: $small

.toggle-chip
  display: inline-flex
  align-items: center
  gap: $tiny
  padding: $minor $small
  font-size: $secondary-font-size
  font-family: "PT Sans", sans-serif
  cursor: pointer
  border-radius: $border-radius
  transition: all 0.15s
  border: 1px solid $border
  background-color: $bg-element
  color: $text-muted

  &:hover
    border-color: $link
    color: $link

  &.include
    border-color: $accent-green
    color: $accent-green

  &.exclude
    border-color: $accent-red
    color: $accent-red

.state-icon
  width: 12px
  height: 12px
  flex-shrink: 0
</style>

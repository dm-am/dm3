<script setup lang="ts">
/**
 * FilterApplyButton - Apply/Clear buttons for filter inputs.
 *
 * Used in date range and numeric range pickers.
 */
defineOptions({ name: "FilterApplyButton" });

withDefaults(
  defineProps<{
    /** Button label */
    label?: string;
    /** Whether button is disabled */
    disabled?: boolean;
    /** Button variant */
    variant?: "primary" | "secondary";
  }>(),
  {
    label: "Применить",
    disabled: false,
    variant: "primary",
  },
);

const emit = defineEmits<{
  click: [];
}>();
</script>

<template>
  <button
    type="button"
    class="filter-apply-btn"
    :class="[`filter-apply-btn--${variant}`]"
    :disabled="disabled"
    @click="emit('click')"
  >
    {{ label }}
  </button>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"
@import "src/assets/styles/Variables"

.filter-apply-btn
  width: 100%
  padding: $small
  font-size: $secondary-font-size
  font-family: inherit
  cursor: pointer
  border: 1px solid $border
  border-radius: $border-radius
  transition: background-color 0.1s

  &--primary
    background-color: $bg-element
    color: $text

    &:hover:not(:disabled)
      background-color: $bg-element-accent

  &--secondary
    background-color: transparent
    color: $link
    border-color: transparent

    &:hover:not(:disabled)
      color: $link-hover
      text-decoration: underline

  &:disabled
    opacity: 0.5
    cursor: not-allowed
</style>

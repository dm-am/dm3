<script setup lang="ts">
/**
 * FilterApplyButton - Apply/Clear buttons for filter inputs.
 *
 * Used in date range and numeric range pickers.
 * "apply" — solid button (default), "clear" — text-link style.
 * Shows tooltip with reason when disabled.
 */
import { Tooltip } from "@/shared/ui/Tooltip";

defineOptions({ name: "FilterApplyButton" });

withDefaults(
  defineProps<{
    /** Button label */
    label?: string;
    /** Whether button is disabled */
    disabled?: boolean;
    /** Tooltip explaining why button is disabled */
    disabledReason?: string;
    /** Button variant: "apply" (solid) or "clear" (text link) */
    variant?: "apply" | "clear";
  }>(),
  {
    label: "Применить",
    disabled: false,
    disabledReason: undefined,
    variant: "apply",
  },
);

const emit = defineEmits<{
  click: [];
}>();
</script>

<template>
  <Tooltip :text="disabled ? disabledReason : undefined" placement="top">
    <button
      type="button"
      class="filter-apply-btn"
      :class="[`filter-apply-btn--${variant}`]"
      :disabled="disabled"
      @click="emit('click')"
    >
      {{ label }}
    </button>
  </Tooltip>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.filter-apply-btn
  width: 100%
  +button

  &--clear
    +button-link
</style>

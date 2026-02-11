<template>
  <div :class="['form-field', (label || $slots.label) ? 'form-field__labeled' : null]">
    <div v-if="label || $slots.label" class="form-field-label">
      <slot name="label">
        <label :for="name">{{ label }}</label>
      </slot>
    </div>
    <div class="form-field-row">
      <slot />
    </div>
    <span
      v-for="error in displayErrors"
      :key="error"
      class="form-field-error"
      role="alert"
      :id="name ? `${name}-error` : undefined"
    >
      {{ translateError(error) }}
    </span>
    <div v-if="$slots.hint" class="form-field-hint">
      <slot name="hint" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{
  label?: string;
  name?: string;
  errors?: string[];
}>();

// Filter out empty errors (from .required("") validation)
const displayErrors = computed(() => props.errors?.filter((e) => e) || []);

const errorMessages: Record<string, string> = {
  Empty: "Обязательное поле",
  Short: "Слишком короткое значение",
  Long: "Слишком длинное значение",
  Taken: "Уже занято",
  NotFound: "Не найдено",
  Invalid: "Некорректное значение",
};

const translateError = (error: string): string => {
  return errorMessages[error] || error;
};
</script>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.form-field__labeled
  display: flex
  flex-direction: column
  margin: $small 0
  gap: $minor

  &.error input
    animation-name: shake-error
    animation-duration: $animation-time
    animation-timing-function: ease-in-out
    border-color: $border-accent-red

    &:focus
      box-shadow: inset 0 0 $minor $border-accent-red

.form-field-label
  display: flex
  justify-content: space-between
  align-items: center
  color: $text-muted
  font-size: $secondary-font-size

  label
    color: inherit

  & input, & textarea, & select
    box-sizing: border-box

.form-field-row
  input, textarea, select
    width: 280px

.form-field-error
  display: block
  margin-top: $minor
  color: $accent-red
  font-size: $secondary-font-size

.form-field-hint
  margin-top: $minor
  color: $text-muted
  font-size: $secondary-font-size
  line-height: 1.4
</style>

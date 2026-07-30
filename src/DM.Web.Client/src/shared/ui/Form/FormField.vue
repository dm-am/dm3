<template>
  <div
    :class="[
      'form-field',
      label || $slots.label ? 'form-field__labeled' : null,
    ]"
  >
    <div v-if="label || $slots.label" class="form-field-label">
      <slot name="label">
        <label :for="name">{{ label }}</label>
      </slot>
      <span v-if="optional" class="form-field-optional">необязательно</span>
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
import { computed, inject } from "vue";
import { VALIDATION_MESSAGES } from "@/shared/lib/errors/validationErrors";

const props = defineProps<{
  label?: string;
  name?: string;
  errors?: string[];
  optional?: boolean;
}>();

// Filter out empty errors (from .required("") validation)
const displayErrors = computed(
  () => props.errors?.filter((e) => e?.trim()) || [],
);

// The vocabulary is the server's, so it is shared rather than declared here.
// This copy held six of the thirteen codes, which is why a password failing the
// digit rule showed the reader "RequiresDigit".
const injectedTranslations = inject<Record<string, string>>(
  "formFieldTranslations",
  {},
);
const errorMessages = { ...VALIDATION_MESSAGES, ...injectedTranslations };

const translateError = (error: string): string => {
  return errorMessages[error] || error;
};
</script>

<style scoped lang="sass">
.form-field__labeled
  display: flex
  flex-direction: column
  margin: $small 0
  gap: $minor

  &.error input
    animation-name: shake-error
    animation-duration: 0.4s
    animation-timing-function: ease-in-out
    border-color: $border-accent-red

    &:focus
      box-shadow: inset 0 0 $minor $border-accent-red

.form-field-label
  display: flex
  justify-content: space-between
  align-items: center
  color: $text
  font-size: $secondary-font-size

  label
    color: inherit

  & input, & textarea, & select
    box-sizing: border-box

.form-field-optional
  flex-shrink: 0
  color: $text-muted

.form-field-row
  :deep(input), :deep(textarea), :deep(select)
    width: 100%
    box-sizing: border-box

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

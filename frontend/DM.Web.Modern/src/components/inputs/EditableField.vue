<script setup lang="ts">
import { computed } from "vue";

const props = withDefaults(
  defineProps<{
    modelValue: string;
    label?: string;
    placeholder?: string;
    type?: "text" | "textarea";
    editing?: boolean;
    empty?: string;
    rows?: number;
  }>(),
  {
    type: "text",
    editing: false,
    empty: "Не указано",
    rows: 3,
  },
);

const emit = defineEmits<{
  (e: "update:modelValue", value: string): void;
}>();

const displayValue = computed(() => props.modelValue || props.empty);
const isEmpty = computed(() => !props.modelValue);
</script>

<template>
  <div class="editable-field" :class="{ 'is-editing': editing }">
    <label v-if="label" class="field-label">{{ label }}</label>

    <template v-if="editing">
      <textarea
        v-if="type === 'textarea'"
        :value="modelValue"
        :placeholder="placeholder || label"
        :rows="rows"
        class="field-input field-textarea"
        @input="emit('update:modelValue', ($event.target as HTMLTextAreaElement).value)"
      />
      <input
        v-else
        type="text"
        :value="modelValue"
        :placeholder="placeholder || label"
        class="field-input"
        @input="emit('update:modelValue', ($event.target as HTMLInputElement).value)"
      />
    </template>

    <span v-else class="field-value" :class="{ 'is-empty': isEmpty }">
      {{ displayValue }}
    </span>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.editable-field
  margin-bottom: $small

.field-label
  display: block
  font-size: $secondary-font-size
  color: $text-muted
  margin-bottom: $tiny

.field-value
  display: block
  color: $text
  line-height: 1.4

  &.is-empty
    color: $text-meta
    font-style: italic

.field-input
  width: 100%
  padding: $small
  border: 1px solid $border
  border-radius: $border-radius
  background: $bg-element
  color: $text
  font-size: inherit
  font-family: inherit
  transition: border-color 0.2s

  &:focus
    outline: none
    border-color: $link

  &::placeholder
    color: $text-meta

.field-textarea
  resize: vertical
  min-height: $grid-step * 20

.is-editing
  .field-label
    color: $text
</style>

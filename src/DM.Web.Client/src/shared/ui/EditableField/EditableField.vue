<script setup lang="ts">
import { computed } from "vue";

const props = withDefaults(
  defineProps<{
    modelValue?: string;
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

// The id the label points at. Random suffix, because a page renders a column of
// these at once and an id has to be unique on the page. Same shape as FormField
// generates.
const controlId = `editable-field-${Math.random().toString(36).slice(2, 9)}`;
</script>

<template>
  <!-- Display mode: render colon + space as REAL text nodes so
       Selection.toString() yields "Label: value" instead of
       "Labelvalue" (CSS `::after` content is invisible to the Selection
       API). Edit mode keeps label as a bare <label> for the input. -->
  <div class="editable-field" :class="{ 'is-editing': editing }">
    <template v-if="editing">
      <label v-if="label" class="field-label" :for="controlId">{{
        label
      }}</label>
      <textarea
        v-if="type === 'textarea'"
        :id="controlId"
        :value="modelValue"
        :placeholder="placeholder || label"
        :rows="rows"
        class="field-input field-textarea"
        @input="
          emit(
            'update:modelValue',
            ($event.target as HTMLTextAreaElement).value,
          )
        "
      />
      <input
        v-else
        :id="controlId"
        type="text"
        :value="modelValue"
        :placeholder="placeholder || label"
        class="field-input"
        @input="
          emit('update:modelValue', ($event.target as HTMLInputElement).value)
        "
      />
    </template>

    <template v-else>
      <span v-if="label" class="field-label">{{ label }}:</span>{{ " "
      }}<span class="field-value" :class="{ 'is-empty': isEmpty }">{{
        displayValue
      }}</span>
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

// No external margin — spacing is the parent container's responsibility
// (e.g. ProfilePersonalInfo `.info-grid` uses `gap: 0`). A built-in
// margin would stack with the parent gap, creating an uneven rhythm
// between rows that mix EditableField + StatLine siblings.
// line-height 1.25 is synchronized with StatLine — in `.info-grid` both
// field types alternate, and any line-height mismatch produces
// uneven inter-line spacing.
.editable-field
  display: block
  font-size: $font-size
  line-height: 1.25
  color: $text

.field-label
  color: $text

.field-value
  color: $text

.field-input
  font-family: inherit
  font-size: $font-size

.field-textarea
  resize: vertical
  min-height: $grid-step * 20
</style>

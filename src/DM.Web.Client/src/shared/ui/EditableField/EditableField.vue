<script setup lang="ts">
import { computed } from "vue";
import { FormField } from "@/shared/ui/Form";

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

// The id of the control, so the caption FormField draws points at it. Random
// suffix, because a page renders a column of these at once and an id has to be
// unique on the page. Same shape as FormField generates.
const controlId = `editable-field-${Math.random().toString(36).slice(2, 9)}`;
</script>

<template>
  <!-- Display mode: render colon + space as REAL text nodes so
       Selection.toString() yields "Label: value" instead of
       "Labelvalue" (CSS `::after` content is invisible to the Selection
       API). Edit mode is a field of the page: the caption over it, bold,
       the way every form of a page captions its fields (FormField). -->
  <div class="editable-field" :class="{ 'is-editing': editing }">
    <FormField v-if="editing" :label="label">
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
    </FormField>

    <template v-else>
      <span v-if="label" class="field-label">{{ label }}:</span>{{ " "
      }}<span class="field-value" :class="{ 'is-empty': isEmpty }">{{
        displayValue
      }}</span>
    </template>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

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

// The type is the site's: 14 in a field of one line, the body's 16 in the
// text area (+input in Inputs.sass). The row of FormField gives the width.
.field-textarea
  resize: vertical
  min-height: $grid-step * 20
</style>

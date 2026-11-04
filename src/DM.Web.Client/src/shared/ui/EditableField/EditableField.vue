<script setup lang="ts">
import { computed, useId } from "vue";
import { FormField } from "@/shared/ui/Form";

const props = withDefaults(
  defineProps<{
    modelValue?: string;
    label?: string;
    placeholder?: string;
    editing?: boolean;
    empty?: string;
  }>(),
  { editing: false, empty: "Не указано" },
);

const emit = defineEmits<{
  (e: "update:modelValue", value: string): void;
}>();

const displayValue = computed(() => props.modelValue || props.empty);

// FormField's caption points at the control by this id; a page draws a column of these at once.
const controlId = `editable-field-${useId()}`;
</script>

<template>
  <!-- The colon and space are real text, so a selection copies "Label: value". -->
  <div class="editable-field">
    <FormField v-if="editing" :label="label">
      <input
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
      }}<span class="field-value">{{ displayValue }}</span>
    </template>
  </div>
</template>

<style scoped lang="sass">
// No outer margin: the parent spaces the rows. line-height 1.25 matches StatLine, which alternates with it.
.editable-field
  display: block
  font-size: $font-size
  line-height: 1.25
  color: $text
</style>

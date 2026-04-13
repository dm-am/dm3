<script setup lang="ts">
/**
 * FilterSearchInput - Search input with icon and clear button.
 *
 * Simple input component - debouncing is handled by parent (useFilterSearch).
 */
import { SvgIcon } from "@/shared/ui/Icon";
import { symbols } from "@/shared/lib/utils/icons";

defineOptions({ name: "FilterSearchInput" });

const props = withDefaults(
  defineProps<{
    /** Current value (v-model) */
    modelValue: string;
    /** Placeholder text */
    placeholder?: string;
  }>(),
  {
    placeholder: "Поиск",
  },
);

const emit = defineEmits<{
  "update:modelValue": [value: string];
  input: [];
  keydown: [event: KeyboardEvent];
  focus: [];
  blur: [];
}>();

function handleInput(event: Event) {
  const value = (event.target as HTMLInputElement).value;
  emit("update:modelValue", value);
  emit("input");
}

function handleKeydown(event: KeyboardEvent) {
  emit("keydown", event);
}

function clearInput() {
  emit("update:modelValue", "");
  emit("input");
}
</script>

<template>
  <div class="search-container">
    <SvgIcon name="search" class="search-icon" />
    <input
      :value="modelValue"
      type="text"
      class="the-input"
      :placeholder="placeholder"
      @input="handleInput"
      @keydown="handleKeydown"
      @focus="emit('focus')"
      @blur="emit('blur')"
    />
    <button
      v-if="modelValue"
      type="button"
      class="clear-input-btn"
      @click.stop="clearInput"
    >
      {{ symbols.close }}
    </button>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Filters"

+filter-search-container
</style>

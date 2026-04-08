<script setup lang="ts">
import { ref, watch, onUnmounted } from "vue";
import { SvgIcon } from "@/shared/ui/Icon";

const props = withDefaults(
  defineProps<{
    /** Current search value */
    modelValue: string;
    /** Placeholder text */
    placeholder?: string;
    /** Debounce delay in milliseconds */
    debounce?: number;
  }>(),
  {
    placeholder: "Поиск",
    debounce: 300,
  },
);

const emit = defineEmits<{
  "update:modelValue": [value: string];
}>();

const localValue = ref(props.modelValue);
let debounceTimer: ReturnType<typeof setTimeout> | null = null;

watch(
  () => props.modelValue,
  (newValue) => {
    localValue.value = newValue;
  },
);

function handleInput(event: Event) {
  const input = event.target as HTMLInputElement;
  localValue.value = input.value;

  if (debounceTimer) {
    clearTimeout(debounceTimer);
  }

  debounceTimer = setTimeout(() => {
    emit("update:modelValue", localValue.value);
  }, props.debounce);
}

function handleClear() {
  localValue.value = "";
  emit("update:modelValue", "");
}

onUnmounted(() => {
  if (debounceTimer) {
    clearTimeout(debounceTimer);
  }
});
</script>

<template>
  <div class="search-input">
    <SvgIcon name="searchFilled" class="search-icon" aria-hidden="true" />
    <input
      type="text"
      :value="localValue"
      :placeholder="placeholder"
      @input="handleInput"
    />
    <button
      v-if="localValue"
      type="button"
      class="clear-btn"
      @click="handleClear"
      aria-label="Очистить поиск"
    >
      <SvgIcon name="closeThin" />
    </button>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"

.search-input
  position: relative
  display: inline-flex
  align-items: center

  .search-icon
    position: absolute
    left: $small
    width: 16px
    height: 16px
    color: $text-muted
    pointer-events: none

  input
    +input()
    &
      padding-left: $big
      padding-right: $big
      min-width: 200px

  .clear-btn
    position: absolute
    right: $small
    display: inline-flex
    align-items: center
    justify-content: center
    width: 20px
    height: 20px
    padding: 0
    font-size: 16px
    line-height: 1
    cursor: pointer
    border: none
    background: none
    color: $text-muted
    border-radius: 50%

    &:hover
      background-color: $bg-element-overlay
      color: $text
</style>

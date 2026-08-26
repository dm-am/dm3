<script setup lang="ts">
/**
 * DatePicker — a compact calendar popover styled in the site's own design
 * (no native <input type="date">). A trigger button opens the shared
 * CalendarGrid; picking a day emits the date as a YYYY-MM-DD string and
 * closes the popover.
 *
 * Closes on click-outside and Esc. The grid itself (month navigation,
 * min/max limits, highlights) lives in CalendarGrid.
 */
import { ref, onMounted, onUnmounted } from "vue";
import CalendarGrid from "./CalendarGrid.vue";

withDefaults(
  defineProps<{
    /** Selected date, YYYY-MM-DD (or null/empty for none). */
    modelValue?: string | null;
    /** Latest selectable date, YYYY-MM-DD. Later days are disabled. */
    max?: string;
    /** Earliest selectable date, YYYY-MM-DD. Earlier days are disabled. */
    min?: string;
    /** Trigger button label. */
    label?: string;
  }>(),
  { modelValue: null, label: "Перейти к дате" },
);

const emit = defineEmits<{ "update:modelValue": [value: string] }>();

const isOpen = ref(false);
const rootRef = ref<HTMLElement | null>(null);

function close() {
  isOpen.value = false;
}

function toggle() {
  isOpen.value = !isOpen.value;
}

function handlePick(value: string) {
  emit("update:modelValue", value);
  close();
}

function onDocClick(e: MouseEvent) {
  if (
    isOpen.value &&
    rootRef.value &&
    !rootRef.value.contains(e.target as Node)
  ) {
    close();
  }
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === "Escape" && isOpen.value) close();
}

onMounted(() => {
  document.addEventListener("click", onDocClick);
  document.addEventListener("keydown", onKeydown);
});
onUnmounted(() => {
  document.removeEventListener("click", onDocClick);
  document.removeEventListener("keydown", onKeydown);
});
</script>

<template>
  <div ref="rootRef" class="date-picker">
    <button
      type="button"
      class="dp-trigger"
      :aria-expanded="isOpen"
      @click.stop="toggle"
    >
      {{ label }}
    </button>

    <CalendarGrid
      v-if="isOpen"
      class="dp-popover"
      :model-value="modelValue"
      :min="min"
      :max="max"
      @update:model-value="handlePick"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ZIndex" as *
@use "@/assets/styles/Inputs" as *

.date-picker
  position: relative
  display: inline-block

// Same look as the filter / sort buttons (unified +button mixin).
.dp-trigger
  +button

// Panel visuals come from CalendarGrid — only the popover placement here.
.dp-popover
  position: absolute
  right: 0
  top: calc(100% + #{$tiny})
  z-index: $z-dropdown
</style>

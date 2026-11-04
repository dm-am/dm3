<script setup lang="ts">
// `open` is said when the calendar opens, so the feed asks for its first day only then.
import { ref } from "vue";
import { vClickOutside } from "@/shared/directives";
import { SvgIcon } from "@/shared/ui/Icon";
import { CalendarGrid } from "@/shared/ui/DatePicker";

withDefaults(
  defineProps<{
    selectedDate?: string;
    /** The day of the first message. */
    minDate?: string;
    maxDate?: string;
  }>(),
  { selectedDate: "", minDate: "", maxDate: "" },
);

const emit = defineEmits<{
  "date-picked": [value: string];
  open: [];
}>();

const open = ref(false);
const triggerRef = ref<HTMLButtonElement | null>(null);

function close() {
  open.value = false;
}

function toggle() {
  open.value = !open.value;
  if (open.value) emit("open");
}

function closeAndReturnFocus() {
  if (!open.value) return;
  close();
  triggerRef.value?.focus();
}

// Handed back to the button, the focus stays where it was instead of falling to the top.
function onPick(value: string) {
  close();
  triggerRef.value?.focus({ preventScroll: true });
  emit("date-picked", value);
}
</script>

<template>
  <div
    v-click-outside="close"
    class="date-section"
    @keydown.esc="closeAndReturnFocus"
  >
    <button
      ref="triggerRef"
      type="button"
      class="date-btn"
      :class="{ active: open }"
      aria-haspopup="dialog"
      aria-label="Перейти к дате"
      :aria-expanded="open"
      @click="toggle"
    >
      <SvgIcon name="calendar" class="date-icon" />
      <span>Перейти к дате</span>
    </button>

    <CalendarGrid
      v-if="open"
      class="date-popover"
      :model-value="selectedDate"
      :min="minDate || undefined"
      :max="maxDate || undefined"
      focus-on-open
      @update:model-value="onPick"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Buttons" as *
@use "@/assets/styles/ZIndex" as *

.date-section
  flex-shrink: 0

.date-btn
  justify-content: flex-start
  gap: $small
  +button

.date-icon
  width: 20px
  height: 20px
  flex-shrink: 0

// Hung from the row, not the button: when the row breaks, a button-hung calendar ran past it.
.date-popover
  position: absolute
  top: calc(100% + #{$tiny})
  right: 0
  z-index: $z-dropdown
</style>

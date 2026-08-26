<script setup lang="ts">
/**
 * ChatDateJump — "Перейти к дате" for the chat archive.
 *
 * A real button on the search row, built exactly like "Фильтры" and the sort
 * button everywhere else (+button, 20px icon, label), with the shared
 * CalendarGrid hanging under it. It used to be a text link inside the events
 * strip, which put chat-history navigation inside an events readout and made
 * "Поиск | К дате" a composite that copied broken.
 */
import { ref } from "vue";
import { vClickOutside } from "@/shared/directives";
import { SvgIcon } from "@/shared/ui/Icon";
import { CalendarGrid } from "@/shared/ui/DatePicker";

withDefaults(
  defineProps<{
    /** Currently selected archive date, YYYY-MM-DD (highlights in the calendar). */
    selectedDate?: string;
    /** Latest selectable date, YYYY-MM-DD. */
    maxDate?: string;
  }>(),
  { selectedDate: "", maxDate: "" },
);

const emit = defineEmits<{ "date-picked": [value: string] }>();

const open = ref(false);
const triggerRef = ref<HTMLButtonElement | null>(null);

function close() {
  open.value = false;
}

function toggle() {
  open.value = !open.value;
}

/** Escape gives focus back to the control that opened the calendar. */
function closeAndReturnFocus() {
  if (!open.value) return;
  close();
  triggerRef.value?.focus();
}

function onPick(value: string) {
  close();
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
      :max="maxDate"
      @update:model-value="onPick"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/ZIndex" as *

.date-section
  position: relative
  flex-shrink: 0

// Icon-specific additions only — the visual comes from the unified +button
// mixin, same as FilterButton and SortButton.
.date-btn
  justify-content: flex-start
  gap: $small
  +button

.date-icon
  width: 20px
  height: 20px
  flex-shrink: 0

.date-popover
  position: absolute
  top: calc(100% + #{$tiny})
  right: 0
  z-index: $z-dropdown
</style>

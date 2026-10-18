<script setup lang="ts">
/**
 * ChatDateJump — "Перейти к дате" over the feed of the global chat.
 *
 * A real button on the search row, built exactly like "Фильтры" and the sort
 * button everywhere else (+button, 20px icon, label), with the shared
 * CalendarGrid hanging under its row. It used to be a text link inside the
 * events strip, which put chat-history navigation inside an events readout
 * and made "Поиск | К дате" a composite that copied broken.
 *
 * The button says its words at every width, as on a desktop. It used to give
 * them up for its icon alone on a narrow row, a square a phone showed in
 * their place (the owner, 12 October 2026: nothing is shortened on a phone).
 * Where the row has no room for them beside the search, the row puts the
 * button under the field (ChatSearchRow).
 *
 * The calendar offers the days the feed has: up to today, and from the first
 * message on where the feed knows it (`minDate`). `open` is
 * said when the calendar opens, so the feed can ask for that first day only
 * when somebody is about to pick one.
 */
import { ref } from "vue";
import { vClickOutside } from "@/shared/directives";
import { SvgIcon } from "@/shared/ui/Icon";
import { CalendarGrid } from "@/shared/ui/DatePicker";

withDefaults(
  defineProps<{
    /** Currently selected archive date, YYYY-MM-DD (highlights in the calendar). */
    selectedDate?: string;
    /** Earliest selectable date, YYYY-MM-DD: the day of the first message. */
    minDate?: string;
    /** Latest selectable date, YYYY-MM-DD. */
    maxDate?: string;
  }>(),
  { selectedDate: "", minDate: "", maxDate: "" },
);

const emit = defineEmits<{
  "date-picked": [value: string];
  /** The calendar opened. */
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

/** Escape gives focus back to the control that opened the calendar. */
function closeAndReturnFocus() {
  if (!open.value) return;
  close();
  triggerRef.value?.focus();
}

/**
 * The calendar goes with the pick, and the focus it held with it; handed back
 * to the button, the keyboard stays where it was instead of falling to the
 * top of the page.
 */
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
      <span class="date-btn-label">Перейти к дате</span>
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
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/ZIndex" as *

// Not positioned: the calendar hangs from the row (below).
.date-section
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

// Hung from the row the button stands in (the search's filter bar, which is
// positioned), under it at its right edge. On one line that is the right edge
// of the button, at the end of the row. Where the row breaks and the button
// stands under the field, at its start (ChatSearchRow), a calendar hung from
// the button reached past the row on the left, over the column beside the
// chat; hung from the row, it keeps within it.
.date-popover
  position: absolute
  top: calc(100% + #{$tiny})
  right: 0
  z-index: $z-dropdown
</style>

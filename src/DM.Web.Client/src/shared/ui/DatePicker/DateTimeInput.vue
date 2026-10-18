<script setup lang="ts">
/**
 * DateTimeInput — a date and a time of day side by side: the site's date
 * field (DateInput) and time field (TimeInput) in one row.
 *
 * It stands where <input type="datetime-local"> stood, and keeps its value:
 * the model is the same "YYYY-MM-DDTHH:mm" the browser's field gave, so
 * toSiteInput and fromSiteInput read and write it as before. The browser's
 * field drew the date and the time in its own look and language ("06-Oct-2026
 * 05:30 AM") with a calendar of its own; this one is the site's, and the date
 * is a date that must be given, so it has no cross.
 *
 * Bounds are moments of the same spelling. The date field takes the days of
 * the bounds and greys the rest in its calendar; the time is judged here
 * against them, on the day of a bound. A value out of them is not taken: the
 * model keeps what it had, and the reason says the moment that is the limit
 * ("Не раньше 06.10.2026 05:30"). So does a value that the bounds moved out
 * from under (the end of a poll whose start moved past it).
 *
 * One reason for the row, for the consumer to show under it: the date's,
 * else the time's, else the bounds'. An emptied part empties the model, so a
 * form reads a date without a time as no moment at all, and not as the moment
 * it held before.
 *
 * The widths are the ones that hold the text of either part, 148 and 72 with
 * 8 between: in a form on a phone and in the card of a poll alike.
 */
import { computed, ref, watch } from "vue";
import DateInput from "./DateInput.vue";
import TimeInput from "./TimeInput.vue";

const props = withDefaults(
  defineProps<{
    /** The moment, "YYYY-MM-DDTHH:mm"; empty for none. */
    modelValue: string;
    /** The earliest moment taken, "YYYY-MM-DDTHH:mm". */
    min?: string;
    /** The latest moment taken, "YYYY-MM-DDTHH:mm". */
    max?: string;
    /** The id of the date field, for a caption that points at the row. */
    id?: string;
  }>(),
  { min: undefined, max: undefined, id: undefined },
);

const emit = defineEmits<{
  "update:modelValue": [value: string];
  /** Why the row is not a moment it takes; null once it is. */
  reason: [value: string | null];
}>();

const VALUE = /^(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2})$/;

const split = (value: string | undefined) => {
  const match = VALUE.exec(value ?? "");
  return match ? { date: match[1], time: match[2] } : null;
};

/** "2026-10-06T05:30" → "06.10.2026 05:30". */
const spell = (value: string) =>
  `${value.slice(8, 10)}.${value.slice(5, 7)}.${value.slice(0, 4)} ${value.slice(11, 16)}`;

const date = ref<string | null>(split(props.modelValue)?.date ?? null);
const time = ref<string | null>(split(props.modelValue)?.time ?? null);

// A moment set from outside (a form reset, the poll the editor opened on)
// is shown; the model the row emitted itself already is what it shows.
watch(
  () => props.modelValue,
  (value) => {
    const parts = split(value);
    if (parts && (parts.date !== date.value || parts.time !== time.value)) {
      date.value = parts.date;
      time.value = parts.time;
    }
  },
);

const minDate = computed(() => split(props.min)?.date);
const maxDate = computed(() => split(props.max)?.date);

/** The moment the two parts make, or "" while one of them is empty. */
const moment = computed(() =>
  date.value && time.value ? `${date.value}T${time.value}` : "",
);

const boundReason = computed(() => {
  const value = moment.value;
  if (!value) return null;
  if (props.min && value < props.min) return `Не раньше ${spell(props.min)}`;
  if (props.max && value > props.max) return `Не позже ${spell(props.max)}`;
  return null;
});

const dateReason = ref<string | null>(null);
const timeReason = ref<string | null>(null);

const reason = computed(
  () => dateReason.value ?? timeReason.value ?? boundReason.value,
);

watch(reason, (next) => emit("reason", next), { immediate: true });

watch(moment, (value) => {
  if (value && boundReason.value) return;
  if (value !== props.modelValue) emit("update:modelValue", value);
});
</script>

<template>
  <div class="date-time-input">
    <DateInput
      class="dti-date"
      :input-id="id"
      :model-value="date"
      :min="minDate"
      :max="maxDate"
      :clearable="false"
      aria-label="Дата"
      @update:model-value="date = $event"
      @reason="dateReason = $event"
    />
    <TimeInput
      class="dti-time"
      :model-value="time"
      aria-label="Время"
      @update:model-value="time = $event"
      @reason="timeReason = $event"
    />
  </div>
</template>

<style scoped lang="sass">
.date-time-input
  display: flex
  gap: $small
  max-width: 100%

// Ten characters and the calendar icon, and five characters: each part as
// wide as its text asks and no wider, so the row of two reads as one moment.
// The date gives first where the row is narrower than 228 (the card of a poll
// in a column of 225): it still holds its ten characters at 121.
// The second selector outweighs FormField's rule that stretches every input
// of its row across it.
.date-time-input .dti-date
  flex: 0 1 148px
  min-width: 0

.date-time-input .dti-time
  flex: 0 0 72px
  width: 72px
</style>

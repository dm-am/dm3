<script setup lang="ts">
// The model is datetime-local's "YYYY-MM-DDTHH:mm". An emptied part empties the model: a date without a
// time is no moment, not the moment held before. One reason for the row: the date's, else the time's, else the bounds'.
import { computed, ref, watch } from "vue";
import DateInput from "./DateInput.vue";
import TimeInput from "./TimeInput.vue";
import { boundReason } from "./typedField";

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

// A moment set from outside is shown; one the row emitted itself already is.
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

const outOfBounds = computed(() => {
  const value = moment.value;
  if (!value) return null;
  return boundReason(value, props.min, props.max, spell);
});

const dateReason = ref<string | null>(null);
const timeReason = ref<string | null>(null);

const reason = computed(
  () => dateReason.value ?? timeReason.value ?? outOfBounds.value,
);

watch(reason, (next) => emit("reason", next), { immediate: true });

watch(moment, (value) => {
  if (value && outOfBounds.value) return;
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

// Each part as wide as its text; the date gives first in a narrow row. The second selector outweighs
// FormField's rule that stretches every input of its row.
.date-time-input .dti-date
  flex: 0 1 148px
  min-width: 0

.date-time-input .dti-time
  flex: 0 0 72px
  width: 72px
</style>

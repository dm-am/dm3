<script setup lang="ts">
// The colon puts itself in, as the dots of a date do.
// The one element is the <input>, so the caller's class, id and aria reach it.
import { ref, watch } from "vue";
import { useInDialog } from "@/shared/ui/Layout/dialogContext";
import { reasonSayer, shapeInput } from "./typedField";

const props = withDefaults(
  defineProps<{
    /** The time, "HH:mm" (null for none). */
    modelValue: string | null;
    /** Input placeholder. */
    placeholder?: string;
    /** Accessible label for the field. */
    ariaLabel?: string;
  }>(),
  { placeholder: "чч:мм", ariaLabel: undefined },
);

const emit = defineEmits<{
  "update:modelValue": [value: string | null];
  /** Why the text in the field is not a time; null once it is. */
  reason: [value: string | null];
}>();

/** A field in a window takes the window's measure (see the styles). */
const inDialog = useInDialog();

/** "H:mm" or "HH:mm" → "HH:mm"; null when the text is not a time of day. */
function parseTime(raw: string): string | null {
  const match = /^(\d{1,2}):(\d{2})$/.exec(raw.trim());
  if (!match) return null;
  const hours = +match[1];
  const minutes = +match[2];
  if (hours > 23 || minutes > 59) return null;
  return `${String(hours).padStart(2, "0")}:${match[2]}`;
}

/**
 * The typed text with its colon put in: "233" is "23:3". Only a run of digits,
 * or one whose colon already stands where this one would, is touched. A
 * trailing colon is not added, so backspace deletes a digit and not a colon
 * the field puts back.
 */
function withColon(raw: string): string {
  if (!/^[\d:]*$/.test(raw)) return raw;
  const digits = raw.replace(/:/g, "");
  if (digits.length > 4) return raw;
  const colon = raw.indexOf(":");
  if (colon !== -1 && (colon !== 2 || raw.lastIndexOf(":") !== colon))
    return raw;
  return digits.length > 2
    ? `${digits.slice(0, 2)}:${digits.slice(2)}`
    : digits;
}

const text = ref(props.modelValue ?? "");

const setReason = reasonSayer((next) => emit("reason", next));

const reasonFor = (t: string): string | null =>
  t.trim() && !parseTime(t) ? "Такого времени нет" : null;

watch(
  () => props.modelValue,
  (val) => {
    if (parseTime(text.value) !== (val || null)) {
      text.value = val ?? "";
      setReason(null);
    }
  },
);

function onInput(e: Event) {
  const shaped = shapeInput(e.target as HTMLInputElement, withColon);
  text.value = shaped;
  if (!shaped.trim()) {
    if (props.modelValue !== null) emit("update:modelValue", null);
  } else {
    const parsed = parseTime(shaped);
    if (parsed && parsed !== props.modelValue)
      emit("update:modelValue", parsed);
  }
  if (reasonFor(shaped) === null) setReason(null);
}

/** Leaving the field: a time is written out in full, anything else is said. */
function settleText() {
  const next = reasonFor(text.value);
  setReason(next);
  if (next === null) text.value = parseTime(text.value) ?? "";
}
</script>

<template>
  <input
    :value="text"
    type="text"
    class="time-input"
    :class="{ 'time-input--dialog': inDialog }"
    inputmode="numeric"
    autocomplete="off"
    maxlength="5"
    :placeholder="placeholder"
    :aria-label="ariaLabel"
    @input="onInput"
    @blur="settleText"
    @keydown.enter.prevent="settleText"
  />
</template>

<style scoped lang="sass">
@use "@/shared/ui/DatePicker/calendar" as *

.time-input--dialog
  +window-field
</style>

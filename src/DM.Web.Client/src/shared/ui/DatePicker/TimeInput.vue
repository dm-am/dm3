<script setup lang="ts">
/**
 * TimeInput — a field for a time of day, "чч:мм", typed in digits.
 *
 * The site's own field in place of <input type="time">, whose look, language
 * and twelve-hour "PM" were the browser's. The colon puts itself in, as the
 * dots of a date do: "2330" reads "23:30" as it is typed, so a phone's numeric
 * keyboard, which often has no colon, is enough. A colon the reader typed in a
 * place of their own ("9:30") is left alone, and the text is written out in
 * full ("09:30") when the reader leaves the field.
 *
 * Typed only: no list of times and no icon. A time is four digits, and a list
 * of half hours added a control with a keyboard of its own and nothing a
 * reader could not type faster.
 *
 * Emits "HH:mm", or null when emptied. What does not make a time ("25:00",
 * "23:5") is not taken: it stays in the field, and leaving the field emits the
 * reason "Такого времени нет" for the consumer to show under it, as a date
 * field says "Такой даты нет". The reason clears as soon as the text is right.
 *
 * The one element is the <input>, so the caller's class, id and aria reach it,
 * and FormField sets it at the window's 52 and 15 as it sets any field.
 */
import { ref, watch } from "vue";
import { useInDialog } from "@/shared/ui/Layout/dialogContext";

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

/** The caret position just after the n-th digit of the text. */
function afterDigits(text: string, count: number): number {
  if (count === 0) return 0;
  const nth = [...text.matchAll(/\d/g)][count - 1];
  return nth?.index === undefined ? text.length : nth.index + 1;
}

const text = ref(props.modelValue ?? "");

/** The reason said last, so each change is said once. */
const reason = ref<string | null>(null);

function setReason(next: string | null) {
  if (next === reason.value) return;
  reason.value = next;
  emit("reason", next);
}

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
  const input = e.target as HTMLInputElement;
  const raw = input.value;
  const shaped = withColon(raw);
  if (shaped !== raw) {
    const caret = input.selectionStart ?? raw.length;
    const digitsBefore = raw.slice(0, caret).replace(/\D/g, "").length;
    input.value = shaped;
    const at = afterDigits(shaped, digitsBefore);
    input.setSelectionRange(at, at);
  }
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
@use "@/assets/styles/DialogSurface" as *

// In a window, the window's field: 52 high, its text at 15 and 14 in from the
// frame, as FormField sets a field of one line there. Said here as well, for
// the field that does not stand right in the row of a FormField (the time
// beside a date).
.time-input--dialog
  height: $dialog-control-height
  padding: 0 $dialog-field-inset
  font-size: $dialog-field-font-size
</style>

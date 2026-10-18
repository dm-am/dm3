<script setup lang="ts">
/**
 * YearInput — a field for a year, four digits.
 *
 * The site's own field in place of <input type="number">, which drew the
 * browser's arrows up and down beside the year and took "e" and a minus for a
 * number. Only digits go in, four at most; on a phone the numeric keyboard
 * opens. No grid of years: a year is four digits, and the one place that asks
 * for one (a new series of a contest) is filled by an administrator who knows
 * it.
 *
 * Emits the year as a number once four digits stand between `min` and `max`,
 * null when emptied. Anything else stays in the field, and leaving the field
 * emits the reason for the consumer to show under it: "Год из четырех цифр",
 * "Не раньше 2000", "Не позже 2100". The reason clears as soon as the year is
 * right.
 *
 * The one element is the <input>, so the caller's class and id reach it, and
 * FormField sets it at the window's 52 and 15 as it sets any field.
 */
import { ref, watch } from "vue";
import { useInDialog } from "@/shared/ui/Layout/dialogContext";

const props = withDefaults(
  defineProps<{
    /** The year (null for none). */
    modelValue: number | null;
    /** The earliest year taken. */
    min?: number;
    /** The latest year taken. */
    max?: number;
    /** Input placeholder. */
    placeholder?: string;
    /** Accessible label for the field. */
    ariaLabel?: string;
  }>(),
  {
    min: undefined,
    max: undefined,
    placeholder: "гггг",
    ariaLabel: undefined,
  },
);

const emit = defineEmits<{
  "update:modelValue": [value: number | null];
  /** Why the text in the field is not a year it takes; null once it is. */
  reason: [value: string | null];
}>();

/** A field in a window takes the window's measure (see the styles). */
const inDialog = useInDialog();

const text = ref(props.modelValue === null ? "" : String(props.modelValue));

/** Why the text is not a year the field takes, or null when it is one. */
function reasonFor(t: string): string | null {
  if (!t) return null;
  if (!/^\d{4}$/.test(t)) return "Год из четырех цифр";
  const year = +t;
  if (props.min !== undefined && year < props.min)
    return `Не раньше ${props.min}`;
  if (props.max !== undefined && year > props.max)
    return `Не позже ${props.max}`;
  return null;
}

/** The reason said last, so each change is said once. */
const reason = ref<string | null>(null);

function setReason(next: string | null) {
  if (next === reason.value) return;
  reason.value = next;
  emit("reason", next);
}

watch(
  () => props.modelValue,
  (val) => {
    const shown =
      reasonFor(text.value) === null && text.value ? +text.value : null;
    if (shown !== val) {
      text.value = val === null ? "" : String(val);
      setReason(null);
    }
  },
);

function onInput(e: Event) {
  const input = e.target as HTMLInputElement;
  // Digits only: a letter or a sign typed or pasted does not get in.
  const digits = input.value.replace(/\D/g, "").slice(0, 4);
  if (digits !== input.value) input.value = digits;
  text.value = digits;
  const next = reasonFor(digits);
  if (!digits) {
    if (props.modelValue !== null) emit("update:modelValue", null);
  } else if (next === null && +digits !== props.modelValue) {
    emit("update:modelValue", +digits);
  }
  if (next === null) setReason(null);
}

/**
 * Enter sends the form the field is in, as in any field of it, but not a
 * year the field does not take: that is said instead, and the form waits.
 */
function onEnter(e: KeyboardEvent) {
  const next = reasonFor(text.value);
  setReason(next);
  if (next) e.preventDefault();
}
</script>

<template>
  <input
    :value="text"
    type="text"
    class="year-input"
    :class="{ 'year-input--dialog': inDialog }"
    inputmode="numeric"
    autocomplete="off"
    maxlength="4"
    :placeholder="placeholder"
    :aria-label="ariaLabel"
    @input="onInput"
    @blur="setReason(reasonFor(text))"
    @keydown.enter="onEnter"
  />
</template>

<style scoped lang="sass">
@use "@/assets/styles/DialogSurface" as *

// In a window, the window's field: 52 high, its text at 15 and 14 in from the
// frame, as FormField sets a field of one line there. Said here as well, for
// the field that does not stand right in the row of a FormField (the time
// beside a date).
.year-input--dialog
  height: $dialog-control-height
  padding: 0 $dialog-field-inset
  font-size: $dialog-field-font-size
</style>

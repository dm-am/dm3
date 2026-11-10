<script setup lang="ts">
// Digits only, four at most: <input type="number"> drew arrows and took "e" and a minus.
// The one element is the <input>, so the caller's class and id reach it.
import { ref, watch } from "vue";
import { useInDialog } from "@/shared/ui/Layout/dialogContext";
import { boundReason, reasonSayer } from "./typedField";

const props = withDefaults(
  defineProps<{
    /** The year (null for none). */
    modelValue: number | null;
    /** The earliest year taken. */
    min?: number;
    /** The latest year taken. */
    max?: number;
    /** Accessible label for the field. */
    ariaLabel?: string;
  }>(),
  {
    min: undefined,
    max: undefined,
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
  return boundReason(+t, props.min, props.max, String);
}

const setReason = reasonSayer((next) => emit("reason", next));

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
    placeholder="гггг"
    :aria-label="ariaLabel"
    @input="onInput"
    @blur="setReason(reasonFor(text))"
    @keydown.enter="onEnter"
  />
</template>

<style scoped lang="sass">
@use "@/shared/ui/DatePicker/calendar" as *

.year-input--dialog
  +window-field
</style>

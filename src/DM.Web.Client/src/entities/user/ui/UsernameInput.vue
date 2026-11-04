<template>
  <div class="username-input">
    <div class="input-wrapper">
      <input
        :id="id"
        type="text"
        :value="modelValue"
        @input="onInput"
        :disabled="disabled"
        :class="{ 'has-status': showStatus }"
        :maxlength="USERNAME_MAX_LENGTH"
        autocomplete="off"
        spellcheck="false"
      />
      <span
        v-if="showStatus && !checking"
        class="status-icon"
        :class="statusClass"
      >
        <span v-if="isAvailable === true">{{ symbols.checkmark }}</span>
        <span v-else-if="isAvailable === false">{{ symbols.cross }}</span>
      </span>
    </div>
    <div class="username-status">
      <span v-if="validationError" class="error">{{ validationError }}</span>
      <span v-else-if="isAvailable === true" class="available"
        >Имя свободно</span
      >
      <span v-else-if="isAvailable === false" class="unavailable">{{
        unavailableReason
      }}</span>
      <span v-else class="hint">2-20 символов</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from "vue";
import { symbols } from "@/shared/lib/utils/icons";
import { SEARCH_DEBOUNCE_MS } from "@/shared/lib/constants/search";
import { accountApi } from "../api";
import {
  USERNAME_MAX_LENGTH,
  USERNAME_MIN_LENGTH,
} from "@/shared/lib/constants/user";

const props = withDefaults(
  defineProps<{
    modelValue: string;
    disabled?: boolean;
    debounceMs?: number;
    id?: string;
  }>(),
  { debounceMs: SEARCH_DEBOUNCE_MS },
);

const emit = defineEmits<{
  "update:modelValue": [value: string];
  availability: [available: boolean];
}>();

const checking = ref(false);
const isAvailable = ref<boolean | null>(null);
const reason = ref<string | null>(null);
const validationError = ref<string | null>(null);

let debounceTimer: ReturnType<typeof setTimeout> | null = null;

// docs/conventions/USERNAME_POLICY.md; control characters are matched on purpose.
const forbiddenPattern =
  // eslint-disable-next-line no-control-regex
  /[\x00-\x1F\x7F<>"'`\\/@?#%&[\](){}=~!$^*+|;:\u200B-\u200F\u2028-\u202F\uFEFF]/;

const showStatus = computed(() => {
  return (
    props.modelValue.length >= USERNAME_MIN_LENGTH &&
    (checking.value || isAvailable.value !== null)
  );
});

const statusClass = computed(() => {
  if (isAvailable.value === true) return "available";
  if (isAvailable.value === false) return "unavailable";
  return "";
});

// "Taken" also covers a name a former user left behind, so the field does not claim an owner.
const NAME_UNAVAILABLE = "Это имя занято";

// A forbidden character is fixed by typing another one, so it does not read as "taken".
const unavailableReason = computed(() =>
  reason.value === "InvalidFormat" ? "Недопустимый символ" : NAME_UNAVAILABLE,
);

function validateFormat(username: string): string | null {
  if (username.length === 0) return null;
  if (username.length < USERNAME_MIN_LENGTH) return "Минимум 2 символа";
  if (username.length > USERNAME_MAX_LENGTH) return "Максимум 20 символов";
  if (forbiddenPattern.test(username)) return "Недопустимый символ";
  if (/^\s/.test(username)) return "Не может начинаться с пробела";
  if (/\s$/.test(username)) return "Не может заканчиваться пробелом";
  if (/\s{2}/.test(username)) return "Пробелы не могут идти подряд";
  return null;
}

function onInput(event: Event) {
  const target = event.target as HTMLInputElement;
  const value = target.value;
  emit("update:modelValue", value);

  checking.value = false;
  isAvailable.value = null;
  reason.value = null;
  validationError.value = validateFormat(value);

  if (debounceTimer) {
    clearTimeout(debounceTimer);
    debounceTimer = null;
  }

  if (validationError.value) {
    emit("availability", false);
    return;
  }

  if (value.length >= USERNAME_MIN_LENGTH) {
    checking.value = true;
    debounceTimer = setTimeout(
      () => checkAvailability(value),
      props.debounceMs,
    );
  } else {
    emit("availability", false);
  }
}

async function checkAvailability(username: string) {
  try {
    const { data, error } = await accountApi.checkUsername(username);

    // The value changed while the request was out.
    if (username !== props.modelValue) return;

    if (error) {
      isAvailable.value = false;
      reason.value = null;
      emit("availability", false);
      return;
    }

    if (data) {
      isAvailable.value = data.isAvailable;
      reason.value = data.reason || null;
      emit("availability", data.isAvailable);
    }
  } catch {
    isAvailable.value = false;
    emit("availability", false);
  } finally {
    checking.value = false;
  }
}

// A form that was reset enables the field again.
watch(
  () => props.disabled,
  (newDisabled) => {
    if (!newDisabled && props.modelValue.length >= USERNAME_MIN_LENGTH) {
      checkAvailability(props.modelValue);
    }
  },
);
</script>

<style scoped lang="sass">
.username-input
  width: 100%

.input-wrapper
  position: relative
  width: 100%

  input
    width: 100%
    box-sizing: border-box
    // Room for the mark, at the field's inset on either side of it.
    padding-right: $font-size + 2 * $field-inset

.status-icon
  position: absolute
  right: $field-inset
  top: 50%
  transform: translateY(-50%)
  font-size: $font-size
  pointer-events: none
  display: flex
  align-items: center
  justify-content: center

  &.available
    color: $accent-green

  &.unavailable
    color: $accent-red

.username-status
  margin-top: $small
  font-size: $secondary-font-size
  // A line held for the status, so the form does not move when it speaks.
  min-height: 1lh

  .hint
    color: $text-muted

  .available
    color: $accent-green
    font-weight: 400

  .error, .unavailable
    color: $accent-red
</style>

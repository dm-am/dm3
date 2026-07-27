<template>
  <div class="username-input">
    <div class="input-wrapper">
      <input
        type="text"
        :value="modelValue"
        @input="onInput"
        :disabled="disabled"
        :class="{ 'has-status': showStatus }"
        maxlength="20"
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
      <template v-if="validationError">
        <span class="error">{{ validationError }}</span>
      </template>
      <template v-else-if="isAvailable === true">
        <span class="available">Имя свободно</span>
      </template>
      <template v-else-if="isAvailable === false">
        <span class="unavailable">{{ unavailableReason }}</span>
      </template>
      <template v-else>
        <span class="hint">2–20 символов</span>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from "vue";
import { symbols } from "@/shared/lib/utils/icons";
import { AccountApi } from "@/shared/api";

const props = withDefaults(
  defineProps<{
    modelValue: string;
    disabled?: boolean;
    debounceMs?: number;
  }>(),
  {
    disabled: false,
    debounceMs: 300,
  },
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

// Forbidden: control chars, HTML/URL unsafe, quotes, brackets, special chars, zero-width
// See: docs/architecture/USERNAME_POLICY.md
// Control characters are matched intentionally (forbidden in usernames).
const forbiddenPattern =
  // eslint-disable-next-line no-control-regex
  /[\x00-\x1F\x7F<>"'`\\/@?#%&[\](){}=~!$^*+|;:\u200B-\u200F\u2028-\u202F\uFEFF]/;

const showStatus = computed(() => {
  return (
    props.modelValue.length >= 2 &&
    (checking.value || isAvailable.value !== null)
  );
});

const statusClass = computed(() => {
  if (isAvailable.value === true) return "available";
  if (isAvailable.value === false) return "unavailable";
  return "";
});

const unavailableReason = computed(() => {
  switch (reason.value) {
    case "taken":
      return "Имя уже занято";
    case "reserved":
      return "Имя зарезервировано";
    case "invalid_format":
      return "Недопустимые символы";
    default:
      return "Имя недоступно";
  }
});

function validateFormat(username: string): string | null {
  if (username.length === 0) return null;
  if (username.length < 2) return "Минимум 2 символа";
  if (username.length > 20) return "Максимум 20 символов";
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

  // Reset state
  checking.value = false;
  isAvailable.value = null;
  reason.value = null;
  validationError.value = validateFormat(value);

  // Clear pending debounce
  if (debounceTimer) {
    clearTimeout(debounceTimer);
    debounceTimer = null;
  }

  // Emit unavailable if validation fails
  if (validationError.value) {
    emit("availability", false);
    return;
  }

  // Check availability with debounce
  if (value.length >= 2) {
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
    const { data, error } = await AccountApi.checkUsername(username);

    // Ignore if value changed during request
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

// Re-check when disabled changes (in case form was reset)
watch(
  () => props.disabled,
  (newDisabled) => {
    if (!newDisabled && props.modelValue.length >= 2) {
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
    padding-right: 2.5rem

.status-icon
  position: absolute
  right: 0.75rem
  top: 50%
  transform: translateY(-50%)
  font-size: 1rem
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
  min-height: 1.4em

  .hint
    color: $text-muted

  .available
    color: $accent-green
    font-weight: 500

  .error, .unavailable
    color: $accent-red
</style>

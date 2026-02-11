<template>
  <div class="login-input">
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
      <span v-if="showStatus" class="status-icon" :class="statusClass">
        <span v-if="checking" class="spinner" />
        <span v-else-if="isAvailable === true">&#10003;</span>
        <span v-else-if="isAvailable === false">&#10007;</span>
      </span>
    </div>
    <div class="login-status">
      <template v-if="checking">
        <span class="checking">Проверяем...</span>
      </template>
      <template v-else-if="validationError">
        <span class="error">{{ validationError }}</span>
      </template>
      <template v-else-if="isAvailable === true">
        <span class="available">Имя свободно</span>
      </template>
      <template v-else-if="isAvailable === false">
        <span class="unavailable">{{ unavailableReason }}</span>
      </template>
      <template v-else>
        <span class="hint">2–20 символов. Нельзя: &lt; &gt; " ' ` \</span>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from "vue";
import accountApi from "@/api/requests/accountApi";

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

// Forbidden: control chars, HTML unsafe, quotes, backslash, zero-width
const forbiddenPattern = /[\x00-\x1F\x7F<>"'`\\\u200B-\u200F\u2028-\u202F\uFEFF]/;

const showStatus = computed(() => {
  return (
    props.modelValue.length >= 2 &&
    (checking.value || isAvailable.value !== null)
  );
});


const statusClass = computed(() => {
  if (checking.value) return "checking";
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

function validateFormat(login: string): string | null {
  if (login.length === 0) return null;
  if (login.length < 2) return "Минимум 2 символа";
  if (login.length > 20) return "Максимум 20 символов";
  if (forbiddenPattern.test(login)) return "Недопустимые символы: < > \" ' ` \\";
  if (/^\s/.test(login)) return "Не может начинаться с пробела";
  if (/\s$/.test(login)) return "Не может заканчиваться пробелом";
  if (/\s{2}/.test(login)) return "Пробелы не могут идти подряд";
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
    debounceTimer = setTimeout(() => checkAvailability(value), props.debounceMs);
  } else {
    emit("availability", false);
  }
}

async function checkAvailability(login: string) {
  try {
    const { data, error } = await accountApi.checkLogin(login);

    // Ignore if value changed during request
    if (login !== props.modelValue) return;

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
@import "@/assets/styles/Variables"

.login-input
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

  &.checking
    color: $text-muted

  &.available
    color: $accent-green

  &.unavailable
    color: $accent-red

.spinner
  display: inline-block
  width: 1rem
  height: 1rem
  border: 2px solid $text-muted
  border-top-color: transparent
  border-radius: 50%
  animation: spin 0.8s linear infinite

@keyframes spin
  to
    transform: rotate(360deg)

.login-status
  margin-top: $small
  font-size: $secondary-font-size
  min-height: 1.4em

  .hint
    color: $text-muted

  .checking
    color: $text-muted

  .available
    color: $accent-green
    font-weight: 500

  .error, .unavailable
    color: $accent-red
</style>

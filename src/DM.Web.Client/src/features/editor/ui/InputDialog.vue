<script setup lang="ts">
/**
 * InputDialog - Reusable modal dialog for user input
 * Replaces browser prompt() with styled modal
 */
import { ref, watch, nextTick } from "vue";

export interface InputField {
  name: string;
  label: string;
  type?: "text" | "url" | "email";
  placeholder?: string;
  required?: boolean;
  validator?: (value: string) => string | null; // Returns error message or null if valid
}

const props = withDefaults(
  defineProps<{
    show: boolean;
    title: string;
    fields: InputField[];
    submitLabel?: string;
    cancelLabel?: string;
  }>(),
  {
    submitLabel: "OK",
    cancelLabel: "Отмена",
  },
);

const emit = defineEmits<{
  (e: "update:show", value: boolean): void;
  (e: "submit", values: Record<string, string>): void;
  (e: "cancel"): void;
}>();

const values = ref<Record<string, string>>({});
const errors = ref<Record<string, string>>({});
const firstInput = ref<HTMLInputElement | null>(null);

// Initialize values when fields change
watch(
  () => props.fields,
  (fields) => {
    const newValues: Record<string, string> = {};
    fields.forEach((f) => {
      newValues[f.name] = values.value[f.name] || "";
    });
    values.value = newValues;
    errors.value = {};
  },
  { immediate: true },
);

// Focus first input when shown
watch(
  () => props.show,
  (show) => {
    if (show) {
      nextTick(() => {
        firstInput.value?.focus();
      });
    }
  },
);

function validateField(field: InputField): boolean {
  const value = values.value[field.name] || "";

  if (field.required && !value.trim()) {
    errors.value[field.name] = "Это поле обязательно";
    return false;
  }

  if (field.type === "url" && value.trim()) {
    try {
      const url = new URL(value);
      // Security: Only allow safe protocols (prevent javascript:, data: XSS)
      const allowedProtocols = ["http:", "https:", "mailto:"];
      if (!allowedProtocols.includes(url.protocol)) {
        errors.value[field.name] = "Разрешены только HTTP и HTTPS ссылки";
        return false;
      }
    } catch {
      errors.value[field.name] = "Введите корректный URL";
      return false;
    }
  }

  if (field.validator) {
    const error = field.validator(value);
    if (error) {
      errors.value[field.name] = error;
      return false;
    }
  }

  delete errors.value[field.name];
  return true;
}

function validateAll(): boolean {
  let valid = true;
  props.fields.forEach((field) => {
    if (!validateField(field)) {
      valid = false;
    }
  });
  return valid;
}

function handleSubmit() {
  if (validateAll()) {
    emit("submit", { ...values.value });
    close();
  }
}

function close() {
  emit("update:show", false);
  values.value = {};
  errors.value = {};
}

function handleCancel() {
  emit("cancel");
  close();
}

function handleKeydown(e: KeyboardEvent) {
  if (e.key === "Escape") {
    handleCancel();
  } else if (e.key === "Enter" && !e.shiftKey) {
    e.preventDefault();
    handleSubmit();
  }
}

function handleBackdropClick(e: MouseEvent) {
  if (e.target === e.currentTarget) {
    handleCancel();
  }
}
</script>

<template>
  <Teleport to="body">
    <Transition name="dialog">
      <div
        v-if="show"
        class="dialog-backdrop"
        @click="handleBackdropClick"
        @keydown="handleKeydown"
      >
        <div
          class="dialog-container"
          role="dialog"
          aria-modal="true"
          :aria-label="title"
        >
          <div class="dialog-header">
            <h3 class="dialog-title">{{ title }}</h3>
            <button
              type="button"
              class="dialog-close"
              @click="handleCancel"
              aria-label="Закрыть"
            >
              &times;
            </button>
          </div>

          <form class="dialog-content" @submit.prevent="handleSubmit">
            <div
              v-for="(field, index) in fields"
              :key="field.name"
              class="dialog-field"
            >
              <label :for="`dialog-${field.name}`" class="dialog-label">
                {{ field.label }}
                <span v-if="field.required" class="required">*</span>
                <span v-else class="optional">(необязательно)</span>
              </label>
              <input
                :id="`dialog-${field.name}`"
                :ref="
                  index === 0
                    ? (el) => (firstInput = el as HTMLInputElement)
                    : undefined
                "
                v-model="values[field.name]"
                :type="field.type || 'text'"
                :placeholder="field.placeholder"
                class="dialog-input"
                :class="{ error: errors[field.name] }"
                @blur="validateField(field)"
              />
              <span v-if="errors[field.name]" class="dialog-error">{{
                errors[field.name]
              }}</span>
            </div>
          </form>

          <div class="dialog-footer">
            <button
              type="button"
              class="dialog-btn dialog-btn-cancel"
              @click="handleCancel"
            >
              {{ cancelLabel }}
            </button>
            <button
              type="button"
              class="dialog-btn dialog-btn-submit"
              @click="handleSubmit"
            >
              {{ submitLabel }}
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"
@import "src/assets/styles/ZIndex"
@import "src/assets/styles/Animations"

.dialog-backdrop
  position: fixed
  inset: 0
  z-index: $z-modal
  display: flex
  align-items: center
  justify-content: center
  background-color: $overlay-bg
  backdrop-filter: blur(2px)

.dialog-container
  width: 100%
  max-width: 400px
  margin: $medium
  border-radius: $border-radius
  box-shadow: 0 8px 32px var(--shadow-color)
  background-color: $bg-element
  border: 1px solid $border

.dialog-header
  display: flex
  justify-content: space-between
  align-items: center
  padding: $medium
  border-bottom: 1px solid
  border-color: $border

.dialog-title
  margin: 0
  font-size: $font-size
  font-weight: 600

.dialog-close
  padding: 4px 8px
  border: none
  background: none
  font-size: 20px
  cursor: pointer
  color: $text-muted

  &:hover
    color: $text

.dialog-content
  padding: $medium

.dialog-field
  margin-bottom: $medium

  &:last-child
    margin-bottom: 0

.dialog-label
  display: block
  margin-bottom: $small
  font-size: $secondary-font-size
  font-weight: 500
  color: $text

  .required
    color: $accent-red

  .optional
    color: $text-muted
    font-weight: normal
    font-size: $tertiary-font-size

.dialog-input
  box-sizing: border-box
  width: 100%
  padding: $small $medium
  border: 1px dashed $border
  font-size: $font-size
  font-family: inherit
  background-color: $input-bg-overlay
  color: $text

  &:focus
    outline: none
    border-style: solid
    border-color: $button-border-hover

  &::placeholder
    color: $text-muted
    opacity: 0.6

.dialog-error
  display: block
  margin-top: 4px
  font-size: 11px
  color: $accent-red

.dialog-footer
  display: flex
  justify-content: flex-start
  gap: $small
  padding: $medium
  border-top: 1px solid
  border-color: $border

.dialog-btn
  font-size: $secondary-font-size
  +button

// Transition animations
.dialog-enter-active,
.dialog-leave-active
  transition: opacity 0.2s ease
  @media (prefers-reduced-motion: reduce)
    transition: none

.dialog-enter-from,
.dialog-leave-to
  opacity: 0

  .dialog-container
    transform: scale(0.95)

.dialog-enter-active .dialog-container,
.dialog-leave-active .dialog-container
  transition: transform 0.2s ease
  @media (prefers-reduced-motion: reduce)
    transition: none
</style>

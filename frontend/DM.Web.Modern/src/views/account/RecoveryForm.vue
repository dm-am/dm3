<script setup lang="ts">
import { ref, nextTick } from "vue";
import { useRouter } from "vue-router";
import TheLightbox from "@/components/layout/TheLightbox.vue";
import LightboxTitle from "@/components/layout/LightboxTitle.vue";
import TheButton from "@/components/inputs/TheButton.vue";
import AccountApi from "@/api/requests/accountApi";
import { useValidatedField, validators } from "@/composables/useValidatedField";

const router = useRouter();

const props = defineProps<{
  prefillEmail?: string;
}>();

const emit = defineEmits<{
  (e: "cancel"): void;
}>();

// Form state
type Result = "password" | "activation" | "notfound" | null;
const result = ref<Result>(null);
const loading = ref(false);
const serverError = ref("");

// Email field with validation
const emailField = useValidatedField({
  initialValue: props.prefillEmail || "",
  validate: validators.combine(
    validators.required(),
    validators.email()
  ),
});

const goToSupport = () => {
  emit("cancel");
  nextTick(() => router.push("/support?reason=access"));
};

const submit = async () => {
  // Validate first
  const isValid = await emailField.validate();
  if (!isValid) return;

  loading.value = true;
  serverError.value = "";

  try {
    const { data, error } = await AccountApi.recover(emailField.value.value.trim());

    if (error || !data) {
      serverError.value = "Произошла ошибка. Попробуйте позже.";
      return;
    }

    if (data.status === "NotFound") {
      result.value = "notfound";
    } else if (data.status === "ActivationResent") {
      result.value = "activation";
    } else {
      result.value = "password";
    }
  } catch {
    serverError.value = "Произошла ошибка. Попробуйте позже.";
  } finally {
    loading.value = false;
  }
};

const tryAgain = () => {
  result.value = null;
  emailField.reset();
  serverError.value = "";
};
</script>

<template>
  <the-lightbox :narrow="!result">
    <!-- Result: Password reset sent -->
    <template v-if="result === 'password'">
      <div class="success-content">
        <lightbox-title>Проверьте почту</lightbox-title>
        <p class="main-text">
          Мы отправили письмо на <strong>{{ emailField.value.value }}</strong> со ссылкой для сброса пароля.
        </p>
        <p class="expiry-note">Ссылка действительна 48 часов</p>
      </div>
    </template>

    <!-- Result: Activation resent -->
    <template v-else-if="result === 'activation'">
      <div class="success-content">
        <lightbox-title>Проверьте почту</lightbox-title>
        <p class="main-text">
          Мы отправили повторное письмо на <strong>{{ emailField.value.value }}</strong> со ссылкой для активации.
        </p>
        <p class="expiry-note">Ссылка действительна 48 часов</p>
      </div>
    </template>

    <!-- Result: Not found -->
    <template v-else-if="result === 'notfound'">
      <div class="success-content">
        <lightbox-title>Аккаунт не найден</lightbox-title>
        <p class="main-text">
          Аккаунта с почтой <strong>{{ emailField.value.value }}</strong> не существует.
        </p>
        <p class="expiry-note">
          <a href="#" @click.prevent="tryAgain">Попробовать другую почту?</a>
        </p>
      </div>
    </template>

    <!-- Email form -->
    <template v-else>
      <lightbox-title>Восстановление доступа</lightbox-title>

      <div class="form-field">
        <div class="form-field-label">
          <label for="recovery-email">Почта</label>
          <a href="#" @click.prevent="goToSupport" class="field-action">Нет доступа к почте?</a>
        </div>
        <input
          v-model="emailField.value.value"
          id="recovery-email"
          type="email"
          autocomplete="email"
          @blur="emailField.onBlur"
          @input="emailField.onInput"
          @keydown.enter.prevent="submit"
        />
      </div>

      <div v-if="emailField.error.value" class="error-message">
        {{ emailField.error.value }}
      </div>

      <div v-else-if="serverError" class="error-message">
        {{ serverError }}
      </div>

      <div class="controls">
        <the-button @click="submit" :loading="loading" :disabled="!emailField.isReady.value">Отправить</the-button>
        <the-button @click="emit('cancel')" secondary>Отмена</the-button>
      </div>
    </template>
  </the-lightbox>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.form-field
  margin-bottom: $small

.form-field-label
  display: flex
  justify-content: space-between
  align-items: center
  margin-bottom: $minor
  font-size: $secondary-font-size

  label
    color: $text-muted

.field-action
  font-size: $secondary-font-size

input
  width: 100%
  padding: $minor $small
  border: 1px solid $border
  background: $bg-element
  color: $text
  box-sizing: border-box

  &:focus
    outline: none
    border-color: $link

.error-message
  color: $accent-red
  margin-top: $small
  font-size: $secondary-font-size

.success-content
  text-align: center

.main-text
  margin: 0 0 $small
  line-height: 1.5

.expiry-note
  margin: 0
  color: $text-muted
  font-size: $secondary-font-size

  a
    color: $link

.controls
  display: flex
  gap: $small
  margin: $medium (-$medium) (-$medium)
  padding: $medium
  background-color: $bg-element-accent
  border-radius: 0 0 $border-radius $border-radius
</style>

<style lang="sass">
.lightbox:has(.success-content)
  width: auto !important
  max-width: 320px !important
</style>

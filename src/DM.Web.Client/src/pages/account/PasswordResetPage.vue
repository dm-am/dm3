<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useNewPasswordField } from "@/shared/lib/composables/useNewPasswordField";
import { AccountApi } from "@/shared/api";
import Button from "@/shared/ui/Button/Button.vue";
import LightboxTitle from "@/shared/ui/Layout/LightboxTitle.vue";
import {
  PasswordInput,
  PasswordStrengthIndicator,
} from "@/shared/ui/PasswordInput";
import StatusIcon from "@/shared/ui/Icon/StatusIcon.vue";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";

const route = useRoute();
const router = useRouter();

// Page state: loading -> ready/expired/invalid/completed
type PageState = "loading" | "ready" | "expired" | "invalid" | "completed";
const pageState = ref<PageState>("loading");
const submitting = ref(false);

// Password field with HIBP check
const {
  password: newPassword,
  hibpStatus,
  isValid,
  onInput: onPasswordInput,
  onBlur: onPasswordBlur,
} = useNewPasswordField();

// Check token on mount
onMounted(async () => {
  const token = route.params.token as string;
  const { data, error } = await AccountApi.getPasswordResetTokenInfo(token);

  if (error || !data) {
    pageState.value = "invalid";
    return;
  }

  if (data.status === "expired") {
    pageState.value = "expired";
  } else {
    pageState.value = "ready";
  }
});

const passwordError = ref("");

const submit = async () => {
  if (!isValid.value) return;

  submitting.value = true;
  passwordError.value = "";

  const token = route.params.token as string;
  const { data, error } = await AccountApi.completePasswordReset(
    token,
    newPassword.value,
  );

  submitting.value = false;

  if (error) {
    const errors = parseApiErrors(error);
    const passErr = getFieldError(errors, "newPassword");

    if (passErr) {
      passwordError.value = passErr;
      return;
    }

    if (errors["token"] || errors["general"]) {
      pageState.value = "expired";
      return;
    }

    pageState.value = "invalid";
    return;
  }

  if (data) {
    pageState.value = "completed";
  }
};

function goHome() {
  router.push("/");
}

function goToRecovery() {
  router.push("/?action=login&recovery=true");
}

function goToLogin() {
  router.push("/?action=login");
}
</script>

<template>
  <div
    class="reset-page"
    :class="{ 'reset-page--wide': pageState === 'invalid' }"
  >
    <div class="reset-card">
      <!-- Success state -->
      <template v-if="pageState === 'completed'">
        <status-icon type="success" />
        <lightbox-title>Пароль изменен</lightbox-title>
        <p class="status-description">
          Теперь вы можете войти с новым паролем.
        </p>
        <div class="status-actions">
          <Button @click="goHome">На главную</Button>
        </div>
      </template>

      <!-- Expired token -->
      <template v-else-if="pageState === 'expired'">
        <status-icon type="warning" />
        <lightbox-title>Токен сброса пароля устарел</lightbox-title>
        <p class="status-description">
          Срок действия токена истек.
          <a href="#" @click.prevent="goToRecovery">Запросите новый</a>.
        </p>
      </template>

      <!-- Invalid token -->
      <template v-else-if="pageState === 'invalid'">
        <status-icon type="error" />
        <lightbox-title>Токен сброса пароля недействителен</lightbox-title>

        <div class="error-info">
          <p><strong>Возможные причины:</strong></p>
          <ul>
            <li>
              Пароль уже был изменен — попробуйте
              <a href="#" @click.prevent="goToLogin">войти</a>
            </li>
            <li>Был запрошен новый токен — проверьте последнее письмо</li>
          </ul>
        </div>
      </template>

      <!-- Form (ready state) -->
      <template v-if="pageState === 'ready'">
        <lightbox-title>Новый пароль</lightbox-title>

        <form @submit.prevent="submit" class="reset-form">
          <div class="form-field">
            <span class="field-hint">Минимум 8 символов</span>
            <password-input
              v-model="newPassword"
              placeholder="Введите новый пароль"
              autocomplete="new-password"
              :disabled="submitting"
              @input="onPasswordInput"
              @blur="onPasswordBlur"
            />
            <PasswordStrengthIndicator
              :password="newPassword"
              :hibp-status="hibpStatus"
            />
            <div v-if="passwordError" class="field-error">
              {{ passwordError }}
            </div>
          </div>

          <Button
            type="submit"
            :loading="submitting"
            :disabled="!isValid"
            class="submit-button"
          >
            Сохранить
          </Button>
        </form>
      </template>

      <div class="help-section">
        Нужна помощь? Обратитесь в
        <a href="/support?reason=access">поддержку</a>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.reset-page
  max-width: 380px
  margin: $major auto
  padding: 0 $medium

  &--wide
    max-width: 480px

.reset-card
  text-align: center
  background: $bg-element
  border-radius: $border-radius
  padding: $big
  box-shadow: 0 2px 12px $shadow-color

.status-description
  margin: 0 0 $medium
  font-size: $font-size
  line-height: 1.6
  color: $text

.status-actions
  margin-top: $big

  :deep(.button)
    min-width: 200px

// Form
.reset-form
  display: flex
  flex-direction: column
  gap: $small

.form-field
  text-align: left

.field-hint
  display: block
  margin-bottom: $tiny
  font-size: $secondary-font-size
  color: $text-muted

:deep(.password-input input)
  width: 100%
  padding: $minor $small
  border: 1px solid $border
  background: $bg-element
  color: $text
  box-sizing: border-box

  &:focus
    outline: none
    border-color: $link

  &:disabled
    opacity: 0.6

.field-error
  margin-top: $tiny
  font-size: $secondary-font-size
  color: $accent-red

.submit-button
  margin-top: $medium

// Error info box
.error-info
  margin: $medium 0
  padding: $small $medium
  border: 1px solid $border-accent-red
  border-radius: $border-radius
  font-size: $secondary-font-size
  line-height: 1.5
  text-align: left

  p
    margin: $minor 0

    &:first-child
      margin-top: 0

  ul
    margin: $minor 0 0
    padding-left: $medium

    li
      margin: $minor 0

  a
    font-weight: bold

// Help section
.help-section
  margin-top: $big
  padding-top: $medium
  border-top: 1px solid $border
  font-size: $secondary-font-size
  color: $text-muted

  a
    font-weight: bold
</style>

<script setup lang="ts">
import { ref, onMounted } from "vue";
import { useRouter } from "vue-router";
import { readConfirmationToken } from "@/shared/lib/utils/confirmationToken";
import { useNewPasswordField } from "@/shared/lib/composables/useNewPasswordField";
import { accountApi } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import {
  PasswordInput,
  PasswordStrengthIndicator,
} from "@/shared/ui/PasswordInput";
import StatusIcon from "@/shared/ui/Icon/StatusIcon.vue";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";

const router = useRouter();

// Page state: loading -> ready/expired/invalid/completed
type PageState = "loading" | "ready" | "expired" | "invalid" | "completed";
const pageState = ref<PageState>("loading");
const submitting = ref(false);

// Password field with HIBP check
const { password: newPassword, isValid } = useNewPasswordField();

// The value arrives in the fragment and is cleared as it is read, so it is read
// once and kept: in the path it went into browser history and into the access log
// of the edge.
const token = ref("");

onMounted(async () => {
  token.value = readConfirmationToken();
  const { data, error } = await accountApi.getPasswordResetTokenInfo(
    token.value,
  );

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

  const { data, error } = await accountApi.completePasswordReset(
    token.value,
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
</script>

<template>
  <div
    class="reset-page"
    :class="{ 'reset-page--wide': pageState === 'invalid' }"
  >
    <div class="reset-card">
      <!-- Checking the link — see AccountActivationPage: the state existed and
           the template had no branch for it, so the page from the letter was a
           blank card with a link to support under it. -->
      <div v-if="pageState === 'loading'" class="reset-loading">
        <p class="loading-text">Проверяем ссылку...</p>
      </div>

      <!-- Success state -->
      <template v-else-if="pageState === 'completed'">
        <status-icon type="success" />
        <dialog-title>Пароль изменен</dialog-title>
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
        <dialog-title>Токен сброса пароля устарел</dialog-title>
        <p class="status-description">
          Срок действия токена истек.
          <router-link :to="{ path: '/', query: { action: 'recovery' } }"
            >Запросите новый</router-link
          >.
        </p>
      </template>

      <!-- Invalid token -->
      <template v-else-if="pageState === 'invalid'">
        <status-icon type="error" />
        <dialog-title>Токен сброса пароля недействителен</dialog-title>

        <div class="error-info">
          <p><strong>Возможные причины:</strong></p>
          <ul>
            <li>
              Пароль уже был изменен, попробуйте
              <router-link :to="{ path: '/', query: { action: 'login' } }"
                >войти</router-link
              >
            </li>
            <li>Был запрошен новый токен, проверьте последнее письмо</li>
          </ul>
        </div>
      </template>

      <!-- Form (ready state) -->
      <template v-else-if="pageState === 'ready'">
        <dialog-title>Новый пароль</dialog-title>

        <form @submit.prevent="submit" class="reset-form">
          <div class="form-field">
            <span class="field-hint">Минимум 8 символов</span>
            <password-input
              v-model="newPassword"
              placeholder="Введите новый пароль"
              autocomplete="new-password"
              :disabled="submitting"
            />
            <PasswordStrengthIndicator :password="newPassword" />
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
        <router-link to="/support?reason=access">поддержку</router-link>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
.reset-page
  max-width: 380px
  margin: $major auto
  padding: 0 $medium

  &--wide
    max-width: 480px

.reset-loading
  padding: $big 0

.loading-text
  color: $text-muted

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

  &:focus:not(:focus-visible)
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

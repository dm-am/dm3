<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useToast } from "@/composables/useToast";
import { useUserStore } from "@/stores";
import accountApi from "@/api/requests/accountApi";
import type { User } from "@/api/models/community";
import TheButton from "@/components/inputs/TheButton.vue";
import LoginInput from "@/components/inputs/LoginInput.vue";
import LightboxTitle from "@/components/layout/LightboxTitle.vue";

// State machine for activation flow
type ActivationPhase =
  | "loading"
  | "selectLogin"
  | "confirmLogin"
  | "expired"
  | "notFound"
  | "submitting"
  | "success";

const route = useRoute();
const router = useRouter();
const toast = useToast();
const userStore = useUserStore();

// State
const phase = ref<ActivationPhase>("loading");
const pendingEmail = ref<string>("");
const token = ref<string>("");
const login = ref("");
const loginAvailable = ref(false);
const activatedUser = ref<User | null>(null);
const error = ref<string | null>(null);

// Resend state
const resendEmail = ref("");
const resendLoading = ref(false);
const resendSuccess = ref(false);

// BroadcastChannel for cross-tab sync
let channel: BroadcastChannel | null = null;

// Check if login is valid to proceed to confirmation
const canProceed = computed(() => {
  return (
    phase.value === "selectLogin" &&
    login.value.length >= 2 &&
    login.value.length <= 20 &&
    loginAvailable.value
  );
});

// Check if ready to submit (on confirmation step)
const canSubmit = computed(() => {
  return phase.value === "confirmLogin";
});

onMounted(async () => {
  token.value = route.params.token as string;

  if (!token.value) {
    phase.value = "notFound";
    error.value = "Токен активации отсутствует";
    return;
  }

  // Setup BroadcastChannel for cross-tab sync
  if (typeof BroadcastChannel !== "undefined") {
    channel = new BroadcastChannel("dm_activation");
    channel.onmessage = (event) => {
      if (event.data.type === "activated") {
        toast.info("Аккаунт был активирован в другой вкладке");
        router.push("/");
      }
    };
  }

  // Store token in sessionStorage for retry
  sessionStorage.setItem("dm_activation_token", token.value);

  // Try to get saved email from registration
  const savedEmail = sessionStorage.getItem("dm_pending_email");
  if (savedEmail) {
    resendEmail.value = savedEmail;
  }

  // Check token status
  await checkTokenStatus();
});

onUnmounted(() => {
  channel?.close();
});

async function checkTokenStatus() {
  phase.value = "loading";

  const { data, error: apiError } = await accountApi.getActivationInfo(token.value);

  if (apiError) {
    // 404 - token not found
    phase.value = "notFound";
    return;
  }

  if (data) {
    pendingEmail.value = data.email;
    resendEmail.value = data.email;

    if (data.status === "ready") {
      phase.value = "selectLogin";
    } else if (data.status === "expired") {
      phase.value = "expired";
    }
  }
}

function proceedToConfirm() {
  if (!canProceed.value) return;
  phase.value = "confirmLogin";
}

function goBackToSelect() {
  phase.value = "selectLogin";
}

async function submitActivation() {
  if (!canSubmit.value) return;

  phase.value = "submitting";

  const { data, error: apiError } = await accountApi.activate({
    token: token.value,
    login: login.value,
    expectedEmail: pendingEmail.value,
  });

  if (apiError) {
    // Handle specific errors
    if ("invalidProperties" in apiError || "errors" in apiError) {
      const err = apiError as { invalidProperties?: Record<string, string[]>; errors?: Record<string, string[]> };
      const props = err.invalidProperties ?? err.errors ?? {};

      // Login errors - go back to selection
      if (props["login"] || props["Login"]) {
        error.value = props["login"]?.[0] || props["Login"]?.[0] || "Ошибка имени пользователя";
        phase.value = "selectLogin";
        loginAvailable.value = false;
        return;
      }

      // Token errors - check if already activated
      error.value = Object.values(props).flat()[0] || "Не удалось активировать аккаунт";
    } else {
      error.value = "Не удалось активировать аккаунт";
    }

    // Check if it might be 410 Gone (expired)
    if (apiError.status === 410) {
      phase.value = "expired";
    } else {
      phase.value = "notFound";
    }
    return;
  }

  if (data?.resource) {
    // Success!
    userStore.updateUser(data.resource);
    activatedUser.value = data.resource;
    phase.value = "success";

    // Notify other tabs
    channel?.postMessage({ type: "activated", login: data.resource.login });

    // Clean up sessionStorage
    sessionStorage.removeItem("dm_pending_email");
    sessionStorage.removeItem("dm_activation_token");
  }
}

async function resend() {
  if (!resendEmail.value) return;

  resendLoading.value = true;

  try {
    await accountApi.resendActivation(resendEmail.value);
    resendSuccess.value = true;
  } catch {
    toast.error("Не удалось отправить письмо");
  } finally {
    resendLoading.value = false;
  }
}

function goHome() {
  router.push("/");
}

function goToProfile() {
  if (activatedUser.value?.login) {
    router.push(`/users/${activatedUser.value.login}`);
  } else {
    router.push("/");
  }
}

function goToLogin() {
  router.push("/?action=login");
}

function goToRegister() {
  router.push("/?action=register");
}
</script>

<template>
  <div class="activation-page" :class="{ 'activation-page--wide': phase === 'notFound' }">
    <div class="activation-card">
      <!-- Step 1: Login Selection -->
      <template v-if="phase === 'selectLogin'">
        <lightbox-title>Выберите имя</lightbox-title>

        <div class="login-form">
          <label class="field-label">Имя пользователя</label>
          <login-input
            v-model="login"
            @availability="loginAvailable = $event"
          />

          <p v-if="error" class="login-error">{{ error }}</p>

          <the-button
            @click="proceedToConfirm"
            :disabled="!canProceed"
            class="submit-button"
          >
            Продолжить
          </the-button>
        </div>
      </template>

      <!-- Step 2: Confirm Login -->
      <template v-if="phase === 'confirmLogin' || phase === 'submitting'">
        <lightbox-title>Подтвердите выбор имени</lightbox-title>

        <div class="confirm-form">
          <div class="login-warning">
            <p><strong>Будьте внимательны</strong></p>
            <p>Дальнейшая смена имени возможна только в исключительных случаях</p>
          </div>

          <div class="login-display">
            <div class="login-display__label">
              <span>Имя пользователя</span>
              <a href="#" @click.prevent="goBackToSelect" :class="{ disabled: phase === 'submitting' }">Изменить</a>
            </div>
            <div class="login-display__value">{{ login }}</div>
          </div>

          <p v-if="error" class="login-error">{{ error }}</p>

          <the-button
            @click="submitActivation"
            :disabled="!canSubmit"
            :loading="phase === 'submitting'"
            class="submit-button"
          >
            Завершить регистрацию
          </the-button>
        </div>
      </template>

      <!-- Success state -->
      <template v-else-if="phase === 'success'">
        <div class="status-icon status-icon--success">
          <svg viewBox="0 0 24 24" fill="none">
            <polyline
              points="4 12 10 18 20 6"
              stroke="currentColor"
              stroke-width="2.5"
              stroke-linecap="round"
              stroke-linejoin="round"
            />
          </svg>
        </div>
        <lightbox-title>Регистрация завершена</lightbox-title>
        <p class="status-description">
          Добро пожаловать, <strong>{{ activatedUser?.login }}</strong>!
        </p>
        <div class="status-actions">
          <the-button @click="goToProfile">Перейти в профиль</the-button>
        </div>
      </template>

      <!-- Expired state -->
      <template v-else-if="phase === 'expired'">
        <div class="status-icon status-icon--warning">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10" />
            <line x1="12" y1="8" x2="12" y2="12" stroke-linecap="round" />
            <line x1="12" y1="16" x2="12.01" y2="16" stroke-linecap="round" />
          </svg>
        </div>
        <lightbox-title>Токен устарел</lightbox-title>

        <div v-if="!resendSuccess" class="expired-action">
          <p class="main-text">Отправить новую ссылку на <strong>{{ pendingEmail }}</strong>?</p>
          <p class="expiry-note">Ссылка действительна 48 часов</p>
          <the-button @click="resend" :loading="resendLoading">
            Отправить
          </the-button>
        </div>

        <div v-else class="expired-action">
          <p class="main-text">Письмо отправлено на <strong>{{ pendingEmail }}</strong></p>
        </div>
      </template>

      <!-- Not Found state -->
      <template v-else-if="phase === 'notFound'">
        <div class="status-icon status-icon--error">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="18" y1="6" x2="6" y2="18" stroke-linecap="round" />
            <line x1="6" y1="6" x2="18" y2="18" stroke-linecap="round" />
          </svg>
        </div>
        <lightbox-title>Токен недействителен</lightbox-title>

        <div class="error-info">
          <p v-if="error"><strong>{{ error }}</strong></p>
          <p><strong>Возможные причины:</strong></p>
          <ul>
            <li>Аккаунт уже активирован — попробуйте <a href="#" @click.prevent="goToLogin">войти</a></li>
            <li>Был запрошен новый токен — проверьте последнее письмо</li>
            <li>Регистрация устарела — <a href="#" @click.prevent="goToRegister">зарегистрируйтесь</a> заново</li>
          </ul>
        </div>
      </template>

      <div class="help-section">
        Нужна помощь? Обратитесь в <a href="/support?reason=access">поддержку</a>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.activation-page
  max-width: 380px
  margin: $major auto
  padding: 0 $medium

  &--wide
    max-width: 480px

.activation-card
  text-align: center
  background: $bg-element
  border-radius: $border-radius
  padding: $big
  box-shadow: 0 2px 12px $shadow-color

// Status icons
.status-icon
  width: 48px
  height: 48px
  margin: 0 auto $medium
  display: flex
  align-items: center
  justify-content: center

  svg
    width: 100%
    height: 100%

.status-icon--success
  color: $accent-green
  animation: success-entrance 0.4s ease-out

  svg
    filter: drop-shadow(0 0 8px var(--accent-green-muted))

  svg polyline
    stroke-dasharray: 30
    stroke-dashoffset: 30
    animation: checkmark-draw 0.5s ease-out 0.2s forwards

.status-icon--warning
  color: $text-muted

.status-icon--error
  color: $accent-red

// Animations
@keyframes success-entrance
  0%
    opacity: 0
    transform: scale(0.9) translateY(8px)
  100%
    opacity: 1
    transform: scale(1) translateY(0)

@keyframes checkmark-draw
  0%
    stroke-dashoffset: 30
  100%
    stroke-dashoffset: 0

.status-description
  margin: 0 0 $medium
  font-size: $font-size
  line-height: 1.6
  color: $text

// Expired state
.expired-action
  .main-text
    margin: 0 0 $small
    line-height: 1.5

  .expiry-note
    margin: 0 0 $medium
    font-size: $secondary-font-size
    color: $text-muted

// Login selection
.login-display
  display: flex
  flex-direction: column
  margin: $small 0
  gap: $minor

  &__label
    display: flex
    justify-content: space-between
    align-items: center
    font-size: $secondary-font-size

    span
      color: $text-muted

    a.disabled
      pointer-events: none
      opacity: 0.5

  &__value
    padding: ($minor + 1px) $small
    background-color: $overlay-subtle
    border: 1px solid $border
    color: $text
    cursor: default
    word-break: break-all
    width: 100%
    box-sizing: border-box
    text-align: center

.login-warning
  margin-bottom: $small
  padding: $small $medium
  border: 1px solid $border-accent-red
  border-radius: $border-radius
  font-size: $secondary-font-size
  line-height: 1.5

  p
    margin: $minor 0
    &:first-child
      margin-top: 0
    &:last-child
      margin-bottom: 0

.field-label
  display: block
  text-align: left
  color: $text-muted
  font-size: $secondary-font-size
  margin: $small 0 $minor

.login-form,
.confirm-form
  display: flex
  flex-direction: column

.login-form
  :deep(.login-input)
    width: 100%

    input
      width: 100%
      box-sizing: border-box
      text-align: center
      padding-left: 2.5rem  // balance icon offset

.login-error
  margin: $small 0 0
  color: $accent-red
  font-size: $secondary-font-size

.submit-button
  margin-top: $medium

// Error info box (styled like registration-info)
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

// Resend section
.resend-section
  margin-top: $big
  padding-top: $big
  border-top: 1px solid $border

.resend-prompt
  text-align: center
  margin-bottom: $medium
  color: $text
  font-size: $secondary-font-size

.resend-actions
  display: flex
  gap: $small
  justify-content: center
  margin-top: $medium

.resend-success
  margin-top: $big
  padding: $medium
  background: $bg-highlight-green
  border-radius: $border-radius
  text-align: center

  p
    margin: 0 0 $small
    color: $text

  .resend-hint
    color: $text-muted
    font-size: $secondary-font-size
    margin-bottom: $medium

// Actions
.status-actions
  margin-top: $big

  :deep(.the-button)
    min-width: 200px

// Help section
.help-section
  margin-top: $big
  padding-top: $medium
  border-top: 1px solid $border
  font-size: $secondary-font-size
  color: $text-muted

  a
    font-weight: bold

// Mobile adjustments
@media (max-width: 480px)
  .status-icon
    width: 36px
    height: 36px

  .login-form
    :deep(.the-button)
      width: 100%
</style>

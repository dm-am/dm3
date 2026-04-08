<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useToast } from "@/shared/lib/composables/useToast";
import { useUserStore } from "@/entities/user";
import { AccountApi } from "@/shared/api";
import type { User } from "@/shared/api/models/community";
import Button from "@/shared/ui/Button/Button.vue";
import { UsernameInput } from "@/shared/ui/UsernameInput";
import LightboxTitle from "@/shared/ui/Layout/LightboxTitle.vue";
import StatusIcon from "@/shared/ui/Icon/StatusIcon.vue";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";

// State machine for activation flow
type ActivationPhase =
  | "loading"
  | "selectUsername"
  | "confirmUsername"
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
const username = ref("");
const usernameAvailable = ref(false);
const activatedUser = ref<User | null>(null);
const error = ref<string | null>(null);

// Resend state
const resendEmail = ref("");
const resendLoading = ref(false);
const resendSuccess = ref(false);

// BroadcastChannel for cross-tab sync
let channel: BroadcastChannel | null = null;

// Check if username is valid to proceed to confirmation
const canProceed = computed(() => {
  return (
    phase.value === "selectUsername" &&
    username.value.length >= 2 &&
    username.value.length <= 20 &&
    usernameAvailable.value
  );
});

// Check if ready to submit (on confirmation step)
const canSubmit = computed(() => {
  return phase.value === "confirmUsername";
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

  const { data, error: apiError } = await AccountApi.getActivationInfo(
    token.value,
  );

  if (apiError) {
    // 404 - token not found
    phase.value = "notFound";
    return;
  }

  if (data) {
    pendingEmail.value = data.email;
    resendEmail.value = data.email;

    if (data.status === "ready") {
      phase.value = "selectUsername";
    } else if (data.status === "expired") {
      phase.value = "expired";
    }
  }
}

function proceedToConfirm() {
  if (!canProceed.value) return;
  phase.value = "confirmUsername";
}

function goBackToSelect() {
  phase.value = "selectUsername";
}

async function submitActivation() {
  if (!canSubmit.value) return;

  phase.value = "submitting";

  const { data, error: apiError } = await AccountApi.activate(token.value, {
    username: username.value,
    expectedEmail: pendingEmail.value,
  });

  if (apiError) {
    const errors = parseApiErrors(apiError);
    const usernameError = getFieldError(errors, "username");

    // Username errors - go back to selection
    if (usernameError) {
      error.value = usernameError;
      phase.value = "selectUsername";
      usernameAvailable.value = false;
      return;
    }

    // Token errors or other errors
    const firstError = Object.values(errors).flat()[0];
    error.value = firstError || "Не удалось активировать аккаунт";

    // Check if it might be 410 Gone (expired)
    if (apiError.status === 410) {
      phase.value = "expired";
    } else {
      phase.value = "notFound";
    }
    return;
  }

  if (data) {
    // Success!
    userStore.updateUser(data);
    activatedUser.value = data;
    phase.value = "success";

    // Notify other tabs
    channel?.postMessage({ type: "activated", username: data.username });

    // Clean up sessionStorage
    sessionStorage.removeItem("dm_pending_email");
    sessionStorage.removeItem("dm_activation_token");
  }
}

async function resend() {
  if (!resendEmail.value) return;

  resendLoading.value = true;

  try {
    await AccountApi.recover(resendEmail.value);
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
  if (activatedUser.value?.username) {
    router.push(`/users/${activatedUser.value.username}`);
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
  <div
    class="activation-page"
    :class="{ 'activation-page--wide': phase === 'notFound' }"
  >
    <div class="activation-card">
      <!-- Step 1: Username Selection -->
      <template v-if="phase === 'selectUsername'">
        <lightbox-title>Выберите имя</lightbox-title>

        <div class="username-form">
          <label class="field-label">Имя пользователя</label>
          <username-input
            v-model="username"
            @availability="usernameAvailable = $event"
          />

          <p v-if="error" class="username-error">{{ error }}</p>

          <Button
            @click="proceedToConfirm"
            :disabled="!canProceed"
            class="submit-button"
          >
            Продолжить
          </Button>
        </div>
      </template>

      <!-- Step 2: Confirm Username -->
      <template v-if="phase === 'confirmUsername' || phase === 'submitting'">
        <lightbox-title>Подтвердите выбор имени</lightbox-title>

        <div class="confirm-form">
          <div class="username-warning">
            <p><strong>Будьте внимательны</strong></p>
            <p>
              Дальнейшая смена имени возможна только в исключительных случаях
            </p>
          </div>

          <div class="username-display">
            <div class="username-display__label">
              <span>Имя пользователя</span>
              <a
                href="#"
                @click.prevent="goBackToSelect"
                :class="{ disabled: phase === 'submitting' }"
                >Изменить</a
              >
            </div>
            <div class="username-display__value">{{ username }}</div>
          </div>

          <p v-if="error" class="username-error">{{ error }}</p>

          <Button
            @click="submitActivation"
            :disabled="!canSubmit"
            :loading="phase === 'submitting'"
            class="submit-button"
          >
            Завершить регистрацию
          </Button>
        </div>
      </template>

      <!-- Success state -->
      <template v-else-if="phase === 'success'">
        <status-icon type="success" />
        <lightbox-title>Регистрация завершена</lightbox-title>
        <p class="status-description">
          Добро пожаловать, <strong>{{ activatedUser?.username }}</strong
          >!
        </p>
        <div class="status-actions">
          <Button @click="goToProfile">Перейти в профиль</Button>
        </div>
      </template>

      <!-- Expired state -->
      <template v-else-if="phase === 'expired'">
        <status-icon type="warning" />
        <lightbox-title>Токен регистрации устарел</lightbox-title>

        <div v-if="!resendSuccess" class="expired-action">
          <p class="main-text">
            Отправить новую ссылку на <strong>{{ pendingEmail }}</strong
            >?
          </p>
          <p class="expiry-note">Ссылка действительна 48 часов</p>
          <Button @click="resend" :loading="resendLoading"> Отправить </Button>
        </div>

        <div v-else class="expired-action">
          <p class="main-text">
            Письмо отправлено на <strong>{{ pendingEmail }}</strong>
          </p>
        </div>
      </template>

      <!-- Not Found state -->
      <template v-else-if="phase === 'notFound'">
        <status-icon type="error" />
        <lightbox-title>Токен регистрации недействителен</lightbox-title>

        <div class="error-info">
          <p v-if="error">
            <strong>{{ error }}</strong>
          </p>
          <p><strong>Возможные причины:</strong></p>
          <ul>
            <li>
              Аккаунт уже активирован — попробуйте
              <a href="#" @click.prevent="goToLogin">войти</a>
            </li>
            <li>Был запрошен новый токен — проверьте последнее письмо</li>
            <li>
              Регистрация устарела —
              <a href="#" @click.prevent="goToRegister">зарегистрируйтесь</a>
              заново
            </li>
          </ul>
        </div>
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

// Username selection
.username-display
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

.username-warning
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

.username-form,
.confirm-form
  display: flex
  flex-direction: column

.username-form
  :deep(.username-input)
    width: 100%

    input
      width: 100%
      box-sizing: border-box
      text-align: center
      padding-left: 2.5rem  // balance icon offset

.username-error
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

  :deep(.button)
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
  .username-form
    :deep(.button)
      width: 100%
</style>

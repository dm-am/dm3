<script setup lang="ts">
import {
  ref,
  computed,
  watch,
  onMounted,
  onBeforeUnmount,
  nextTick,
} from "vue";
import type {
  MailedLink,
  RegisterCredentials,
} from "@/shared/api/models/account";
import { unwrapResource } from "@/shared/api/envelope";

import { useNewPasswordField } from "@/shared/lib/composables/useNewPasswordField";
import { AuthDialog } from "@/shared/ui/Layout";
import { Button } from "@/shared/ui/Button";
import {
  PasswordInput,
  PasswordStrengthIndicator,
} from "@/shared/ui/PasswordInput";
import { Tooltip } from "@/shared/ui/Tooltip";
import HoneypotField from "@/shared/ui/Form/HoneypotField.vue";
import { accountApi } from "@/entities/user";
import { parseApiErrors } from "@/shared/lib/utils/apiErrors";
import { readFieldError } from "@/shared/lib/errors/validationErrors";
import { refusalHere } from "@/shared/lib/errors";
import { useRateLimitNotice } from "@/shared/lib/composables/useRateLimitNotice";
import { useBotTrap } from "@/shared/lib/composables/useBotTrap";
import { supportFor } from "@/entities/ticket";
import {
  EMAIL_MAX_LENGTH,
  useRegistrationEmail,
} from "../lib/useRegistrationEmail";
import { useActivationResend } from "../lib/useActivationResend";
import { ACTIVATION_RESENT } from "../lib/linkLifetime";
import { useSupportExit } from "../lib/useSupportExit";
import LetterReceipt from "./LetterReceipt.vue";

const emit = defineEmits<{
  /** The address the letter went to, and how long its link lives. */
  (e: "success", email: string, hours?: number): void;
  (e: "cancel"): void;
  (e: "openRecovery", email?: string): void;
  (e: "openLogin", email?: string): void;
}>();

const step = ref<"email" | "password">("email");
const loading = ref(false);
const { honeypot, sitOut } = useBotTrap({ minFillMs: 2000 });
const emailInputRef = ref<HTMLInputElement | null>(null);
const passwordInputRef = ref<InstanceType<typeof PasswordInput> | null>(null);
// The rules unlock once opened; the agreement and the policy are accepted by registering.
const rulesViewed = ref(false);
const acceptedRules = ref(false);

// One notice for both steps: the address check and the registration share a budget.
const rateLimit = useRateLimitNotice();

const {
  field: emailField,
  taken: emailTaken,
  pending: emailPending,
  waitForCheck,
  onBlur: onEmailBlur,
  onInput: onEmailInput,
  refusalOf: emailRefusalOf,
  reset: resetEmail,
} = useRegistrationEmail(rateLimit);

const address = computed(() => emailField.value.value.trim());

const {
  password: newPassword,
  isValid: isPasswordValid,
  reset: resetPassword,
} = useNewPasswordField();

// Stands in the strength line and holds the button shut until the password changes.
const passwordRefusal = ref("");

// A failure that named no field takes the strength line too, and turns the bar red.
const formError = ref("");

// Live as soon as the text looks like an address: whether it is free is asked on the press.
const canSubmitEmail = computed(
  () =>
    emailField.isValid.value &&
    !emailField.error.value &&
    !emailTaken.value &&
    !emailPending.value,
);

// The rate limit wins both lines: it is the newest answer and its number keeps moving.
const emailMessage = computed(
  () => rateLimit.message.value || emailField.error.value,
);

const formMessage = computed(() => rateLimit.message.value || formError.value);

const canSubmitPassword = computed(
  () => isPasswordValid.value && !passwordRefusal.value && acceptedRules.value,
);

// A refusal is about the password that earned it.
watch(newPassword, () => {
  passwordRefusal.value = "";
  formError.value = "";
});

onMounted(() => {
  nextTick(() => emailInputRef.value?.focus());
});

onBeforeUnmount(() => {
  clearForm();
});

const toSupport = useSupportExit(() => emit("cancel"));
const goToSupport = () => toSupport(supportFor("AccessRecovery"));

const activationResent = ref(false);

const { resend, hours: resentHours } = useActivationResend(rateLimit);

const resendActivation = async () => {
  const outcome = await resend(address.value);
  if (outcome === "sent") activationResent.value = true;
  else if (outcome !== "waiting") emailField.setError(outcome.refusal);
};

const submitEmail = async () => {
  // Only a press on top of one being served is dropped: Enter and a held key reach here directly.
  if (loading.value || rateLimit.waiting.value) return;

  loading.value = true;
  try {
    // The click blurs the field, which starts the check: wait for it instead of asking again.
    await waitForCheck();

    if (
      emailField.error.value ||
      emailTaken.value ||
      emailPending.value ||
      rateLimit.waiting.value
    ) {
      return;
    }

    const isValid = await emailField.validate();
    // A limit met here refused the check, so nothing is known about the address yet.
    if (
      !isValid ||
      emailTaken.value ||
      emailPending.value ||
      rateLimit.waiting.value
    ) {
      return;
    }

    step.value = "password";
    nextTick(() => passwordInputRef.value?.focus());
  } finally {
    loading.value = false;
  }
};

const submitPassword = async () => {
  // Enter held or hit twice would send two letters or race two registrations.
  if (loading.value || !canSubmitPassword.value || rateLimit.waiting.value) {
    return;
  }

  loading.value = true;
  formError.value = "";
  passwordRefusal.value = "";

  // Waits out the minimum fill time instead of refusing: password managers submit within it.
  await sitOut();

  const credentials: RegisterCredentials = {
    email: address.value,
    password: newPassword.value,
    acceptedRules: acceptedRules.value,
    website: honeypot.value,
  };

  const { data, error: failure } = await accountApi.register(credentials);
  loading.value = false;

  if (!failure) {
    emit(
      "success",
      address.value,
      unwrapResource<MailedLink>(data)?.linkLifetimeHours,
    );
    return;
  }

  // The next screen promises a letter, so every refusal has to stop here.
  if (rateLimit.take(failure)) return;

  const errors = parseApiErrors(failure);
  const emailErr = emailRefusalOf(errors);
  const passErr = readFieldError(errors, "password");

  if (emailErr) {
    emailField.setError(emailErr);
    step.value = "email";
    nextTick(() => emailInputRef.value?.focus());
    return;
  }

  if (passErr) {
    passwordRefusal.value = passErr;
    return;
  }

  const said = refusalHere(
    failure,
    "Не удалось выполнить запрос, попробуйте позже",
  );
  if (said) formError.value = said;
};

const clearForm = () => {
  rateLimit.clear();
  resetEmail();
  resetPassword();
  passwordRefusal.value = "";
  formError.value = "";
  rulesViewed.value = false;
  acceptedRules.value = false;
  step.value = "email";
  // The modal keeps this component mounted: a receipt left here would greet the next visit.
  activationResent.value = false;
};

const cancel = () => {
  clearForm();
  emit("cancel");
};

const goBack = () => {
  step.value = "email";
  nextTick(() => emailInputRef.value?.focus());
};

const handleLogin = () => {
  emit("openLogin", address.value || undefined);
};

const handleRecovery = () => {
  emit("openRecovery", address.value || undefined);
};
</script>

<template>
  <!-- The receipt takes a narrower card: two lines and a way out. -->
  <AuthDialog
    :compact="activationResent"
    :center="activationResent"
    @close="cancel"
    @before-close="clearForm"
  >
    <LetterReceipt
      v-if="activationResent"
      :address="address"
      :hours="resentHours"
    >
      {{ ACTIVATION_RESENT }}
    </LetterReceipt>

    <template v-else>
      <h2 class="auth-title">Регистрация</h2>

      <form v-if="step === 'email'" novalidate @submit.prevent="submitEmail">
        <div class="auth-warnbox auth-warnbox--notice">
          <b>Создание дополнительных аккаунтов запрещено</b>
          Если вы утратили доступ к аккаунту, воспользуйтесь
          <button type="button" class="auth-action" @click="handleRecovery">
            восстановлением доступа
          </button>
          или обратитесь в
          <button type="button" class="auth-action" @click="goToSupport">
            поддержку
          </button>
        </div>

        <div class="auth-label-row">
          <label class="auth-label" for="registration-email">Почта</label>
          <!-- Only with the pair below: a lone label needs no copy space. -->
          <span v-if="emailTaken || emailPending" class="copy-space">{{
            " "
          }}</span>
          <button
            v-if="emailTaken"
            type="button"
            class="auth-action"
            @click="handleLogin"
          >
            Войти
          </button>
          <button
            v-else-if="emailPending"
            type="button"
            class="auth-action"
            @click="resendActivation"
          >
            Отправить письмо повторно
          </button>
        </div>
        <div class="auth-field">
          <!-- Enter goes straight to submitEmail, the one place that runs and waits for the check. -->
          <input
            id="registration-email"
            ref="emailInputRef"
            v-model="emailField.value.value"
            type="email"
            autocomplete="email"
            :maxlength="EMAIL_MAX_LENGTH"
            title=""
            aria-describedby="registration-email-message"
            :aria-invalid="emailField.error.value ? 'true' : undefined"
            @input="onEmailInput"
            @blur="onEmailBlur"
            @keydown.enter.prevent="submitEmail"
          />
        </div>
        <!-- Rendered empty too: the button below stays in place. -->
        <div
          id="registration-email-message"
          class="auth-message"
          :class="{ 'auth-message--error': !!emailMessage }"
          role="alert"
        >
          {{ emailMessage }}
        </div>

        <HoneypotField v-model="honeypot" />

        <Button
          class="auth-button"
          variant="primary"
          :disabled="!canSubmitEmail || rateLimit.waiting.value"
          :loading="loading"
        >
          Продолжить
        </Button>
      </form>

      <form v-else novalidate @submit.prevent="submitPassword">
        <div class="auth-label-row">
          <span class="auth-label">Почта</span>
          <span class="copy-space">{{ " " }}</span>
          <button type="button" class="auth-action" @click="goBack">
            Изменить
          </button>
        </div>
        <div class="auth-value">{{ emailField.value.value }}</div>

        <label class="auth-label" for="registration-password">Пароль</label>
        <div class="auth-field">
          <!-- No Enter handler: the form submits through the button, which may be disabled. -->
          <PasswordInput
            id="registration-password"
            ref="passwordInputRef"
            v-model="newPassword"
            autocomplete="new-password"
            aria-describedby="registration-password-message"
            :aria-invalid="passwordRefusal ? 'true' : undefined"
          />
        </div>
        <!-- The scale stands from the start: the minimum is written in its line. -->
        <PasswordStrengthIndicator
          id="registration-password-message"
          class="auth-meter"
          :password="newPassword"
          :error="passwordRefusal || formMessage || null"
          always
        />

        <!-- The tooltip covers the row: a disabled control swallows the pointer. -->
        <div class="consent">
          <Tooltip
            text="Сначала откройте правила сайта"
            :disabled="rulesViewed"
          >
            <div class="auth-checkline auth-checkline--top">
              <input
                id="registration-rules"
                v-model="acceptedRules"
                type="checkbox"
                :disabled="!rulesViewed"
              />
              <span>
                <label for="registration-rules"
                  >Я принимаю
                  <a
                    href="/rules"
                    target="_blank"
                    rel="noopener"
                    @click="rulesViewed = true"
                    >правила сайта</a
                  ></label
                >
              </span>
            </div>
          </Tooltip>
        </div>

        <HoneypotField v-model="honeypot" />

        <Button
          class="auth-button"
          variant="primary"
          :disabled="!canSubmitPassword || rateLimit.waiting.value"
          :loading="loading"
        >
          Создать аккаунт
        </Button>

        <p class="auth-footnote">
          Регистрируясь, вы принимаете
          <a href="/agreement" target="_blank" rel="noopener"
            >пользовательское соглашение</a
          >
          и
          <a href="/privacy" target="_blank" rel="noopener"
            >политику конфиденциальности</a
          >
        </p>
      </form>
    </template>
  </AuthDialog>
</template>

<style scoped lang="sass">
// The tooltip trigger is inline; the consent row needs the card's width.
.consent :deep(.tooltip-trigger)
  display: block

// A disabled control swallows mouse events: let the hover reach the tooltip.
.auth-checkline input[type="checkbox"]:disabled
  pointer-events: none
</style>

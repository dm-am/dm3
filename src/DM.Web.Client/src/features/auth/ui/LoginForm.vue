<script setup lang="ts">
import { signIn } from "@/entities/user";
import { ref, computed, nextTick, onMounted } from "vue";
import type { LoginCredentials } from "@/shared/api/models/account";
import { supportFor } from "@/entities/ticket";
import { AuthDialog } from "@/shared/ui/Layout";
import { Button } from "@/shared/ui/Button";
import { PasswordInput } from "@/shared/ui/PasswordInput";
import HoneypotField from "@/shared/ui/Form/HoneypotField.vue";
import {
  useValidatedField,
  validators,
} from "@/shared/lib/composables/useValidatedField";
import { parseApiErrors } from "@/shared/lib/utils/apiErrors";
import { readFieldError } from "@/shared/lib/errors/validationErrors";
import type { GeneralError } from "@/shared/api/models/common";
import { useRateLimitNotice } from "@/shared/lib/composables/useRateLimitNotice";
import { useBotTrap } from "@/shared/lib/composables/useBotTrap";
import {
  isLockout,
  isRemoved,
  readAccountRefusal,
  REGISTRATION_PENDING,
  sayLoginFailure,
} from "../lib/loginRefusal";
import { useActivationResend } from "../lib/useActivationResend";
import { ACTIVATION_RESENT } from "../lib/linkLifetime";
import { useSupportExit } from "../lib/useSupportExit";
import LetterReceipt from "./LetterReceipt.vue";
import LoginSecondFactor from "./LoginSecondFactor.vue";

const props = defineProps<{
  prefillEmail?: string;
}>();

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
  (e: "cantSignIn", email?: string): void;
  (e: "cantPassSecondFactor"): void;
  (e: "register"): void;
}>();

const pendingActivation = ref(false);

// Shown in the password strip: one place for every refusal of this form.
const accountRefusal = ref<string | null>(null);

// Kept beside the sentence, not parsed out of it: the ban's line has the same shape.
const removedAccount = ref(false);

const toSupport = useSupportExit(() => emit("cancel"));

// Offered only once the server says the account is gone: moderation alone restores it.
const goToRestoration = () => toSupport(supportFor("AccountRestoration"));

// One dialog for every step: no session exists between the factors to route to.
const step = ref<"credentials" | "secondFactor" | "activationResent">(
  "credentials",
);

// Shared by both steps: the limit is on the address.
const rateLimit = useRateLimitNotice();

const emailField = useValidatedField({
  initialValue: props.prefillEmail || "",
  // Checked here as on registration and recovery, so a typo is named under the field.
  validate: validators.combine(validators.required(), validators.email()),
});

const passwordField = useValidatedField({
  validate: validators.required(),
});

const { honeypot, sitOut } = useBotTrap({ minFillMs: 3000 });
const loading = ref(false);
/** The code of the second step is with the server: the window is locked for it too. */
const secondFactorBusy = ref(false);
const rememberMe = ref(true);

const emailInput = ref<HTMLInputElement | null>(null);
const passwordInput = ref<InstanceType<typeof PasswordInput> | null>(null);

const canSubmit = computed(
  () => !!emailField.value.value.trim() && !!passwordField.value.value.trim(),
);

// Newest answer first: the rate limit, then the account refusal, then the field.
const passwordMessage = computed(
  () =>
    rateLimit.message.value ||
    accountRefusal.value ||
    passwordField.error.value,
);

onMounted(async () => {
  await nextTick();
  // An address carried over from another form leaves the password to fill.
  if (emailField.value.value.trim()) passwordInput.value?.focus();
  else emailInput.value?.focus();
});

const submit = async () => {
  const emailValid = await emailField.validate();
  const passwordValid = await passwordField.validate();
  if (!emailValid || !passwordValid) return;

  loading.value = true;

  // Waits out the minimum fill time instead of refusing: password managers submit within it.
  await sitOut();

  pendingActivation.value = false;
  accountRefusal.value = null;
  removedAccount.value = false;

  const credentials: LoginCredentials = {
    email: emailField.value.value.trim(),
    password: passwordField.value.value,
    website: honeypot.value,
    rememberMe: rememberMe.value,
  };

  const outcome = await signIn(credentials);
  loading.value = false;

  if (outcome.stage === "signedIn") {
    emit("success");
    return;
  }

  if (outcome.stage === "secondFactor") {
    step.value = "secondFactor";
    return;
  }

  refuseCredentials(outcome.failure);
};

const refuseCredentials = (failure: GeneralError) => {
  // The lockout shares the rate-limit counter: one sentence, one set of rules.
  if (rateLimit.take(failure, isLockout(failure))) return;

  // Banned or removed: not about a field, so it goes to the password strip.
  const refusal = readAccountRefusal(failure);
  if (refusal) {
    accountRefusal.value = refusal;
    removedAccount.value = isRemoved(failure);
    return;
  }

  const errors = parseApiErrors(failure);

  if (errors["_pendingactivation"]) {
    pendingActivation.value = true;
    passwordField.setError(
      readFieldError(errors, "email") || REGISTRATION_PENDING,
    );
    return;
  }

  pendingActivation.value = false;
  const emailError = readFieldError(errors, "email");
  const passwordError = readFieldError(errors, "password");
  if (emailError) {
    emailField.setError(emailError);
  }
  if (passwordError) {
    passwordField.setError(passwordError);
  }

  // Nothing named a field: whatever else the server said goes under the password.
  const said = sayLoginFailure(failure);
  if (!emailError && !passwordError && said) {
    passwordField.setError(said);
  }
};

const {
  resend,
  resending,
  hours: resentHours,
} = useActivationResend(rateLimit);

const handleResendActivation = async () => {
  const outcome = await resend(emailField.value.value.trim());
  if (outcome === "sent") step.value = "activationResent";
  else if (outcome !== "waiting") passwordField.setError(outcome.refusal);
};

// At the start of the close, not at the unmount after the fade: the wait stops and the receipt goes.
const onBeforeClose = () => {
  rateLimit.clear();
  if (step.value === "activationResent") step.value = "credentials";
};

const onEmailInput = () => {
  emailField.onInput();
  // Clear password error too - credentials are validated as a pair
  passwordField.error.value = "";
  pendingActivation.value = false;
  // A refusal is about the account that was named, and the name is changing.
  accountRefusal.value = null;
  removedAccount.value = false;
};

const onPasswordInput = () => {
  passwordField.onInput();
  // Clear email error too - credentials are validated as a pair
  emailField.error.value = "";
  pendingActivation.value = false;
};
</script>

<template>
  <!-- The receipt takes a narrower card: two lines and a way out. -->
  <AuthDialog
    :compact="step === 'activationResent'"
    :center="step === 'activationResent'"
    :busy="loading || secondFactorBusy || resending"
    @close="emit('cancel')"
    @before-close="onBeforeClose"
  >
    <LetterReceipt
      v-if="step === 'activationResent'"
      :address="emailField.value.value.trim()"
      :hours="resentHours"
    >
      {{ ACTIVATION_RESENT }}
    </LetterReceipt>

    <LoginSecondFactor
      v-else-if="step === 'secondFactor'"
      :rate-limit="rateLimit"
      @busy="(busy: boolean) => (secondFactorBusy = busy)"
      @success="emit('success')"
      @restore="goToRestoration"
      @cant-pass-second-factor="emit('cantPassSecondFactor')"
    />

    <template v-else>
      <h2 class="auth-title">Войти</h2>

      <form @submit.prevent="submit">
        <label class="auth-label" for="email">Почта</label>
        <div class="auth-field">
          <input
            id="email"
            ref="emailInput"
            v-model="emailField.value.value"
            type="email"
            autocomplete="email"
            aria-describedby="login-email-message"
            :aria-invalid="emailField.error.value ? 'true' : undefined"
            @input="onEmailInput"
            @blur="emailField.onBlur"
          />
        </div>
        <!-- Rendered empty too: the strip holds the space when the server answers. -->
        <div
          id="login-email-message"
          class="auth-message"
          :class="{ 'auth-message--error': !!emailField.error.value }"
          role="alert"
        >
          {{ emailField.error.value }}
        </div>

        <div class="auth-label-row">
          <label class="auth-label" for="password">Пароль</label>
          <!-- One of the three buttons always renders, so the space is unconditional. -->
          <span class="copy-space">{{ " " }}</span>
          <button
            v-if="pendingActivation"
            type="button"
            class="auth-action"
            @click="handleResendActivation"
          >
            Отправить письмо повторно
          </button>
          <button
            v-else-if="removedAccount"
            type="button"
            class="auth-action"
            @click="goToRestoration"
          >
            Восстановить аккаунт
          </button>
          <button
            v-else
            type="button"
            class="auth-action"
            @click="emit('cantSignIn', emailField.value.value.trim())"
          >
            Не могу войти
          </button>
        </div>
        <div class="auth-field">
          <PasswordInput
            id="password"
            ref="passwordInput"
            v-model="passwordField.value.value"
            autocomplete="current-password"
            aria-describedby="login-password-message"
            :aria-invalid="passwordField.error.value ? 'true' : undefined"
            @input="onPasswordInput"
            @blur="passwordField.onBlur"
          />
        </div>
        <div
          id="login-password-message"
          class="auth-message"
          :class="{ 'auth-message--error': !!passwordMessage }"
          role="alert"
        >
          {{ passwordMessage }}
        </div>

        <div class="auth-checkline">
          <input id="rememberMe" v-model="rememberMe" type="checkbox" />
          <label for="rememberMe">Запомнить меня</label>
        </div>

        <Button
          class="auth-button"
          variant="primary"
          :disabled="!canSubmit || rateLimit.waiting.value"
          :loading="loading"
          >Войти</Button
        >

        <HoneypotField v-model="honeypot" />
      </form>

      <p class="auth-footnote">
        Еще нет аккаунта?
        <button type="button" class="auth-action" @click="emit('register')">
          Зарегистрироваться
        </button>
      </p>
    </template>
  </AuthDialog>
</template>

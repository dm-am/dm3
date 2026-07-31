<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, nextTick } from "vue";
import { useRouter } from "vue-router";
import type { RegisterCredentials } from "@/shared/api/models/account";

import { useNewPasswordField } from "@/shared/lib/composables/useNewPasswordField";
import {
  useValidatedField,
  validators,
} from "@/shared/lib/composables/useValidatedField";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import {
  PasswordInput,
  PasswordStrengthIndicator,
} from "@/shared/ui/PasswordInput";
import { Tooltip } from "@/shared/ui/Tooltip";
import { accountApi, register } from "@/entities/user";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";
import { announcedByInterceptor, describeFailure } from "@/shared/lib/errors";

const router = useRouter();

const emit = defineEmits<{
  (e: "success", email: string): void;
  (e: "cancel"): void;
  (e: "openRecovery", email?: string): void;
  (e: "openLogin", email?: string): void;
}>();

// Step management
const step = ref<"email" | "password">("email");
const loading = ref(false);
const honeypot = ref("");
const formLoadTime = ref(0);
const emailInputRef = ref<HTMLInputElement | null>(null);
const passwordInputRef = ref<InstanceType<typeof PasswordInput> | null>(null);
// Consent is split in two: the site rules checkbox unlocks only after the
// rules link was actually opened (rulesViewed), while the legal-terms
// checkbox (agreement + privacy) is freely checkable.
const rulesViewed = ref(false);
const acceptedRules = ref(false);
const acceptedTerms = ref(false);

// Email conflict states (for showing action links)
const emailTaken = ref(false);
const emailPending = ref(false);

// Email field with async availability check
const emailField = useValidatedField({
  validate: validators.combine(validators.required(), validators.email()),
  asyncValidate: async (value) => {
    const { data, error } = await accountApi.checkEmail(value);
    if (error || !data) return null; // Silent fail

    if (data.isAvailable) {
      emailTaken.value = false;
      emailPending.value = false;
      return null;
    } else if (data.reason === "PendingActivation") {
      emailTaken.value = false;
      emailPending.value = true;
      return "Регистрация не завершена";
    } else {
      emailTaken.value = true;
      emailPending.value = false;
      return "Почта уже используется";
    }
  },
});

// Password field with HIBP check
const {
  password: newPassword,
  hibpStatus,
  isValid: isPasswordValid,
  onInput: onPasswordInput,
  onBlur: onPasswordBlur,
  reset: resetPassword,
} = useNewPasswordField();

// Can proceed to next step
const canSubmitEmail = computed(
  () => emailField.isReady.value && !emailTaken.value && !emailPending.value,
);

// Can register (HIBP must pass, both consents given)
const canSubmitPassword = computed(
  () => isPasswordValid.value && acceptedRules.value && acceptedTerms.value,
);

onMounted(() => {
  formLoadTime.value = Date.now();
  nextTick(() => emailInputRef.value?.focus());
});

onBeforeUnmount(() => {
  clearForm();
});

const goToSupport = () => {
  emit("cancel");
  nextTick(() => router.push("/support?reason=access"));
};

// Step 1: Submit email
const submitEmail = async () => {
  const isValid = await emailField.validate();
  if (!isValid || emailTaken.value || emailPending.value) return;

  step.value = "password";
  nextTick(() => passwordInputRef.value?.focus());
};

// Step 2: Submit registration

const passwordError = ref("");

const submitPassword = async () => {
  if (!isPasswordValid.value) return;

  const timeSinceLoad = Date.now() - formLoadTime.value;
  if (timeSinceLoad < 2000) {
    passwordError.value = "Подождите пару секунд";
    return;
  }

  loading.value = true;
  passwordError.value = "";

  const credentials: RegisterCredentials = {
    email: emailField.value.value.trim(),
    password: newPassword.value,
    acceptedRules: acceptedRules.value,
    website: honeypot.value,
  };

  const failure = await register(credentials);
  loading.value = false;

  if (!failure) {
    sessionStorage.setItem("dm_pending_email", emailField.value.value.trim());
    emit("success", emailField.value.value.trim());
    return;
  }

  const errors = parseApiErrors(failure);
  const emailErr = getFieldError(errors, "email");
  const passErr = getFieldError(errors, "password");

  if (emailErr) {
    emailField.setError(emailErr);
    step.value = "email";
    return;
  }

  if (passErr) {
    passwordError.value = passErr;
    return;
  }

  // Nothing named a field: the address is already registered, the letter was
  // not accepted, the attempt hit the rate limit. The next screen is
  // "Проверьте почту", so every one of them has to stop here — that screen
  // promises a letter, and the reader waits for it. A failure the interceptor
  // announced is already on screen as a toast.
  if (!announcedByInterceptor(failure.status)) {
    passwordError.value = describeFailure(
      failure,
      "Не удалось зарегистрироваться",
    );
  }
};

// Reset email conflict on input
const onEmailInput = () => {
  emailField.onInput();
  if (emailTaken.value || emailPending.value) {
    emailTaken.value = false;
    emailPending.value = false;
  }
};

const clearForm = () => {
  emailField.reset();
  resetPassword();
  passwordError.value = "";
  emailTaken.value = false;
  emailPending.value = false;
  acceptedRules.value = false;
  acceptedTerms.value = false;
  step.value = "email";
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
  emit("openLogin", emailField.value.value);
};

const handleRecovery = () => {
  emit("openRecovery", emailField.value.value);
};
</script>

<template>
  <Dialog narrow @before-close="clearForm">
    <dialog-title>Регистрация</dialog-title>

    <!-- Step 1: Email -->
    <template v-if="step === 'email'">
      <div class="registration-info">
        <p><strong>Создание дополнительных аккаунтов запрещено.</strong></p>
        <p>
          Если вы утратили доступ к аккаунту, воспользуйтесь
          <button
            type="button"
            class="inline-link"
            @click="
              emit('openRecovery', emailField.value.value.trim() || undefined)
            "
          >
            восстановлением доступа
          </button>
          или обратитесь в
          <button type="button" class="inline-link" @click="goToSupport">
            поддержку</button
          >.
        </p>
      </div>

      <Form
        @submit="submitEmail"
        @cancel="cancel"
        :valid="canSubmitEmail"
        :loading="loading"
        action="Продолжить"
        cancel="Отмена"
      >
        <form-field
          name="email"
          :errors="emailField.error.value ? [emailField.error.value] : []"
        >
          <template #label>
            <label for="email">Почта</label>
            <button
              v-if="emailTaken"
              type="button"
              class="field-action"
              @click="handleLogin"
            >
              Войти?
            </button>
            <button
              v-else-if="emailPending"
              type="button"
              class="field-action"
              @click="handleRecovery"
            >
              Отправить повторное письмо?
            </button>
          </template>
          <input
            ref="emailInputRef"
            v-model="emailField.value.value"
            type="email"
            id="email"
            autocomplete="email"
            title=""
            @input="onEmailInput"
            @blur="emailField.onBlur"
            @keydown.enter.prevent="submitEmail"
          />
        </form-field>

        <input
          name="website"
          v-model="honeypot"
          class="honeypot-field"
          autocomplete="off"
          tabindex="-1"
          aria-hidden="true"
        />
      </Form>
    </template>

    <!-- Step 2: Password -->
    <template v-else-if="step === 'password'">
      <div class="email-display">
        <div class="email-display__label">
          <span>Почта</span>
          <button type="button" class="inline-link" @click="goBack">
            Изменить
          </button>
        </div>
        <div class="email-display__value">{{ emailField.value.value }}</div>
      </div>

      <Form
        @submit="submitPassword"
        @cancel="cancel"
        :valid="canSubmitPassword"
        :loading="loading"
        action="Зарегистрироваться"
        cancel="Отмена"
      >
        <form-field
          label="Пароль"
          name="password"
          :errors="passwordError ? [passwordError] : []"
        >
          <template #hint>Минимум 8 символов</template>
          <password-input
            ref="passwordInputRef"
            v-model="newPassword"
            id="password"
            autocomplete="new-password"
            @input="onPasswordInput"
            @blur="onPasswordBlur"
            @keydown.enter.prevent="submitPassword"
          />
          <PasswordStrengthIndicator
            :password="newPassword"
            :hibp-status="hibpStatus"
          />
        </form-field>

        <!-- Rules consent stays locked until the rules were actually
             opened; the legal-terms consent below is freely checkable. -->
        <div class="rules-checkbox">
          <Tooltip
            text="Сначала откройте правила сайта"
            :disabled="rulesViewed"
          >
            <input
              type="checkbox"
              v-model="acceptedRules"
              :disabled="!rulesViewed"
              id="acceptedRules"
            />
          </Tooltip>
          <label for="acceptedRules"
            >Я принимаю
            <a
              href="/rules"
              target="_blank"
              rel="noopener"
              @click="rulesViewed = true"
              >правила сайта</a
            ></label
          >
        </div>

        <div class="rules-checkbox">
          <input type="checkbox" v-model="acceptedTerms" id="acceptedTerms" />
          <label for="acceptedTerms"
            >Я принимаю условия
            <a href="/agreement" target="_blank" rel="noopener"
              >Пользовательского соглашения</a
            >
            и
            <a href="/privacy" target="_blank" rel="noopener"
              >Политики конфиденциальности</a
            ></label
          >
        </div>

        <input
          name="website"
          v-model="honeypot"
          class="honeypot-field"
          autocomplete="off"
          tabindex="-1"
          aria-hidden="true"
        />
      </Form>
    </template>
  </Dialog>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

a
  font-weight: bold

// Button-as-link, bold weight — matches the surrounding sentence's <a> tags.
// font-weight overrides the mixin's "font: inherit" (the shorthand resets
// weight), so it must stay after the include; the "&" block keeps the CSS
// cascade order explicit (avoids the Sass mixed-decls deprecation).
.inline-link
  +inline-link-button
  &
    font-weight: bold

.field-action
  +inline-link-button
  &
    font-weight: normal

.registration-info
  margin-bottom: $medium
  padding: $small $medium
  border: 1px solid $border
  border-radius: $border-radius
  color: $text-muted
  font-size: $secondary-font-size
  line-height: 1.5

  p
    margin: $minor 0
    &:first-child
      margin-top: 0
    &:last-child
      margin-bottom: 0

.email-display
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

  &__value
    padding: ($minor + 1px) $small
    background-color: $overlay-subtle
    border: 1px solid $border
    color: $text
    cursor: default
    word-break: break-all
    width: 100%
    box-sizing: border-box

.honeypot-field
  position: absolute
  left: -9999px
  width: 1px
  height: 1px
  opacity: 0

.rules-checkbox
  margin-top: $medium
  display: flex
  align-items: flex-start
  gap: $small

  // The two consent rows read as one group: tighter rhythm between them
  & + .rules-checkbox
    margin-top: $small

  // Tooltip wraps the checkbox in an inline span; keep it aligned with the
  // first line of the (often multi-line) label text next to it
  :deep(.tooltip-trigger)
    margin-top: 2px

  input[type="checkbox"]:disabled
    &::before
      content: ""
      position: absolute
      inset: 0
      background-color: $border
      mask-image: url("data:image/svg+xml,%3Csvg width='6' height='6' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M0 6L6 0M-1 1L1 -1M5 7L7 5' stroke='white' stroke-width='1'/%3E%3C/svg%3E")
</style>

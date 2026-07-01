<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount, nextTick } from "vue";
import { useRouter } from "vue-router";
import type { RegisterCredentials } from "@/shared/api/models/account";
import { useUserStore } from "@/entities/user";
import { useNewPasswordField } from "@/shared/lib/composables/useNewPasswordField";
import {
  useValidatedField,
  validators,
} from "@/shared/lib/composables/useValidatedField";
import LightboxTitle from "@/shared/ui/Layout/LightboxTitle.vue";
import {
  PasswordInput,
  PasswordStrengthIndicator,
} from "@/shared/ui/PasswordInput";
import { AccountApi } from "@/shared/api";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";

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
const rulesViewed = ref(false);
const acceptedRules = ref(false);

// Email conflict states (for showing action links)
const emailTaken = ref(false);
const emailPending = ref(false);

// Email field with async availability check
const emailField = useValidatedField({
  validate: validators.combine(validators.required(), validators.email()),
  asyncValidate: async (value) => {
    const { data, error } = await AccountApi.checkEmail(value);
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
  isChecking: hibpChecking,
  onInput: onPasswordInput,
  onBlur: onPasswordBlur,
  reset: resetPassword,
} = useNewPasswordField();

// Can proceed to next step
const canSubmitEmail = computed(
  () => emailField.isReady.value && !emailTaken.value && !emailPending.value,
);

// Can register (HIBP must pass)
const canSubmitPassword = computed(
  () => isPasswordValid.value && acceptedRules.value,
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
const { register } = useUserStore();
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

  const badRequest = await register(credentials);
  loading.value = false;

  if (badRequest) {
    const errors = parseApiErrors(badRequest);
    const emailErr = getFieldError(errors, "email");
    const passErr = getFieldError(errors, "password");

    if (emailErr) {
      emailField.setError(emailErr);
      step.value = "email";
    } else if (passErr) {
      passwordError.value = passErr;
    }
  } else {
    sessionStorage.setItem("dm_pending_email", emailField.value.value.trim());
    emit("success", emailField.value.value.trim());
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
  <Lightbox narrow @before-close="clearForm">
    <lightbox-title>Регистрация</lightbox-title>

    <!-- Step 1: Email -->
    <template v-if="step === 'email'">
      <div class="registration-info">
        <p><strong>Создание дополнительных аккаунтов запрещено.</strong></p>
        <p>
          Если вы утратили доступ к аккаунту, воспользуйтесь
          <a
            href="#"
            @click.prevent="
              emit('openRecovery', emailField.value.value.trim() || undefined)
            "
            >восстановлением доступа</a
          >
          или обратитесь в
          <a href="#" @click.prevent="goToSupport">поддержку</a>.
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
            <a
              v-if="emailTaken"
              href="#"
              @click.prevent="handleLogin"
              class="field-action"
              >Войти?</a
            >
            <a
              v-else-if="emailPending"
              href="#"
              @click.prevent="handleRecovery"
              class="field-action"
              >Отправить повторное письмо?</a
            >
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
          <a href="#" @click.prevent="goBack">Изменить</a>
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

        <div class="rules-checkbox">
          <input
            type="checkbox"
            v-model="acceptedRules"
            :disabled="!rulesViewed"
            id="acceptedRules"
          />
          <span
            >Я принимаю
            <a
              href="/rules"
              target="_blank"
              rel="noopener"
              @click="rulesViewed = true"
              >правила сайта</a
            >, условия
            <a href="/agreement" target="_blank" rel="noopener"
              >Пользовательского соглашения</a
            >
            и
            <a href="/privacy" target="_blank" rel="noopener"
              >Политики конфиденциальности</a
            ></span
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
  </Lightbox>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

a
  font-weight: bold

.field-action
  font-weight: normal

.registration-info
  margin-bottom: $medium
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
  align-items: center
  gap: $small

  input[type="checkbox"]:disabled
    &::before
      content: ""
      position: absolute
      inset: 0
      background-color: $border
      mask-image: url("data:image/svg+xml,%3Csvg width='6' height='6' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M0 6L6 0M-1 1L1 -1M5 7L7 5' stroke='white' stroke-width='1'/%3E%3C/svg%3E")
</style>

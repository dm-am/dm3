<script setup lang="ts">
import { useUserStore } from "@/entities/user";
import { ref, computed, onMounted } from "vue";
import type { LoginCredentials } from "@/shared/api/models/account";
import LightboxTitle from "@/shared/ui/Layout/LightboxTitle.vue";
import { PasswordInput } from "@/shared/ui/PasswordInput";
import { useValidatedField, validators } from "@/shared/lib/composables/useValidatedField";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";

const props = defineProps<{
  prefillEmail?: string;
}>();

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
  (e: "cantSignIn", email?: string): void;
  (e: "resendActivation", email: string): void;
}>();

// Track pending activation state
const pendingActivation = ref(false);

// Form fields
const emailField = useValidatedField({
  initialValue: props.prefillEmail || "",
  validate: validators.required(),
});

const passwordField = useValidatedField({
  validate: validators.required(),
});

const honeypot = ref("");
const formLoadTime = ref(0);
const loading = ref(false);
const rememberMe = ref(true);

const canSubmit = computed(() =>
  emailField.isReady.value && passwordField.isReady.value
);

onMounted(() => {
  formLoadTime.value = Date.now();
});

const userStore = useUserStore();
const { signIn } = userStore;

const submit = async () => {
  // Validate both fields
  const emailValid = await emailField.validate();
  const passwordValid = await passwordField.validate();
  if (!emailValid || !passwordValid) return;

  // Bot protection
  const timeSinceLoad = Date.now() - formLoadTime.value;
  if (timeSinceLoad < 3000) {
    emailField.setError("Подождите перед отправкой формы");
    return;
  }

  loading.value = true;
  pendingActivation.value = false;

  const credentials: LoginCredentials = {
    email: emailField.value.value.trim(),
    password: passwordField.value.value,
    website: honeypot.value,
    rememberMe: rememberMe.value,
  };

  const badRequest = await signIn(credentials);
  loading.value = false;

  if (badRequest) {
    const errors = parseApiErrors(badRequest);

    // Check for pending activation flag
    if (errors["_pendingactivation"]) {
      pendingActivation.value = true;
      passwordField.setError(getFieldError(errors, "email") || "Регистрация не завершена");
    } else {
      pendingActivation.value = false;
      const emailError = getFieldError(errors, "email");
      const passwordError = getFieldError(errors, "password");
      if (emailError) {
        emailField.setError(emailError);
      }
      if (passwordError) {
        passwordField.setError(passwordError);
      }
    }
  } else {
    emit("success");
  }
};

const handleResendActivation = () => {
  emit("resendActivation", emailField.value.value.trim());
};

const onEmailInput = () => {
  emailField.onInput();
  // Clear password error too - credentials are validated as a pair
  if (passwordField.error.value) {
    passwordField.error.value = "";
  }
  if (pendingActivation.value) {
    pendingActivation.value = false;
  }
};

const onPasswordInput = () => {
  passwordField.onInput();
  // Clear email error too - credentials are validated as a pair
  if (emailField.error.value) {
    emailField.error.value = "";
  }
  if (pendingActivation.value) {
    pendingActivation.value = false;
  }
};
</script>

<template>
  <the-lightbox narrow>
    <lightbox-title>Вход</lightbox-title>

    <the-form
      @submit="submit"
      @cancel="emit('cancel')"
      :valid="canSubmit"
      :loading="loading"
      action="Войти"
      cancel="Отмена"
    >
      <form-field label="Почта" name="email" :errors="emailField.error.value ? [emailField.error.value] : []">
        <input
          v-model="emailField.value.value"
          id="email"
          type="email"
          autocomplete="email"
          @input="onEmailInput"
          @blur="emailField.onBlur"
        />
      </form-field>

      <form-field name="password" :errors="passwordField.error.value ? [passwordField.error.value] : []">
        <template #label>
          <label for="password">Пароль</label>
          <a v-if="pendingActivation" class="field-action" @click="handleResendActivation">Отправить повторное письмо?</a>
          <a v-else class="field-action" @click="emit('cantSignIn', emailField.value.value.trim())">Не могу войти</a>
        </template>
        <password-input
          v-model="passwordField.value.value"
          id="password"
          autocomplete="current-password"
          @input="onPasswordInput"
          @blur="passwordField.onBlur"
        />
      </form-field>

      <div class="remember-me">
        <input type="checkbox" v-model="rememberMe" id="rememberMe" />
        <label for="rememberMe">Запомнить меня</label>
      </div>

      <!-- Honeypot field for bot protection -->
      <input
        name="website"
        v-model="honeypot"
        class="honeypot-field"
        autocomplete="off"
        tabindex="-1"
        aria-hidden="true"
      />
    </the-form>
  </the-lightbox>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.field-action
  cursor: pointer

.honeypot-field
  display: none

.remember-me
  margin-top: $medium
  display: flex
  align-items: center
  gap: $small
</style>

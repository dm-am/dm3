<script setup lang="ts">
import { useUserStore } from "@/stores";
import { ref, computed, onMounted } from "vue";
import type { LoginCredentials } from "@/api/models/account";
import LightboxTitle from "@/components/layout/LightboxTitle.vue";
import { useValidatedField, validators } from "@/composables/useValidatedField";
import { icons } from "@/utils/icons";

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
const showPassword = ref(false);
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
    const rawProps = badRequest.invalidProperties ?? badRequest.errors ?? {};
    const props: Record<string, string[]> = {};
    for (const [key, value] of Object.entries(rawProps)) {
      props[key.toLowerCase()] = value;
    }

    // Check for pending activation flag
    if (props["_pendingactivation"]) {
      pendingActivation.value = true;
      passwordField.setError(props["email"]?.[0] || "Регистрация не завершена");
    } else {
      pendingActivation.value = false;
      if (props["email"]?.[0]) {
        emailField.setError(props["email"][0]);
      }
      if (props["password"]?.[0]) {
        passwordField.setError(props["password"][0]);
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
          <div class="password-label-row">
            <label for="password">Пароль</label>
            <a v-if="pendingActivation" class="help-link" @click="handleResendActivation">Отправить повторное письмо?</a>
            <a v-else class="help-link" @click="emit('cantSignIn', emailField.value.value.trim())">Не могу войти</a>
          </div>
        </template>
        <div class="password-wrapper">
          <input
            v-model="passwordField.value.value"
            :type="showPassword ? 'text' : 'password'"
            id="password"
            autocomplete="current-password"
            @input="onPasswordInput"
            @blur="passwordField.onBlur"
          />
          <button
            type="button"
            class="password-toggle"
            @click="showPassword = !showPassword"
            tabindex="-1"
            aria-label="Показать/скрыть пароль"
          >
            <svg
              :viewBox="showPassword ? icons.eyeOpen.viewBox : icons.eyeClosed.viewBox"
              fill="none"
              v-html="showPassword ? icons.eyeOpen.path : icons.eyeClosed.path"
            />
          </button>
        </div>
      </form-field>

      <div class="remember-me">
        <input type="checkbox" v-model="rememberMe" id="rememberMe" />
        <label for="rememberMe">Запомнить меня</label>
      </div>

      <!-- Honeypot field for bot protection -->
      <input
        name="website"
        v-model="honeypot"
        class="hp-field"
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
  font-size: $secondary-font-size

.hp-field
  display: none

.password-label-row
  display: flex
  justify-content: space-between
  align-items: center
  width: 100%

  label
    color: $text-muted
    font-size: $secondary-font-size

.help-link
  cursor: pointer
  font-size: $secondary-font-size

.password-wrapper
  position: relative
  display: block
  width: 100%

:deep(.form-field-row .password-wrapper input)
  padding-right: 36px

.password-toggle
  position: absolute
  right: 4px
  top: 50%
  transform: translateY(-50%)
  background: none
  border: none
  cursor: pointer
  padding: 4px
  color: $text-muted
  display: flex
  align-items: center
  justify-content: center

  svg
    width: 18px
    height: 18px

  &:hover
    color: $text

.remember-me
  margin-top: $medium
  display: flex
  align-items: center
  gap: $small
</style>

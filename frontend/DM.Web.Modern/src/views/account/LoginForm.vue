<script setup lang="ts">
import { useUserStore } from "@/stores";
import { useForm } from "vee-validate";
import { object, string } from "yup";
import { ref, onMounted } from "vue";
import type { LoginCredentials } from "@/api/models/account";
import { ValidationErrorCode } from "@/api/models/common";
import LightboxTitle from "@/components/layout/LightboxTitle.vue";
import TheButton from "@/components/inputs/TheButton.vue";

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
}>();

const { defineInputBinds, handleSubmit, meta, errorBag, setErrors } =
  useForm<LoginCredentials>({
    validationSchema: object({
      login: string().required(ValidationErrorCode.Empty),
      password: string().required(ValidationErrorCode.Empty),
    }),
  });
const login = defineInputBinds("login", {
  validateOnInput: true,
});
const password = defineInputBinds("password", {
  validateOnInput: true,
});
const rememberMe = ref(true);
const honeypot = ref("");
const formLoadTime = ref(0);

// Bot protection: track when form was loaded
onMounted(() => {
  formLoadTime.value = Date.now();
});

const loading = ref(false);
const { signIn } = useUserStore();

// Discord OAuth
const apiHost = import.meta.env.VITE_API_HOST ?? "http://localhost:5051";
function loginWithDiscord() {
  const returnUrl = encodeURIComponent(window.location.origin + "/auth/callback");
  window.location.href = `${apiHost}/connect/discord?returnUrl=${returnUrl}`;
}

const submit = handleSubmit(async (values, { setErrors: formSetErrors }) => {
  // Bot protection: check minimum form fill time (2 seconds)
  const timeSinceLoad = Date.now() - formLoadTime.value;
  if (timeSinceLoad < 2000) {
    setErrors({
      login: "Please wait before submitting the form",
    });
    return;
  }

  loading.value = true;
  const badRequest = await signIn({
    ...values,
    rememberMe: rememberMe.value,
    website: honeypot.value, // Include honeypot field
  });
  loading.value = false;
  if (badRequest) {
    formSetErrors({
      login: badRequest.errors["login"] as unknown as string,
      password: badRequest.errors["password"] as unknown as string,
    });
  } else {
    emit("success");
  }
});
</script>

<template>
  <the-lightbox :with-form="true">
    <lightbox-title>Вход</lightbox-title>

    <the-form
      @submit="submit"
      @cancel="emit('cancel')"
      :valid="meta.valid"
      :loading="loading"
      action="Войти"
      cancel="Отмена"
    >
      <form-field label="Логин" name="login" :errors="errorBag['login']">
        <input v-bind="login" id="login" />
      </form-field>
      <form-field label="Пароль" name="password" :errors="errorBag['password']">
        <input v-bind="password" type="password" id="password" />
      </form-field>
      <form-field name="rememberMe">
        <label>
          <input type="checkbox" v-model="rememberMe" />
          Запомнить меня
        </label>
      </form-field>
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

    <div class="oauth-divider">
      <span>или</span>
    </div>

    <div class="oauth-buttons">
      <the-button type="button" class="discord-btn" @click="loginWithDiscord">
        <svg class="discord-icon" viewBox="0 0 24 24" fill="currentColor">
          <path d="M20.317 4.37a19.791 19.791 0 0 0-4.885-1.515.074.074 0 0 0-.079.037c-.21.375-.444.864-.608 1.25a18.27 18.27 0 0 0-5.487 0 12.64 12.64 0 0 0-.617-1.25.077.077 0 0 0-.079-.037A19.736 19.736 0 0 0 3.677 4.37a.07.07 0 0 0-.032.027C.533 9.046-.32 13.58.099 18.057a.082.082 0 0 0 .031.057 19.9 19.9 0 0 0 5.993 3.03.078.078 0 0 0 .084-.028 14.09 14.09 0 0 0 1.226-1.994.076.076 0 0 0-.041-.106 13.107 13.107 0 0 1-1.872-.892.077.077 0 0 1-.008-.128 10.2 10.2 0 0 0 .372-.292.074.074 0 0 1 .077-.01c3.928 1.793 8.18 1.793 12.062 0a.074.074 0 0 1 .078.01c.12.098.246.198.373.292a.077.077 0 0 1-.006.127 12.299 12.299 0 0 1-1.873.892.077.077 0 0 0-.041.107c.36.698.772 1.362 1.225 1.993a.076.076 0 0 0 .084.028 19.839 19.839 0 0 0 6.002-3.03.077.077 0 0 0 .032-.054c.5-5.177-.838-9.674-3.549-13.66a.061.061 0 0 0-.031-.03zM8.02 15.33c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.956-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.956 2.418-2.157 2.418zm7.975 0c-1.183 0-2.157-1.085-2.157-2.419 0-1.333.955-2.419 2.157-2.419 1.21 0 2.176 1.096 2.157 2.42 0 1.333-.946 2.418-2.157 2.418z"/>
        </svg>
        Войти через Discord
      </the-button>
    </div>
  </the-lightbox>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.hp-field
  position: absolute
  left: -9999px
  width: 1px
  height: 1px
  opacity: 0

.oauth-divider
  display: flex
  align-items: center
  margin: $medium 0
  color: $text-muted

  &::before, &::after
    content: ""
    flex: 1
    border-bottom: 1px solid $border

  span
    padding: 0 $medium
    font-size: 0.9em

.oauth-buttons
  display: flex
  flex-direction: column
  gap: $small

.discord-btn
  background-color: #5865F2
  color: white
  display: flex
  align-items: center
  justify-content: center
  gap: $small

  &:hover
    background-color: #4752C4

.discord-icon
  width: 20px
  height: 20px
</style>

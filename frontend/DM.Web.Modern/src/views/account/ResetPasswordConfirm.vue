<script setup lang="ts">
import { ref, computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useAsyncAction } from "@/composables/useAsyncAction";
import { useHibpCheck } from "@/composables/useHibpCheck";
import accountApi from "@/api/requests/accountApi";
import FormField from "@/components/inputs/form/FormField.vue";
import TheButton from "@/components/inputs/TheButton.vue";
import PasswordStrengthIndicator from "@/components/inputs/PasswordStrengthIndicator.vue";

const route = useRoute();
const router = useRouter();
const { loading, error, execute } = useAsyncAction();

const password = ref("");
const confirmPassword = ref("");
const completed = ref(false);
const hibpWarningDismissed = ref(false);

// HIBP password check
const { isCompromised, isChecking: hibpChecking, checkPassword } = useHibpCheck();

const onPasswordBlur = async () => {
  if (password.value && password.value.length >= 8) {
    hibpWarningDismissed.value = false;
    await checkPassword(password.value);
  }
};

const dismissHibpWarning = () => {
  hibpWarningDismissed.value = true;
};

// Validation errors
const passwordErrors = computed(() => {
  const errors: string[] = [];
  if (password.value.length > 0 && password.value.length < 8) {
    errors.push("Минимум 8 символов");
  }
  return errors;
});

const confirmErrors = computed(() => {
  const errors: string[] = [];
  if (confirmPassword.value.length > 0 && confirmPassword.value !== password.value) {
    errors.push("Пароли не совпадают");
  }
  return errors;
});

const isValid = computed(() => {
  return (
    password.value.length >= 8 &&
    confirmPassword.value.length > 0 &&
    password.value === confirmPassword.value &&
    passwordErrors.value.length === 0 &&
    confirmErrors.value.length === 0
  );
});

const submit = async () => {
  if (!isValid.value) return;

  await execute(async () => {
    const token = route.params.token as string;

    // The API expects login, but we only have token from email
    // Based on the API signature, we need to pass token and newPassword
    const { data, error: apiError } = await accountApi.changePassword({
      token,
      newPassword: password.value,
      login: "", // Token-based reset doesn't require login
    });

    if (apiError) {
      // Check if token is expired or invalid
      if ("invalidProperties" in apiError || "errors" in apiError) {
        const err = apiError as { invalidProperties?: Record<string, string[]>; errors?: Record<string, string[]> };
        const props = err.invalidProperties ?? err.errors ?? {};
        if (props.token || props.general) {
          throw new Error("Ссылка устарела. Запросите сброс пароля повторно.");
        }
      }
      throw new Error("Не удалось изменить пароль. Попробуйте позже.");
    }

    if (data) {
      completed.value = true;
    }
  });
};
</script>

<template>
  <div class="reset-password-page">
    <div class="reset-container">
      <!-- Success state -->
      <div v-if="completed" class="reset-success">
        <h2>Пароль изменен</h2>
        <p>Теперь вы можете войти с новым паролем.</p>
        <the-button @click="router.push('/')">На главную</the-button>
      </div>

      <!-- Form -->
      <div v-else class="reset-form">
        <h2>Новый пароль</h2>
        <p class="description">Введите новый пароль для вашей учетной записи.</p>

        <form @submit.prevent="submit">
          <form-field label="Новый пароль" name="password" :errors="passwordErrors">
            <input
              v-model="password"
              type="password"
              id="password"
              autocomplete="new-password"
              :disabled="loading"
              @blur="onPasswordBlur"
            />
            <template #hint>
              <span v-if="hibpChecking" class="hibp-checking">Проверка...</span>
              <span v-else-if="isCompromised && !hibpWarningDismissed" class="hibp-warning">
                Пароль найден в утечках данных.
                <a href="#" @click.prevent="dismissHibpWarning">Использовать</a>
              </span>
              <span v-else>Минимум 8 символов</span>
            </template>
          </form-field>

          <PasswordStrengthIndicator :password="password" />

          <form-field
            label="Подтверждение"
            name="confirmPassword"
            :errors="confirmErrors"
          >
            <input
              v-model="confirmPassword"
              type="password"
              id="confirmPassword"
              autocomplete="new-password"
              :disabled="loading"
            />
          </form-field>

          <div v-if="error" class="form-error">{{ error }}</div>

          <div class="form-actions">
            <the-button type="submit" :loading="loading" :disabled="!isValid">
              Сохранить
            </the-button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.reset-password-page
  max-width: 480px
  margin: $big auto
  padding: $medium

.reset-container
  width: 100%
  background: $bg-element
  border-radius: $border-radius
  padding: $big
  box-shadow: 0 2px 8px $shadow-color

h2
  margin: 0 0 $medium
  color: $text
  font-size: 1.5rem
  text-align: center

.description
  color: $text-muted
  margin-bottom: $big
  text-align: center
  font-size: 0.9rem

.reset-form
  form
    display: flex
    flex-direction: column
    gap: $medium

  .form-error
    color: $accent-red
    font-size: 0.9rem
    text-align: center
    padding: $small
    background: $bg-highlight-red
    border-radius: $border-radius
    margin-top: $small

  .form-actions
    margin-top: $medium
    display: flex
    justify-content: center

    button
      min-width: 150px

.reset-success
  text-align: center

  p
    color: $text-muted
    margin-bottom: $big
    font-size: 1rem

  button
    min-width: 200px

.hibp-checking
  color: $text-muted
  font-style: italic

.hibp-warning
  color: $text-muted
  a
    color: $link
    margin-left: $tiny
    &:hover
      text-decoration: underline
</style>

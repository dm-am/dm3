<template>
  <section class="section">
    <h2 class="section-title">Безопасность</h2>

    <div class="security-content">
      <!-- Email Change -->
      <div class="security-block">
        <h3 class="subsection-title">Смена электронной почты</h3>
        <div class="current-value">
          Текущая почта: <strong>{{ user.email || "не указана" }}</strong>
        </div>

        <div class="form-group">
          <label for="new-email" class="form-label">Новая почта</label>
          <input
            id="new-email"
            v-model="emailForm.newEmail"
            type="email"
            class="form-input"
          />
        </div>

        <div class="form-group">
          <label for="email-password" class="form-label"
            >Пароль для подтверждения</label
          >
          <input
            id="email-password"
            v-model="emailForm.password"
            type="password"
            class="form-input"
          />
        </div>

        <span v-if="changeEmailAction.error.value" class="error-text">
          {{ changeEmailAction.error.value }}
        </span>
        <TheButton
          :loading="changeEmailAction.loading.value"
          :disabled="!emailForm.newEmail || !emailForm.password"
          @click="changeEmail"
          class="save-button"
        >
          Изменить почту
        </TheButton>
      </div>

      <!-- Password Change -->
      <div class="security-block">
        <h3 class="subsection-title">Смена пароля</h3>

        <div class="form-group">
          <label for="old-password" class="form-label">Текущий пароль</label>
          <input
            id="old-password"
            v-model="oldPassword"
            type="password"
            class="form-input"
          />
        </div>

        <div class="form-group">
          <label for="new-password" class="form-label">Новый пароль</label>
          <input
            id="new-password"
            v-model="newPassword"
            type="password"
            class="form-input"
            @input="onNewPasswordInput"
            @blur="onNewPasswordBlur"
          />
          <PasswordStrengthIndicator
            :password="newPassword"
            :hibp-status="hibpStatus"
            :is-same-as-old="isSameAsOld"
          />
        </div>

        <div class="form-group">
          <label for="confirm-password" class="form-label"
            >Подтвердите новый пароль</label
          >
          <input
            id="confirm-password"
            v-model="confirmPassword"
            type="password"
            class="form-input"
            :class="{
              'input-error':
                confirmPassword &&
                newPassword !== confirmPassword
            }"
          />
          <span
            v-if="
              confirmPassword &&
              newPassword !== confirmPassword
            "
            class="error-text"
          >
            Пароли не совпадают
          </span>
        </div>

        <span v-if="changePasswordAction.error.value" class="error-text">
          {{ changePasswordAction.error.value }}
        </span>
        <TheButton
          :loading="changePasswordAction.loading.value"
          :disabled="!isPasswordFormValid"
          @click="changePassword"
          class="save-button"
        >
          Изменить пароль
        </TheButton>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, computed } from "vue";
import { useUserStore } from "@/entities/user";
import { AccountApi } from "@/shared/api";
import TheButton from "@/shared/ui/Button/TheButton.vue";
import { PasswordStrengthIndicator } from "@/shared/ui/PasswordInput";
import { useAsyncAction } from "@/shared/lib/composables/useAsyncAction";
import { useToast } from "@/shared/lib/composables/useToast";
import { useNewPasswordField } from "@/shared/lib/composables/useNewPasswordField";
import type { User } from "@/shared/api/models/community/users";

defineProps<{
  user: User;
}>();

const userStore = useUserStore();
const toast = useToast();

// Email form
const emailForm = ref({
  newEmail: "",
  password: ""
});

// Password form
const oldPassword = ref("");
const confirmPassword = ref("");

// New password field with HIBP check and same-as-old validation
const {
  password: newPassword,
  hibpStatus,
  isSameAsOld,
  isValid: isNewPasswordValid,
  onInput: onNewPasswordInput,
  onBlur: onNewPasswordBlur
} = useNewPasswordField({ oldPassword });

const passwordsMatch = computed(() =>
  newPassword.value.length > 0 &&
  newPassword.value === confirmPassword.value
);

const isPasswordFormValid = computed(() =>
  oldPassword.value.length > 0 &&
  isNewPasswordValid.value &&
  passwordsMatch.value
);

// Change email
const changeEmailAction = useAsyncAction();

const changeEmail = () => {
  changeEmailAction.execute(async () => {
    if (!emailForm.value.newEmail || !emailForm.value.password) return;

    const { error } = await AccountApi.changeEmail({
      password: emailForm.value.password,
      email: emailForm.value.newEmail
    });
    if (error) throw new Error("Не удалось изменить почту");

    emailForm.value = { newEmail: "", password: "" };
    await userStore.fetchUser();
    toast.success("На новую почту отправлено письмо с подтверждением.");
  });
};

// Change password
const changePasswordAction = useAsyncAction();

const changePassword = () => {
  changePasswordAction.execute(async () => {
    if (!isPasswordFormValid.value) return;

    const { error } = await AccountApi.changePassword({
      oldPassword: oldPassword.value,
      newPassword: newPassword.value
    });
    if (error) throw new Error("Не удалось изменить пароль");

    oldPassword.value = "";
    newPassword.value = "";
    confirmPassword.value = "";
    toast.success("Пароль успешно изменен");
  });
};
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"
@import "../AccountPage.styles"

.security-content
  display: flex
  flex-direction: column
  gap: $big

.security-block
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.current-value
  margin-bottom: $medium
  color: $text-muted

  strong
    color: $text

.input-error
  border-color: $accent-red !important
</style>

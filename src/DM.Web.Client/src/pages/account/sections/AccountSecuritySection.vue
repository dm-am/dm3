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

        <FormField label="Новая почта" name="new-email">
          <input
            id="new-email"
            v-model="emailForm.newEmail"
            type="email"
            autocomplete="email"
          />
        </FormField>

        <FormField label="Пароль для подтверждения" name="email-password">
          <input
            id="email-password"
            v-model="emailForm.password"
            type="password"
            autocomplete="current-password"
          />
        </FormField>

        <span v-if="changeEmailAction.error.value" class="error-text">
          {{ changeEmailAction.error.value }}
        </span>
        <Button
          :loading="changeEmailAction.loading.value"
          :disabled="!emailForm.newEmail || !emailForm.password"
          @click="changeEmail"
          class="save-button"
        >
          Изменить почту
        </Button>
      </div>

      <!-- Password Change -->
      <div class="security-block">
        <h3 class="subsection-title">Смена пароля</h3>

        <FormField label="Текущий пароль" name="old-password">
          <input
            id="old-password"
            v-model="oldPassword"
            type="password"
            autocomplete="current-password"
          />
        </FormField>

        <FormField label="Новый пароль" name="new-password">
          <input
            id="new-password"
            v-model="newPassword"
            type="password"
            autocomplete="new-password"
            @input="onNewPasswordInput"
            @blur="onNewPasswordBlur"
          />
          <PasswordStrengthIndicator
            :password="newPassword"
            :hibp-status="hibpStatus"
            :is-same-as-old="isSameAsOld"
          />
          <template #hint>Минимум 8 символов</template>
        </FormField>

        <FormField
          label="Подтвердите новый пароль"
          name="confirm-password"
          :errors="
            confirmPassword && newPassword !== confirmPassword
              ? ['Пароли не совпадают']
              : []
          "
        >
          <input
            id="confirm-password"
            v-model="confirmPassword"
            type="password"
            autocomplete="new-password"
          />
        </FormField>

        <span v-if="changePasswordAction.error.value" class="error-text">
          {{ changePasswordAction.error.value }}
        </span>
        <Button
          :loading="changePasswordAction.loading.value"
          :disabled="!isPasswordFormValid"
          @click="changePassword"
          class="save-button"
        >
          Изменить пароль
        </Button>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, computed } from "vue";
import { useUserStore } from "@/entities/user";
import { AccountApi } from "@/shared/api";
import Button from "@/shared/ui/Button/Button.vue";
import { FormField } from "@/shared/ui/Form";
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
  password: "",
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
  onBlur: onNewPasswordBlur,
} = useNewPasswordField({ oldPassword });

const passwordsMatch = computed(
  () =>
    newPassword.value.length > 0 && newPassword.value === confirmPassword.value,
);

const isPasswordFormValid = computed(
  () =>
    oldPassword.value.length > 0 &&
    isNewPasswordValid.value &&
    passwordsMatch.value,
);

// Change email
const changeEmailAction = useAsyncAction();

const changeEmail = () => {
  changeEmailAction.execute(async () => {
    if (!emailForm.value.newEmail || !emailForm.value.password) return;

    const { error } = await AccountApi.changeEmail({
      password: emailForm.value.password,
      email: emailForm.value.newEmail,
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
      newPassword: newPassword.value,
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
</style>

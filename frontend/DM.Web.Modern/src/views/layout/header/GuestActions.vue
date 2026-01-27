<script setup lang="ts">
import { useModal } from "vue-final-modal";
import LoginForm from "@/views/account/LoginForm.vue";
import RegistrationForm from "@/views/account/RegistrationForm.vue";
import RegistrationSuccess from "@/views/account/RegistrationSuccess.vue";
import PasswordReset from "@/views/account/PasswordReset.vue";

const { open: openLogin, close: closeLogin } = useModal({
  component: LoginForm,
  attrs: {
    onSuccess: () => closeLogin(),
    onCancel: () => closeLogin(),
  },
});
const { open: openRegistrar, close: closeRegistrar } = useModal({
  component: RegistrationForm,
  attrs: {
    onSuccess: () => {
      closeRegistrar();
      openRegistrarSuccess();
    },
    onCancel: () => closeRegistrar(),
  },
});
const { open: openRegistrarSuccess, close: closeRegistrarSuccess } = useModal({
  component: RegistrationSuccess,
  attrs: {
    onConfirm: () => closeRegistrarSuccess(),
  },
});
const { open: openPasswordReset, close: closePasswordReset } = useModal({
  component: PasswordReset,
  attrs: {
    onSuccess: () => closePasswordReset(),
    onCancel: () => closePasswordReset(),
  },
});
</script>

<template>
  <div class="guest-actions">
    <a @click="openLogin" data-testid="login-button">Вход</a>
    |
    <a @click="openRegistrar" data-testid="register-button">Регистрация</a>
    |
    <a @click="openPasswordReset" data-testid="password-reset-button">Восстановить пароль</a>
  </div>
</template>

<style scoped lang="sass"></style>

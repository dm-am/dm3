<script setup lang="ts">
import { ref, watch, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useModal } from "vue-final-modal";
import LoginForm from "@/views/account/LoginForm.vue";
import RegistrationForm from "@/views/account/RegistrationForm.vue";
import RegistrationSuccess from "@/views/account/RegistrationSuccess.vue";
import RecoveryForm from "@/views/account/RecoveryForm.vue";

const route = useRoute();
const router = useRouter();

// Store email for passing between modals
const registeredEmail = ref("");
const prefillEmail = ref("");

const { open: openLogin, close: closeLogin } = useModal({
  component: LoginForm,
  attrs: {
    get prefillEmail() { return prefillEmail.value; },
    onSuccess: () => closeLogin(),
    onCancel: () => closeLogin(),
    onCantSignIn: () => {
      closeLogin();
      openRecovery();
    },
    onResendActivation: (email: string) => {
      prefillEmail.value = email;
      closeLogin();
      openRecovery();
    },
  },
});

const { open: openRegistrar, close: closeRegistrar } = useModal({
  component: RegistrationForm,
  attrs: {
    onSuccess: (email: string) => {
      registeredEmail.value = email;
      closeRegistrar();
      openRegistrarSuccess();
    },
    onCancel: () => closeRegistrar(),
    onOpenRecovery: (email?: string) => {
      prefillEmail.value = email || "";
      closeRegistrar();
      openRecovery();
    },
    onOpenLogin: (email?: string) => {
      prefillEmail.value = email || "";
      closeRegistrar();
      openLogin();
    },
  },
});

const { open: openRegistrarSuccess, close: closeRegistrarSuccess } = useModal({
  component: RegistrationSuccess,
  attrs: {
    get email() { return registeredEmail.value; },
    onConfirm: () => closeRegistrarSuccess(),
  },
});

const { open: openRecovery, close: closeRecovery } = useModal({
  component: RecoveryForm,
  attrs: {
    get prefillEmail() { return prefillEmail.value; },
    onCancel: () => closeRecovery(),
  },
});

// Handle ?action= query param to open modals from other pages
function handleActionParam() {
  const action = route.query.action as string;
  if (action) {
    // Remove query param first
    router.replace({ query: {} });
    prefillEmail.value = "";

    if (action === "login") {
      openLogin();
    } else if (action === "register") {
      openRegistrar();
    } else if (action === "reset" || action === "recovery") {
      openRecovery();
    }
  }
}

onMounted(handleActionParam);
watch(() => route.query.action, handleActionParam);
</script>

<template>
  <div class="guest-actions">
    <a @click="prefillEmail = ''; openLogin()" data-testid="login-button">Вход</a>
    |
    <a @click="openRegistrar" data-testid="register-button">Регистрация</a>
    |
    <a @click="prefillEmail = ''; openRecovery()" data-testid="recovery-button">Восстановление доступа</a>
  </div>
</template>

<style scoped lang="sass">
</style>

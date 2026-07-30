<script setup lang="ts">
import { ref, watch, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useModal } from "vue-final-modal";
import {
  LoginForm,
  RegistrationForm,
  RegistrationSuccess,
  AccessRecoveryForm,
} from "@/features/auth";

const route = useRoute();
const router = useRouter();

// Store email for passing between modals
const registeredEmail = ref("");
const prefillEmail = ref("");

const { open: openLogin, close: closeLogin } = useModal({
  component: LoginForm,
  attrs: {
    get prefillEmail() {
      return prefillEmail.value;
    },
    onSuccess: () => closeLogin(),
    onCancel: () => closeLogin(),
    onCantSignIn: (email?: string) => {
      prefillEmail.value = email || "";
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
    get email() {
      return registeredEmail.value;
    },
    onConfirm: () => closeRegistrarSuccess(),
  },
});

const { open: openRecovery, close: closeRecovery } = useModal({
  component: AccessRecoveryForm,
  attrs: {
    get prefillEmail() {
      return prefillEmail.value;
    },
    onCancel: () => closeRecovery(),
  },
});

// Handle ?action= query param to open modals from other pages
function handleActionParam() {
  const action = route.query.action as string;
  if (action) {
    // Remove only the consumed action key, keep the rest of the query intact
    const query = { ...route.query };
    delete query.action;
    router.replace({ query });
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

// Extract handlers to avoid inline arrow functions in template
function handleLoginClick() {
  prefillEmail.value = "";
  openLogin();
}

function handleRecoveryClick() {
  prefillEmail.value = "";
  openRecovery();
}
</script>

<template>
  <div class="guest-actions">
    <button
      type="button"
      class="action-link"
      @click="handleLoginClick"
      data-testid="login-button"
    >
      Вход
    </button>
    |
    <button
      type="button"
      class="action-link"
      @click="openRegistrar"
      data-testid="register-button"
    >
      Регистрация
    </button>
    |
    <button
      type="button"
      class="action-link"
      @click="handleRecoveryClick"
      data-testid="recovery-button"
    >
      Восстановление доступа
    </button>
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.action-link
  vertical-align: baseline
  +inline-link-button
</style>

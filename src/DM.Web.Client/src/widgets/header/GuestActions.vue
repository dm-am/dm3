<script setup lang="ts">
import { ref, watch, onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useModal } from "vue-final-modal";
import {
  LoginForm,
  RegistrationForm,
  RegistrationSuccess,
  AccessRecoveryForm,
  TwoFactorRemovalForm,
} from "@/features/auth";
import { REDIRECT_QUERY_KEY, resumeTarget } from "@/shared/lib/auth";

const route = useRoute();
const router = useRouter();

const registeredEmail = ref("");
const registeredHours = ref<number>();
const prefillEmail = ref("");

// Captured when the dialog is asked for, since handleActionParam strips the query at once.
const pendingRedirect = ref<string | null>(null);

function resumeAfterLogin() {
  const target = pendingRedirect.value;
  pendingRedirect.value = null;
  // replace, not push: "Назад" must not lead back into the refusal.
  if (target) void router.replace(target);
}

const { open: openLogin, close: closeLogin } = useModal({
  component: LoginForm,
  attrs: {
    get prefillEmail() {
      return prefillEmail.value;
    },
    onSuccess: () => {
      closeLogin();
      resumeAfterLogin();
    },
    onCancel: () => closeLogin(),
    onCantSignIn: (email?: string) => {
      prefillEmail.value = email || "";
      closeLogin();
      openRecovery();
    },
    // Not the recovery dialog: that one resets a password, which this reader knows.
    onCantPassSecondFactor: () => {
      closeLogin();
      openTwoFactorRemoval();
    },
    onRegister: () => {
      closeLogin();
      openRegistrar();
    },
  },
});

const { open: openRegistrar, close: closeRegistrar } = useModal({
  component: RegistrationForm,
  attrs: {
    onSuccess: (email: string, hours?: number) => {
      registeredEmail.value = email;
      registeredHours.value = hours;
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
    get hours() {
      return registeredHours.value;
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

const { open: openTwoFactorRemoval, close: closeTwoFactorRemoval } = useModal({
  component: TwoFactorRemovalForm,
  attrs: {
    onCancel: () => closeTwoFactorRemoval(),
  },
});

const DIALOG_OF_ACTION = new Map<string, () => unknown>([
  ["login", openLogin],
  ["register", openRegistrar],
  ["reset", openRecovery],
  ["recovery", openRecovery],
  ["two-factor", openTwoFactorRemoval],
]);

// ?action= opens a dialog from another page.
function handleActionParam() {
  const action = route.query.action as string;
  if (action) {
    const query = { ...route.query };
    delete query.action;
    delete query[REDIRECT_QUERY_KEY];
    // Read before the query is rewritten; resumeTarget refuses anything off this site.
    pendingRedirect.value = resumeTarget(route.query[REDIRECT_QUERY_KEY]);
    router.replace({ query });
    prefillEmail.value = "";

    DIALOG_OF_ACTION.get(action)?.();
  }
}

onMounted(handleActionParam);
watch(() => route.query.action, handleActionParam);

function handleLoginClick() {
  prefillEmail.value = "";
  // A deliberate click on "Вход" is not a refused navigation: nothing to resume.
  pendingRedirect.value = null;
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
@use "@/assets/styles/Links" as *

.action-link
  vertical-align: baseline
  +inline-link-button
</style>

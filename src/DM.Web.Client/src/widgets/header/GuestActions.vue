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

// Store email for passing between modals
const registeredEmail = ref("");
const prefillEmail = ref("");

// Where the viewer was going when the guard (or an expired session) sent them
// here. Captured the moment the dialog is asked for, because handleActionParam
// strips the query straight away; spent once, on a sign-in that succeeded.
const pendingRedirect = ref<string | null>(null);

function resumeAfterLogin() {
  const target = pendingRedirect.value;
  pendingRedirect.value = null;
  // replace, not push: the home page the guard substituted is not a step the
  // viewer took, and "Назад" must not lead back into the refusal.
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
    onResendActivation: (email: string) => {
      prefillEmail.value = email;
      closeLogin();
      openRecovery();
    },
    // Neither the phone nor the recovery codes. The mailed path is the only
    // way back from there, and it starts here rather than in the recovery
    // dialog next door: that one resets a password, which this reader knows.
    onCantPassSecondFactor: () => {
      closeLogin();
      openTwoFactorRemoval();
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

const { open: openTwoFactorRemoval, close: closeTwoFactorRemoval } = useModal({
  component: TwoFactorRemovalForm,
  attrs: {
    onCancel: () => closeTwoFactorRemoval(),
  },
});

// Handle ?action= query param to open modals from other pages
function handleActionParam() {
  const action = route.query.action as string;
  if (action) {
    // Remove only the consumed keys, keep the rest of the query intact
    const query = { ...route.query };
    delete query.action;
    delete query[REDIRECT_QUERY_KEY];
    // Read before the query is rewritten. resumeTarget refuses anything that
    // is not a path on this site: the value arrives through the URL.
    pendingRedirect.value = resumeTarget(route.query[REDIRECT_QUERY_KEY]);
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
@use "@/assets/styles/Inputs" as *

.action-link
  vertical-align: baseline
  +inline-link-button
</style>

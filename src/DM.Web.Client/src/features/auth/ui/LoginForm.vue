<script setup lang="ts">
import { signIn, completeSecondFactor } from "@/entities/user";
import { ref, computed, nextTick, onMounted, useTemplateRef } from "vue";
import type { LoginCredentials } from "@/shared/api/models/account";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import { PasswordInput } from "@/shared/ui/PasswordInput";
import {
  useValidatedField,
  validators,
} from "@/shared/lib/composables/useValidatedField";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";
import { announcedByInterceptor, describeFailure } from "@/shared/lib/errors";

const props = defineProps<{
  prefillEmail?: string;
}>();

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
  (e: "cantSignIn", email?: string): void;
  (e: "resendActivation", email: string): void;
  (e: "cantPassSecondFactor"): void;
}>();

// Track pending activation state
const pendingActivation = ref(false);

/**
 * Which half of the login is on screen.
 *
 * The second step is this same dialog with its contents swapped, and not a
 * route of its own: between the two factors there is no session, so there is
 * nowhere to send the reader and nothing that would survive the trip.
 */
const step = ref<"credentials" | "secondFactor">("credentials");

/**
 * Whether the reader said they are typing a recovery code.
 *
 * A hint and a label, nothing more: one field goes to the server either way,
 * and the server tells the two apart by shape. A flag the client set wrongly
 * would turn a right value into a refusal, which is why it never leaves here.
 */
const recoveryMode = ref(false);

/**
 * The one field of the second step.
 *
 * Focused when the step arrives, because the step arrives by swapping the
 * contents of a dialog that is already open: the button the reader pressed is
 * gone, focus falls back to the document, and somebody on a keyboard has to
 * find their way back into a form they never left.
 */
const codeInput = useTemplateRef<HTMLInputElement>("codeInput");

// Form fields
const emailField = useValidatedField({
  initialValue: props.prefillEmail || "",
  validate: validators.required(),
});

const passwordField = useValidatedField({
  validate: validators.required(),
});

const codeField = useValidatedField({
  validate: validators.required(),
});

const honeypot = ref("");
const formLoadTime = ref(0);
const loading = ref(false);
const rememberMe = ref(true);

const canSubmit = computed(
  () => emailField.isReady.value && passwordField.isReady.value,
);

const codeLabel = computed(() =>
  recoveryMode.value ? "Резервный код" : "Код из приложения",
);

const codeHint = computed(() =>
  recoveryMode.value
    ? "Шестнадцать символов резервного кода, регистр и дефисы не важны"
    : "Шесть цифр из приложения-аутентификатора",
);

onMounted(() => {
  formLoadTime.value = Date.now();
});

const submit = async () => {
  // Validate both fields
  const emailValid = await emailField.validate();
  const passwordValid = await passwordField.validate();
  if (!emailValid || !passwordValid) return;

  loading.value = true;

  // Bot protection — transparently wait out the remainder of the minimum
  // form-fill time instead of rejecting the submit. Password managers can
  // fill+submit in well under 3s, so a hard error here punishes legitimate
  // users; honeypot + server rate-limit still catch actual bots.
  const timeSinceLoad = Date.now() - formLoadTime.value;
  if (timeSinceLoad < 3000) {
    await new Promise((resolve) => setTimeout(resolve, 3000 - timeSinceLoad));
  }

  pendingActivation.value = false;

  const credentials: LoginCredentials = {
    email: emailField.value.value.trim(),
    password: passwordField.value.value,
    website: honeypot.value,
    rememberMe: rememberMe.value,
  };

  const outcome = await signIn(credentials);
  loading.value = false;

  if (outcome.stage === "signedIn") {
    emit("success");
    return;
  }

  // The password was right and the login is not finished. This used to read as
  // a success with no viewer in it: the dialog closed, the store stayed empty,
  // and an account with a factor on it could not sign in at all.
  if (outcome.stage === "secondFactor") {
    step.value = "secondFactor";
    recoveryMode.value = false;
    codeField.reset();
    await nextTick();
    codeInput.value?.focus();
    return;
  }

  const failure = outcome.failure;
  const errors = parseApiErrors(failure);

  // Check for pending activation flag
  if (errors["_pendingactivation"]) {
    pendingActivation.value = true;
    passwordField.setError(
      getFieldError(errors, "email") || "Регистрация не завершена",
    );
    return;
  }

  pendingActivation.value = false;
  const emailError = getFieldError(errors, "email");
  const passwordError = getFieldError(errors, "password");
  if (emailError) {
    emailField.setError(emailError);
  }
  if (passwordError) {
    passwordField.setError(passwordError);
  }

  // Nothing named a field, so the server refused the account rather than the
  // form: banned, removed, locked out after too many attempts. The sentence
  // goes under the password field, where the wrong-password sentence goes,
  // because it answers the same submit. A 403 is ours to say — accountApi
  // takes it back from the response interceptor — while a rate limit or a dead
  // server is already on screen as a toast and must not be said twice.
  const refused = failure.status === 403;
  if (
    !emailError &&
    !passwordError &&
    (refused || !announcedByInterceptor(failure.status))
  ) {
    passwordField.setError(describeFailure(failure, "Не удалось войти"));
  }
};

const submitSecondFactor = async () => {
  if (!(await codeField.validate())) return;

  loading.value = true;
  const failure = await completeSecondFactor(codeField.value.value);
  loading.value = false;

  if (!failure) {
    emit("success");
    return;
  }

  // Every way of failing this step answers with one sentence: a wrong code, an
  // expired challenge, a recovery code already spent. A 403 is the account
  // itself refused in the minutes between the two factors, and it belongs in
  // the same place. Anything the interceptor already announced is left to its
  // toast rather than said twice.
  if (failure.status === 403 || !announcedByInterceptor(failure.status)) {
    codeField.setError(describeFailure(failure, "Не удалось войти"));
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
  <Dialog narrow>
    <template v-if="step === 'secondFactor'">
      <dialog-title>Подтверждение входа</dialog-title>

      <Form
        @submit="submitSecondFactor"
        @cancel="emit('cancel')"
        :valid="codeField.isReady.value"
        :loading="loading"
        action="Войти"
        cancel="Отмена"
      >
        <form-field
          name="two-factor-code"
          :errors="codeField.error.value ? [codeField.error.value] : []"
        >
          <template #label>
            <label for="two-factor-code">{{ codeLabel }}</label>
            <button
              type="button"
              class="field-action"
              @click="recoveryMode = !recoveryMode"
            >
              {{
                recoveryMode
                  ? "Ввести код из приложения"
                  : "Ввести резервный код"
              }}
            </button>
          </template>
          <input
            id="two-factor-code"
            ref="codeInput"
            v-model="codeField.value.value"
            class="code-input"
            type="text"
            :inputmode="recoveryMode ? 'text' : 'numeric'"
            autocomplete="one-time-code"
            autocapitalize="off"
            autocorrect="off"
            spellcheck="false"
            @input="codeField.onInput"
            @blur="codeField.onBlur"
          />
          <template #hint>{{ codeHint }}</template>
        </form-field>

        <p class="second-factor-help">
          <button
            type="button"
            class="field-action"
            @click="emit('cantPassSecondFactor')"
          >
            Нет доступа к приложению и резервным кодам?
          </button>
        </p>
      </Form>
    </template>

    <template v-else>
      <dialog-title>Вход</dialog-title>

      <Form
        @submit="submit"
        @cancel="emit('cancel')"
        :valid="canSubmit"
        :loading="loading"
        action="Войти"
        cancel="Отмена"
      >
        <form-field
          label="Почта"
          name="email"
          :errors="emailField.error.value ? [emailField.error.value] : []"
        >
          <input
            v-model="emailField.value.value"
            id="email"
            type="email"
            autocomplete="email"
            @input="onEmailInput"
            @blur="emailField.onBlur"
          />
        </form-field>

        <form-field
          name="password"
          :errors="passwordField.error.value ? [passwordField.error.value] : []"
        >
          <template #label>
            <label for="password">Пароль</label>
            <button
              v-if="pendingActivation"
              type="button"
              class="field-action"
              @click="handleResendActivation"
            >
              Отправить повторное письмо?
            </button>
            <button
              v-else
              type="button"
              class="field-action"
              @click="emit('cantSignIn', emailField.value.value.trim())"
            >
              Не могу войти
            </button>
          </template>
          <password-input
            v-model="passwordField.value.value"
            id="password"
            autocomplete="current-password"
            @input="onPasswordInput"
            @blur="passwordField.onBlur"
          />
        </form-field>

        <div class="remember-me">
          <input type="checkbox" v-model="rememberMe" id="rememberMe" />
          <label for="rememberMe">Запомнить меня</label>
        </div>

        <!-- Honeypot field for bot protection -->
        <input
          name="website"
          v-model="honeypot"
          class="honeypot-field"
          autocomplete="off"
          tabindex="-1"
          aria-hidden="true"
        />
      </Form>
    </template>
  </Dialog>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.field-action
  +inline-link-button

.remember-me
  margin-top: $medium
  display: flex
  align-items: center
  gap: $small

.code-input
  font-family: monospace

.second-factor-help
  margin: $medium 0 0
</style>

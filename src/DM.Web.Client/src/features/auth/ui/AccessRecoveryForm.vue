<script setup lang="ts">
// "No such account" is disclosed on purpose: registration tells a guest the same about any address.
import { computed, nextTick, onMounted, ref } from "vue";
import AuthDialog from "@/shared/ui/Layout/AuthDialog.vue";
import Button from "@/shared/ui/Button/Button.vue";
import { accountApi } from "@/entities/user";
import { supportFor } from "@/entities/ticket";
import {
  useValidatedField,
  validators,
} from "@/shared/lib/composables/useValidatedField";
import { describeFailure, SERVER_ERROR } from "@/shared/lib/errors";
import { useRateLimitNotice } from "@/shared/lib/composables/useRateLimitNotice";
import { useSupportExit } from "../lib/useSupportExit";
import LetterReceipt from "./LetterReceipt.vue";
import { ACTIVATION_RESENT } from "../lib/linkLifetime";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const props = defineProps<{
  prefillEmail?: string;
}>();

const emit = defineEmits<{
  (e: "cancel"): void;
}>();

type Result = "password" | "activation" | "notfound" | null;
const result = ref<Result>(null);
/** How long the link of the letter sent lives, as the answer named it. */
const sentHours = ref<number>();
const loading = ref(false);
const serverError = ref("");

const emailField = useValidatedField({
  // Not what opens the button: a half-typed address would grey it out mid-typing.
  validate: validators.combine(validators.required(), validators.email()),
  initialValue: props.prefillEmail || "",
});

// Live as soon as there is something to send; a half-typed address is answered in the strip.
const canSubmit = computed(() => !!emailField.value.value.trim());

const rateLimit = useRateLimitNotice();

const emailInput = ref<HTMLInputElement | null>(null);

onMounted(async () => {
  await nextTick();
  emailInput.value?.focus();
});

const toSupport = useSupportExit(() => emit("cancel"));
const goToSupport = () => toSupport(supportFor("AccessRecovery"));

// The rate limit first: it is the newest answer and its number keeps moving.
const message = computed(
  () => rateLimit.message.value || emailField.error.value || serverError.value,
);

const submit = async () => {
  // A wait the server imposed is not over because Enter was pressed again.
  if (rateLimit.waiting.value) return;

  const isValid = await emailField.validate();
  if (!isValid) return;

  loading.value = true;
  serverError.value = "";

  // Claimed at the press, not in the method: the activation page calls it with no strip.
  const { data, error } = await accountApi.recover(
    emailField.value.value.trim(),
    OWNS_REFUSAL,
  );
  loading.value = false;

  if (error) {
    if (rateLimit.take(error)) return;

    // Every refusal stays in the strip: a toast fades over a card that has not changed.
    serverError.value = describeFailure(error, SERVER_ERROR);
    return;
  }

  if (!data) {
    serverError.value = SERVER_ERROR;
    return;
  }

  sentHours.value = data.linkLifetimeHours;
  if (data.status === "NotFound") {
    result.value = "notfound";
  } else if (data.status === "ActivationResent") {
    result.value = "activation";
  } else {
    result.value = "password";
  }
};

const tryAgain = () => {
  result.value = null;
  emailField.reset();
  serverError.value = "";
};

const onEmailInput = () => {
  emailField.onInput();
  serverError.value = "";
};

const clearForm = () => {
  rateLimit.clear();
  result.value = null;
  serverError.value = "";
  loading.value = false;
  emailField.reset();
};
</script>

<template>
  <AuthDialog
    :compact="result !== null"
    :center="result !== null"
    :busy="loading"
    @close="emit('cancel')"
    @before-close="clearForm"
  >
    <LetterReceipt
      v-if="result === 'password'"
      :address="emailField.value.value"
      :hours="sentHours"
    >
      Мы отправили письмо со ссылкой для сброса пароля на
    </LetterReceipt>

    <LetterReceipt
      v-else-if="result === 'activation'"
      :address="emailField.value.value"
      :hours="sentHours"
    >
      {{ ACTIVATION_RESENT }}
    </LetterReceipt>

    <template v-else-if="result === 'notfound'">
      <h2 class="auth-title">Аккаунт не найден</h2>
      <p class="auth-text">
        Аккаунта с почтой <strong>{{ emailField.value.value }}</strong> не
        существует
      </p>
      <Button
        class="auth-button auth-button--spaced"
        type="button"
        variant="primary"
        @click="tryAgain"
      >
        Попробовать другую почту
      </Button>
    </template>

    <template v-else>
      <h2 class="auth-title">Восстановление доступа</h2>

      <form @submit.prevent="submit">
        <div class="auth-label-row">
          <label class="auth-label" for="recovery-email">Почта</label>
          <span class="copy-space">{{ " " }}</span>
          <button type="button" class="auth-action" @click="goToSupport">
            Нет доступа к почте?
          </button>
        </div>
        <div class="auth-field">
          <input
            id="recovery-email"
            ref="emailInput"
            v-model="emailField.value.value"
            type="email"
            autocomplete="email"
            aria-describedby="recovery-email-message"
            :aria-invalid="message ? 'true' : undefined"
            @blur="emailField.onBlur"
            @input="onEmailInput"
          />
        </div>
        <!-- Rendered empty too: the strip holds the height. -->
        <div
          id="recovery-email-message"
          class="auth-message"
          :class="{ 'auth-message--error': !!message }"
          role="alert"
        >
          {{ message }}
        </div>

        <Button
          class="auth-button"
          variant="primary"
          :disabled="!canSubmit || rateLimit.waiting.value"
          :loading="loading"
        >
          Отправить письмо
        </Button>
      </form>
    </template>
  </AuthDialog>
</template>

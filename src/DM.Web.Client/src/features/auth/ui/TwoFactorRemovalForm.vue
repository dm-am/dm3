<script setup lang="ts">
// One receipt for every address: the server does not disclose which accounts carry a factor.
import { computed, nextTick, onMounted, ref } from "vue";
import AuthDialog from "@/shared/ui/Layout/AuthDialog.vue";
import Button from "@/shared/ui/Button/Button.vue";
import { accountApi } from "@/entities/user";
import { unwrapResource } from "@/shared/api/envelope";
import type { MailedLink } from "@/shared/api/models/account";
import {
  useValidatedField,
  validators,
} from "@/shared/lib/composables/useValidatedField";
import { describeFailure, SERVER_ERROR } from "@/shared/lib/errors";
import { useRateLimitNotice } from "@/shared/lib/composables/useRateLimitNotice";
import { supportFor } from "@/entities/ticket";
import { useSupportExit } from "../lib/useSupportExit";
import LetterReceipt from "./LetterReceipt.vue";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const emit = defineEmits<{
  (e: "cancel"): void;
}>();

const toSupport = useSupportExit(() => emit("cancel"));
const goToSupport = () => toSupport(supportFor("AccessRecovery"));

const sent = ref(false);
const sentTo = ref("");
/** How long the link of the letter lives, as the answer named it. */
const sentHours = ref<number>();
const loading = ref(false);
const serverError = ref("");

const rateLimit = useRateLimitNotice();

const emailField = useValidatedField({
  // Not what opens the button: a half-typed address would grey it out mid-typing.
  validate: validators.combine(validators.required(), validators.email()),
});

// Live as soon as there is something to send; a half-typed address is answered in the strip.
const canSubmit = computed(() => !!emailField.value.value.trim());

// The rate limit first: it is the newest answer and its number keeps moving.
const message = computed(
  () => rateLimit.message.value || emailField.error.value || serverError.value,
);

const emailInput = ref<HTMLInputElement | null>(null);

onMounted(async () => {
  await nextTick();
  emailInput.value?.focus();
});

const submit = async () => {
  // A wait the server imposed is not over because Enter was pressed again.
  if (rateLimit.waiting.value) return;

  if (!(await emailField.validate())) return;

  loading.value = true;
  serverError.value = "";
  const address = emailField.value.value.trim();
  // The card says every refusal and counts the wait itself, so it claims both at the press.
  const { data, error } = await accountApi.requestTwoFactorRemoval(
    address,
    OWNS_REFUSAL,
  );
  loading.value = false;

  if (error) {
    if (rateLimit.take(error)) return;

    // Every refusal stays in the strip: a toast fades over a card that has not changed.
    serverError.value = describeFailure(error, SERVER_ERROR);
    return;
  }

  sentTo.value = address;
  sentHours.value = unwrapResource<MailedLink>(data)?.linkLifetimeHours;
  sent.value = true;
};

const onEmailInput = () => {
  emailField.onInput();
  serverError.value = "";
};

const clearForm = () => {
  rateLimit.clear();
  sent.value = false;
  sentTo.value = "";
  loading.value = false;
  serverError.value = "";
  emailField.reset();
};
</script>

<template>
  <AuthDialog
    :compact="sent"
    :center="sent"
    :busy="loading"
    @close="emit('cancel')"
    @before-close="clearForm"
  >
    <!-- The same words for every address: the receipt must not say whether the account exists. -->
    <LetterReceipt v-if="sent" :address="sentTo" :hours="sentHours">
      Мы отправили письмо со ссылкой для отключения двухфакторной аутентификации
      на
    </LetterReceipt>

    <template v-else>
      <!-- Named by the step: the full term would take a second line, and a heading here never does. -->
      <h2 class="auth-title">Запрос на отключение</h2>
      <div class="auth-warnbox auth-warnbox--notice">
        <b>Отключение выполняется с задержкой</b>Ссылка из письма назначит
        отключение двухфакторной аутентификации через 7 дней
      </div>

      <form @submit.prevent="submit">
        <label class="auth-label" for="two-factor-removal-email">Почта</label>
        <div class="auth-field">
          <input
            id="two-factor-removal-email"
            ref="emailInput"
            v-model="emailField.value.value"
            type="email"
            autocomplete="email"
            aria-describedby="two-factor-removal-message"
            :aria-invalid="message ? 'true' : undefined"
            @blur="emailField.onBlur"
            @input="onEmailInput"
          />
        </div>
        <!-- Rendered empty too: the strip holds the height. -->
        <div
          id="two-factor-removal-message"
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

      <p class="auth-footnote">
        Нужна помощь? Обратитесь в
        <button type="button" class="auth-action" @click="goToSupport">
          поддержку
        </button>
      </p>
    </template>
  </AuthDialog>
</template>

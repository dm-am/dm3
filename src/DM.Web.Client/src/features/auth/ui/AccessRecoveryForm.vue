<script setup lang="ts">
/**
 * One address, three answers.
 *
 * The endpoint decides for itself what the address needs — a password reset for
 * a live account, a second activation letter for a registration that never
 * finished — so the form asks once and then names which of the two went out.
 * The third answer, that no such account exists, is disclosed on purpose: the
 * registration form next door tells a guest the same fact about any address, so
 * withholding it here would cost a reader his way back and buy no secrecy.
 */
import { computed, nextTick, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import AuthDialog from "@/shared/ui/Layout/AuthDialog.vue";
import Button from "@/shared/ui/Button/Button.vue";
import { accountApi } from "@/entities/user";
import { supportFor } from "@/entities/ticket";
import {
  useValidatedField,
  validators,
} from "@/shared/lib/composables/useValidatedField";
import { describeFailure } from "@/shared/lib/errors";
import { useRateLimitNotice } from "@/shared/lib/composables/useRateLimitNotice";

const props = defineProps<{
  prefillEmail?: string;
}>();

const emit = defineEmits<{
  (e: "cancel"): void;
}>();

const router = useRouter();

/**
 * What the strip says when the press failed and the server named nothing
 * readable: the answer never arrived, the response carried no payload, or the
 * call threw. All three are one thing to the reader, and the wording is the one
 * the API puts in the title of a 500, so the card never has two names for a
 * failed press. One sentence, so no full stop.
 */
const FAILED_PRESS = "Ошибка сервера";

/** Which of the three answers came back, or none yet. */
type Result = "password" | "activation" | "notfound" | null;
const result = ref<Result>(null);
const loading = ref(false);
const serverError = ref("");

const emailField = useValidatedField({
  // The format is answered on leaving the field and again on submit, and it is
  // deliberately not what opens the button: a half-typed address would grey the
  // control out while it is being typed, and the reader reads that as a screen
  // that stopped working rather than as an address that is not finished.
  validate: validators.combine(validators.required(), validators.email()),
  initialValue: props.prefillEmail || "",
});

/**
 * The button is live as soon as there is something to send.
 *
 * A press on a half-typed address is answered in the strip below, in the
 * sentence the field already knows - which is a refusal the reader can read,
 * where a dead button is one he can only guess at.
 */
const canSubmit = computed(() => !!emailField.value.value.trim());

/**
 * The rate limit, shown by this card instead of by a toast over it: the button
 * says a letter will be sent, and a reader who gets nothing presses it again.
 */
const rateLimit = useRateLimitNotice();

const emailInput = ref<HTMLInputElement | null>(null);

onMounted(async () => {
  await nextTick();
  // The caret goes into the first field with nothing in it, and this card has
  // one field: an address carried over from the login form is still the thing
  // the reader is here to confirm or to correct.
  emailInput.value?.focus();
});

/**
 * The mailbox is out of reach as well, and no dialog can answer that: the way
 * back runs through a person. The reason travels in the address, so the ticket
 * opens on the category this card is about and the reader is not asked to pick
 * it out of a list. The dialog is closed first, so the support page does not
 * open behind a dimmed overlay.
 */
const goToSupport = () => {
  emit("cancel");
  nextTick(() => router.push(supportFor("AccessRecovery")));
};

// One strip under the field carries both refusals - the format one the field
// raises for itself, and whatever the server said. They never hold at once:
// typing clears the second.
// The rate limit stands ahead of both: it is the newest answer to the submit,
// it is why the button is shut, and its number keeps moving while the other two
// would sit there unchanged.
const message = computed(
  () => rateLimit.message.value || emailField.error.value || serverError.value,
);

const submit = async () => {
  // A wait the server imposed is not over because Enter was pressed again.
  if (rateLimit.waiting.value) return;

  // Validate first
  const isValid = await emailField.validate();
  if (!isValid) return;

  loading.value = true;
  serverError.value = "";

  try {
    // This card prints every refusal of the press in its own strip and counts
    // the wait down on its own button, so it claims both from the interceptor
    // here, at the press. Fixed inside the method instead, the same two flags
    // would also silence the activation page, which presses the same endpoint
    // and has no strip to print anything in.
    const { data, error } = await accountApi.recover(
      emailField.value.value.trim(),
      { ownsRefusal: true, ownsRateLimit: true },
    );

    if (error) {
      // The limit answers in the strip below, with the seconds running down and
      // the button shut; everything else stands in the same strip, in the
      // sentence the server sent, or the reader waits for a letter nobody
      // posted.
      if (rateLimit.take(error)) return;

      // Every refusal of this press, including the ones the interceptor also
      // says in a toast: the toast fades after eight seconds over a card that
      // has not changed, and a letter that never went out is exactly what the
      // reader must not be left believing in.
      //
      // The fallback is worded exactly as the API titles a 500, so the strip
      // has one sentence for a failed press whether the answer came back or
      // never did: two names for one failure only ask the reader to tell them
      // apart. One sentence, so no full stop.
      serverError.value = describeFailure(error, FAILED_PRESS);
      return;
    }

    if (!data) {
      serverError.value = FAILED_PRESS;
      return;
    }

    if (data.status === "NotFound") {
      result.value = "notfound";
    } else if (data.status === "ActivationResent") {
      result.value = "activation";
    } else {
      result.value = "password";
    }
  } catch {
    serverError.value = FAILED_PRESS;
  } finally {
    loading.value = false;
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
  <!-- An answer needs a narrower card than the question did: two lines and a
       way out, against a card sized for a labelled field. -->
  <AuthDialog
    :compact="result !== null"
    :center="result !== null"
    @close="emit('cancel')"
    @before-close="clearForm"
  >
    <!-- Result: password reset sent -->
    <template v-if="result === 'password'">
      <h2 class="auth-title">Проверьте почту</h2>
      <p class="auth-text">
        Мы отправили письмо со ссылкой для сброса пароля на
        <strong>{{ emailField.value.value }}</strong>
      </p>
      <!-- Both links out of this card live the same two days: the reset window
           was raised to the activation one so a receipt never has to explain
           why one letter is worth less time than the other. -->
      <p class="auth-note">Ссылка действительна 48 часов</p>
      <!-- No button. There is nothing left to do on a receipt, and the cross of
           the dialog is the way out of it. -->
    </template>

    <!-- Result: activation resent -->
    <template v-else-if="result === 'activation'">
      <h2 class="auth-title">Проверьте почту</h2>
      <p class="auth-text">
        Мы отправили повторное письмо со ссылкой для активации на
        <strong>{{ emailField.value.value }}</strong>
      </p>
      <p class="auth-note">Ссылка действительна 48 часов</p>
    </template>

    <!-- Result: no such account -->
    <template v-else-if="result === 'notfound'">
      <h2 class="auth-title">Аккаунт не найден</h2>
      <p class="auth-text">
        Аккаунта с почтой <strong>{{ emailField.value.value }}</strong> не
        существует
      </p>
      <!-- The one thing worth doing here is trying another address, so that is
           the button. The button that used to stand here only closed the
           dialog, which is what the cross in the corner does. -->
      <Button
        class="auth-button auth-button--spaced"
        type="button"
        variant="primary"
        @click="tryAgain"
      >
        Попробовать другую почту
      </Button>
    </template>

    <!-- The question -->
    <template v-else>
      <h2 class="auth-title">Восстановление доступа</h2>

      <!-- A real form, so Enter from the field sends it, as it did before the
           grey footer of Form.vue left this contour. -->
      <form @submit.prevent="submit">
        <div class="auth-label-row">
          <label class="auth-label" for="recovery-email">Почта</label>
          <!-- A real space, so the row copies as one line (Reset.sass,
               .copy-space). -->
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
        <!-- Rendered even when empty: the strip holds the height, so the card
             does not change size when a refusal appears. -->
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

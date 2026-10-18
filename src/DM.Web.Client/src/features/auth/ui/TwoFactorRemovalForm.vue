<script setup lang="ts">
/**
 * The way back for somebody who lost both the phone and the recovery codes.
 *
 * The letter itself takes nothing off. Following the link in it schedules the
 * removal a week ahead, ends every session and sends a second letter that can
 * call it off - and so can any successful sign-in with the factor. That is the
 * whole point of the delay: whoever holds the mailbox gets the account either
 * way, but only after a week of noise the owner can stop.
 *
 * One result screen for every address, because the server answers the same for
 * every address: which accounts carry a factor is not something it discloses.
 */
import { computed, nextTick, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import AuthDialog from "@/shared/ui/Layout/AuthDialog.vue";
import Button from "@/shared/ui/Button/Button.vue";
import { accountApi } from "@/entities/user";
import {
  useValidatedField,
  validators,
} from "@/shared/lib/composables/useValidatedField";
import { describeFailure } from "@/shared/lib/errors";
import { useRateLimitNotice } from "@/shared/lib/composables/useRateLimitNotice";
import { supportFor } from "@/entities/ticket";

const emit = defineEmits<{
  (e: "cancel"): void;
}>();

const router = useRouter();

/**
 * The last way out of this card, for a reader who cannot reach the mailbox
 * either. He arrived here without the phone and without the codes, so the
 * address is the only thing this dialog can still ask for, and an address he
 * does not hold leaves him nowhere. The dialog closes first, so the support
 * page does not open behind a dimmed overlay, the way the recovery card does
 * it (AccessRecoveryForm).
 */
const goToSupport = () => {
  emit("cancel");
  nextTick(() => router.push(supportFor("AccessRecovery")));
};

const sent = ref(false);
/** The address the letter went to, as the receipt has to name it back. */
const sentTo = ref("");
const loading = ref(false);
const serverError = ref("");

/**
 * The rate limit, shown by this card instead of by a toast over it. This path is
 * anonymous and sits on the address budget, so the limit is the refusal it
 * actually meets, and it meets it from a reader who has already run out of
 * other ways in.
 */
const rateLimit = useRateLimitNotice();

const emailField = useValidatedField({
  // The format is answered on leaving the field and again on submit, and it is
  // deliberately not what opens the button: a half-typed address would grey the
  // control out while it is being typed, and the reader reads that as a screen
  // that stopped working rather than as an address that is not finished.
  validate: validators.combine(validators.required(), validators.email()),
});

/**
 * The button is live as soon as there is something to send.
 *
 * A press on a half-typed address is answered in the strip below, in the
 * sentence the field already knows - which is a refusal the reader can read,
 * where a dead button is one he can only guess at.
 */
const canSubmit = computed(() => !!emailField.value.value.trim());

// One strip under the field carries both refusals - the format one the field
// raises for itself, and whatever the server said. They never hold at once:
// typing clears the second.
// The rate limit stands ahead of both: it is the newest answer to the submit,
// it is why the button is shut, and its number keeps moving while the other two
// would sit there unchanged.
const message = computed(
  () => rateLimit.message.value || emailField.error.value || serverError.value,
);

const emailInput = ref<HTMLInputElement | null>(null);

onMounted(async () => {
  await nextTick();
  // The card opens with one empty field, and that is where the caret belongs:
  // the reader arrived here to type an address and nothing else.
  emailInput.value?.focus();
});

const submit = async () => {
  // A wait the server imposed is not over because Enter was pressed again.
  if (rateLimit.waiting.value) return;

  if (!(await emailField.validate())) return;

  loading.value = true;
  serverError.value = "";
  const address = emailField.value.value.trim();
  // This card prints every refusal of the press in its strip and counts the
  // wait down on its own button, so it claims both from the interceptor here,
  // at the press - the request cannot know which screen sent it, and the same
  // flags fixed inside it would silence a screen that shows nothing.
  const { error } = await accountApi.requestTwoFactorRemoval(address, {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
  loading.value = false;

  if (error) {
    // The limit answers in the strip below, with the seconds running down and
    // the button shut; everything else stands in the same strip, or the reader
    // waits for a letter nobody sent.
    if (rateLimit.take(error)) return;

    // Every refusal of this press, including the ones the interceptor also says
    // in a toast: the toast fades after eight seconds over a card that has not
    // changed, and this is the card of a reader who has already run out of
    // other ways in - a letter he never gets and no line explaining why leaves
    // him with nothing.
    //
    // The fallback is worded exactly as the API titles a 500, so the strip has
    // one sentence for a failed press whether the answer came back or never
    // did: two names for one failure only ask the reader to tell them apart.
    // One sentence, so no full stop.
    serverError.value = describeFailure(error, "Ошибка сервера");
    return;
  }

  sentTo.value = address;
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
  <!-- The answer is two lines and the cross, so it takes the narrow card; the
       question needs the width of a labelled field. -->
  <AuthDialog
    :compact="sent"
    :center="sent"
    @close="emit('cancel')"
    @before-close="clearForm"
  >
    <template v-if="sent">
      <h2 class="auth-title">Проверьте почту</h2>
      <!-- The letter goes out now and the link in it lives a day: the week is
           what starts when the link is followed, and it is the letter's to
           explain. The receipt said "если аккаунт есть", which is the one thing
           it must not say out loud - the sentence turned a screen into a
           riddle to keep a secret the sentence itself was announcing. The same
           words for every address keep the secret and ask nothing of the
           reader. -->
      <p class="auth-text">
        Мы отправили письмо со ссылкой для отключения двухфакторной
        аутентификации на <strong>{{ sentTo }}</strong>
      </p>
      <p class="auth-note">Ссылка действительна 24 часа</p>
      <!-- No button. There is nothing left to do on a receipt, and the cross of
           the dialog is the way out of it. -->
    </template>

    <template v-else>
      <!-- The card is named by what it does, the way every other heading of
           this contour is. It used to repeat the question the link asks ("Нет
           доступа к приложению?"), which names the reader's trouble and not the
           errand he is on. Owner's decision.
           What it does not carry is the term itself. "Отключение двухфакторной
           аутентификации" measures 493.8px where the heading of this card has
           334, and 398.9 against 278 on a phone (guestTitleFits.spec.ts), so it
           would take a second line, and a heading of this surface never does.
           Cutting the term down ("Отключение аутентификации") would be a second
           name for the one thing this edit is here to give one name, so the
           step is named instead - the errand the card is on, spelled the way
           the three screens the letter leads to spell theirs ("Подтвердите
           отключение", "Отключение назначено", "Отключение отменено"). The term
           itself stands in full in the box directly under the heading. -->
      <h2 class="auth-title">Запрос на отключение</h2>
      <!-- What the link in the letter does, in the box the confirmation screen
           asks in (TwoFactorRemovalPage): the delay is the same delay on both
           screens, and a caution that changes its shape between the request and
           the press reads as a different caution. The headline is the delay;
           the line under it says what the link schedules and when it lands, so
           the reader is not left thinking the letter itself takes the factor
           off. Centred inside the box, as the box centres its own prose. -->
      <div class="auth-warnbox auth-warnbox--notice">
        <b>Отключение выполняется с задержкой</b>Ссылка из письма назначит
        отключение двухфакторной аутентификации через 7 дней
      </div>

      <!-- A real form, so Enter from the field sends it, as it did before the
           grey footer of Form.vue left this contour. -->
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
        <!-- Rendered even when empty: the strip holds the height, so the card
             does not change size when a refusal appears. -->
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

      <!-- The one exit this card had none of. Everywhere else on the guest
           surface the reader who runs out of ways in is offered a person; here
           he is the reader most likely to need one, since he is already down to
           his last factor. Under the rule, where the surface keeps its one
           secondary offer, and not on the receipt above: there the letter is
           out and nothing has gone wrong. -->
      <p class="auth-footnote">
        Нужна помощь? Обратитесь в
        <button type="button" class="auth-action" @click="goToSupport">
          поддержку
        </button>
      </p>
    </template>
  </AuthDialog>
</template>

<!-- No styles of its own: the caution above is drawn by .auth-warnbox and
     centred by its --notice modifier, both in AuthSurface.sass, where the rest
     of this card is drawn too. As loose prose those two lines sat on the
     field's left edge; inside the box they keep the box's own axis. -->

<script setup lang="ts">
import { signIn, completeSecondFactor, accountApi } from "@/entities/user";
import { ref, computed, nextTick, onMounted } from "vue";
import { useRouter } from "vue-router";
import type { LoginCredentials } from "@/shared/api/models/account";
import { supportFor } from "@/entities/ticket";
import { AuthDialog } from "@/shared/ui/Layout";
import { Button } from "@/shared/ui/Button";
import { PasswordInput } from "@/shared/ui/PasswordInput";
import { useCodeInput } from "@/shared/lib/composables/useCodeInput";
import {
  useValidatedField,
  validators,
} from "@/shared/lib/composables/useValidatedField";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";
import type { GeneralError } from "@/shared/api/models/common";
import { formatDate } from "@/shared/lib/utils/datetime";
import {
  describeFailure,
  describeRateLimit,
  refusalHere,
} from "@/shared/lib/errors";
import { useRateLimitNotice } from "@/shared/lib/composables/useRateLimitNotice";

const props = defineProps<{
  prefillEmail?: string;
}>();

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
  (e: "cantSignIn", email?: string): void;
  (e: "cantPassSecondFactor"): void;
  (e: "register"): void;
}>();

const router = useRouter();

// Track pending activation state
const pendingActivation = ref(false);

/**
 * A refusal that may carry the end of the ban, as the API may come to name it.
 *
 * The login's 403 carries no such key today — it is built from a status and a
 * message and nothing else — so a ban currently reads as one with no term,
 * which is the sentence for a ban that has none.
 */
type BanRefusal = GeneralError & { banExpiresUtc?: string | null };

/**
 * The titles the API answers these two with, from
 * DM.Domain.Core.Exceptions.RefusalMessage (AccountBanned, AccountRemoved).
 * They are the server's vocabulary, not copy: nothing on screen shows them.
 */
const BANNED_TITLE = "Аккаунт забанен";
const REMOVED_TITLE = "Аккаунт удален";

/**
 * What the strip says about an account the server refused, or null when the
 * refusal is not one of the two and belongs to whoever asked.
 *
 * The client decides the wording of a fact the server named, the way
 * validationErrors.ts decides the wording of a validation code: the API answers
 * "Аккаунт забанен", which is the event, and this is the event said to the
 * person it stopped. A refusal it does not recognise is left to the server's
 * own sentence.
 */
const isRemoved = (failure: GeneralError) =>
  failure.status === 403 && failure.title === REMOVED_TITLE;

const readAccountRefusal = (failure: GeneralError): string | null => {
  // Only a 403 is the account itself. A wrong password is a 400 with the field
  // named, an unfinished registration says so, and the lockout after too many
  // wrong passwords is a 403 about the attempts and not about the account.
  if (failure.status !== 403) return null;

  if (isRemoved(failure)) return "Аккаунт с этой почтой был удален";

  if (failure.title === BANNED_TITLE) {
    const until = (failure as BanRefusal).banExpiresUtc;
    return until
      ? `Аккаунт находится в полном бане до ${formatDate(until)}`
      : "Аккаунт находится в полном бане бессрочно";
  }

  return null;
};

/** The title the server puts on a door shut by too many wrong passwords. */
const LOCKED_TITLE = "Вход заблокирован";

/**
 * The server's refusal, said in the words of this screen.
 *
 * Almost every one of them arrives written the way this screen would write it,
 * and goes through unchanged. The lockout is the exception. It used to stand
 * one word away from the rate limit — "Вход заблокирован после неудачных
 * попыток" against "Слишком много попыток, попробуйте через 30 секунд" — and a
 * reader who met both learned nothing from the difference and lost the one
 * thing worth knowing: when to come back. The two events are not the same
 * (fifteen wrong passwords shut this account's door for half an hour; a rate
 * limit is a budget of requests an address spends in a minute), but the answer
 * to both is the same act, so they are now one sentence with the wait in it.
 * The number rides with the refusal and the wording is the shared one, so the
 * two cannot drift apart again.
 */
const asSaidHere = (failure: GeneralError, sentence: string) =>
  isLockout(failure) ? describeRateLimit(failure.retryAfterSeconds) : sentence;

/**
 * Whether this refusal is the door shut by too many wrong passwords.
 *
 * The countdown cannot tell it from the other refusals of its status, so the
 * screen that knows the title says so. It is handed to the same counter the
 * rate limit uses, which shuts the button and takes the number down: one
 * sentence with two sets of rules under it is what the reader reads as a
 * broken form.
 */
const isLockout = (failure: GeneralError) =>
  failure.status === 403 && failure.title === LOCKED_TITLE;

/**
 * What moderation did to the account, when that is the answer to the submit.
 *
 * It stands in the strip every other refusal of this form stands in — under
 * the password on the first step, under the code on the second — because a
 * refusal kept in a box of its own is a second place to look for one thing.
 * It outlives the step: the same two refusals arrive before the first factor
 * and between the two, and the reader is told the same thing either way.
 */
const accountRefusal = ref<string | null>(null);

/**
 * Whether the account this card just named is gone.
 *
 * Carried beside the sentence rather than read back out of it: the strip holds
 * one line for every refusal about an account, and the ban's line has the same
 * shape as this one.
 */
const removedAccount = ref(false);

/**
 * The way back for the owner of an account that is gone.
 *
 * Nothing on this card can bring it back: an account is removed and restored by
 * moderation alone, so what is offered is a ticket with the reason already
 * chosen. It takes the place of "Не могу войти" the moment the server says the
 * account was removed, and only then. Standing there from the start, it was an
 * offer made to every reader of the card, most of whom have nothing to restore;
 * and it told them nothing, because the state it belongs to is the one state
 * that says out loud what happened — the strip under the password already reads
 * "Аккаунт с этой почтой был удален", so the link beside it gives away nothing
 * the card has not already said.
 */
const goToRestoration = () => {
  emit("cancel");
  nextTick(() => router.push(supportFor("AccountRestoration")));
};

/**
 * Which half of the login is on screen.
 *
 * The second step is this same dialog with its contents swapped, and not a
 * route of its own: between the two factors there is no session, so there is
 * nowhere to send the reader and nothing that would survive the trip. One
 * AuthDialog holds both halves for the same reason: two of them under a
 * v-if/v-else would close the modal and open a second one at the moment the
 * password is accepted. The receipt for a second activation letter is the same
 * card again, for the same reason: the letter went out of this screen.
 */
const step = ref<"credentials" | "secondFactor" | "activationResent">(
  "credentials",
);

/**
 * Whether the reader said they are typing a recovery code.
 *
 * A name, a mask and a parse, nothing more: one value goes to the server either
 * way, and the server tells the two apart by shape. A flag the client set
 * wrongly would turn a right value into a refusal, which is why it never leaves
 * here.
 */
const recoveryMode = ref(false);

/**
 * The name of each kind, written once: the label says one of them, the link
 * beside it offers the other.
 *
 * The link says the act and not the bare name. Label and link stand in one row
 * and the two names differ by a word, so a noun beside a noun left the question
 * mark on the end of one of them to say which was the control.
 */
const APP_CODE = "Код из приложения";
const RECOVERY_CODE = "Резервный код";
const ENTER_APP_CODE = "Ввести код из приложения";
const ENTER_RECOVERY_CODE = "Ввести резервный код";

/**
 * What the strip says when the request never reached the server.
 *
 * Written out rather than left as the bare name of the trouble, because the
 * reader of this one is on a working screen with a dead wire behind it, and
 * pressing again is the thing that fixes it more often than not.
 */
const NO_ANSWER = "Не удалось войти, попробуйте еще раз";

/**
 * The one field of the second step, and the parse behind it.
 *
 * The composable repeats what the server keeps of the entry, six digits or
 * sixteen symbols of Crockford's alphabet in groups of four, so the field
 * cannot hand over something refused on shape alone.
 */
const code = useCodeInput({
  mode: () => (recoveryMode.value ? "recovery" : "digits"),
});

/** What the server answered the code with, in the strip under the field. */
const codeError = ref("");

/**
 * The rate limit, when the way in runs into one, shown by this form rather than
 * by a toast over it. Both steps share it: the limit is on the address, the two
 * submits spend the same budget, and the reader who met it on the password step
 * meets the same wait on the code step.
 */
const rateLimit = useRateLimitNotice();

// Form fields
const emailField = useValidatedField({
  initialValue: props.prefillEmail || "",
  // The shape of the address is checked here as it is on every other card of
  // the surface. Owner's decision: a reader who mistyped the address is told
  // what is wrong with it under the field, in the same words and the same place
  // as on registration and on access recovery, instead of being sent to press
  // the button and meet "Неверная почта или пароль" - a sentence that points at
  // the password as readily as at the address.
  validate: validators.combine(validators.required(), validators.email()),
});

const passwordField = useValidatedField({
  validate: validators.required(),
});

const honeypot = ref("");
const formLoadTime = ref(0);
const loading = ref(false);
const rememberMe = ref(true);

/**
 * The address field, and the eye-bearing password field beside it.
 *
 * Focused on open, because a dialog that appears over the page has taken the
 * reader's attention already and asking them to click into the first field is
 * asking twice.
 */
const emailInput = ref<HTMLInputElement | null>(null);
const passwordInput = ref<InstanceType<typeof PasswordInput> | null>(null);

const canSubmit = computed(
  () => !!emailField.value.value.trim() && !!passwordField.value.value.trim(),
);

/**
 * The strip under the password, and what stands in it.
 *
 * Three sentences can want it and they are ordered by how new they are. The
 * rate limit wins: it is the newest answer to this submit, it is the reason the
 * button is shut, and the number in it keeps moving while an earlier sentence
 * would sit there stale. The refusal of the account comes next, over the
 * field's own sentence, because it answers the submit that cleared the field.
 */
const passwordMessage = computed(
  () =>
    rateLimit.message.value ||
    accountRefusal.value ||
    passwordField.error.value,
);

/** The same three, for the one strip of the second step. */
const codeMessage = computed(
  () => rateLimit.message.value || accountRefusal.value || codeError.value,
);

/** What the label says: the kind being typed right now. */
const codeName = computed(() =>
  recoveryMode.value ? RECOVERY_CODE : APP_CODE,
);

/** What the link beside it offers: the other kind, said as the act. */
const otherCodeAction = computed(() =>
  recoveryMode.value ? ENTER_APP_CODE : ENTER_RECOVERY_CODE,
);

/** What the strip under the field says while the server has said nothing. */
const codeHint = computed(() =>
  recoveryMode.value
    ? "Шестнадцать знаков из списка резервных кодов"
    : "Шесть цифр из приложения-аутентификатора",
);

onMounted(async () => {
  formLoadTime.value = Date.now();

  await nextTick();
  // The first field with nothing in it. An address carried over from another
  // form is not the thing left to fill in. The password is.
  if (emailField.value.value.trim()) passwordInput.value?.focus();
  else emailInput.value?.focus();
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
  accountRefusal.value = null;
  removedAccount.value = false;

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
    code.reset();
    codeError.value = "";
    await nextTick();
    code.inputRef.value?.focus();
    return;
  }

  const failure = outcome.failure;

  // The limit on the way in is the one refusal a reader answers by pressing
  // again, so the form has to change: the sentence goes into the strip below
  // with the seconds counting down, and the button is shut until they run out.
  // The request took the limit over from the interceptor, so nothing else says
  // it. The lockout after too many wrong passwords goes to the same counter:
  // one sentence on screen, one set of rules under it.
  if (rateLimit.take(failure, isLockout(failure))) return;

  // The credentials were accepted and the account was not: banned, or removed.
  // Neither is about a field, and both are answers to this submit, so both go
  // where every other answer to it goes — the strip under the password. Only
  // one of the two has anywhere to go next, so only one swaps the link in the
  // row above the strip.
  const refusal = readAccountRefusal(failure);
  if (refusal) {
    accountRefusal.value = refusal;
    removedAccount.value = isRemoved(failure);
    return;
  }

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

  // Nothing named a field, and the two refusals about the account itself are
  // answered above: what is left is the lockout after too many wrong passwords
  // and whatever else the server may name. The sentence goes under the password
  // field, where the wrong-password sentence goes, because it answers the same
  // submit. A 403 is ours to say — accountApi takes it back from the response
  // interceptor — and so is a dead server: the plate the interceptor held for
  // it is taken down (refusalHere). An expired session and a dead connection
  // stay with the plate.
  const refused = failure.status === 403;
  const said = refused
    ? describeFailure(failure, NO_ANSWER)
    : refusalHere(failure, NO_ANSWER);
  if (!emailError && !passwordError && said) {
    passwordField.setError(asSaidHere(failure, said));
  }
};

const submitSecondFactor = async () => {
  // Half a code is not an attempt, and a second attempt while the first is in
  // flight would spend one of the few the server allows. Neither is a code
  // typed during a wait the server imposed, nor one typed after the account
  // itself was refused: the check starts by itself here, so without this gate
  // the field would keep sending into a shut door.
  if (
    loading.value ||
    !code.isComplete.value ||
    rateLimit.waiting.value ||
    accountRefusal.value
  ) {
    return;
  }

  loading.value = true;
  codeError.value = "";
  const failure = await completeSecondFactor(code.normalized.value);
  loading.value = false;

  if (!failure) {
    emit("success");
    return;
  }

  // The limit answers in the strip, as it does on the first step, and the code
  // is left in the field: there is nothing to retype, only a wait to sit out.
  if (rateLimit.take(failure)) return;

  // The account itself, refused in the minutes between the two factors. The
  // API can answer this step with exactly two such refusals — a ban and a
  // removal, both thrown by CompleteTwoFactor — and they are the same two the
  // password step throws, said here in the same words and in the same place.
  // Nothing else about the account can arrive: an unfinished registration and
  // a wrong password never reach a challenge, and a lockout met between the
  // factors comes back as a refused code.
  const refusal = readAccountRefusal(failure);
  if (refusal) {
    accountRefusal.value = refusal;
    removedAccount.value = isRemoved(failure);
    return;
  }

  // Every way of failing the code itself answers with one sentence: a wrong
  // code, an expired challenge, a recovery code already spent. A 403 that is
  // not one of the two above has no shape the server documents, and the
  // request took the status back from the interceptor, so saying nothing here
  // would leave the screen silent. A dead server is said here too, its plate
  // taken down; an expired session and a dead connection stay with the plate.
  const said =
    failure.status === 403
      ? describeFailure(failure, NO_ANSWER)
      : refusalHere(failure, NO_ANSWER);
  if (said) codeError.value = asSaidHere(failure, said);

  // A code that did not pass is selected whole: the next one is typed over it,
  // and there is nothing to clear by hand first.
  await nextTick();
  code.inputRef.value?.focus();
  code.inputRef.value?.select();
};

const onCodeInput = () => {
  code.onInput();
  codeError.value = "";
  // The check starts by itself on a complete code, the way every second factor
  // on the web behaves: after the last symbol there is nothing left to decide,
  // and reaching for a button is a step that only costs time. The button stays
  // for the entry that arrived some other way.
  //
  // Except when the field had to throw symbols away to get there. A recovery
  // code pasted into the digit field, a line of a letter, a password with
  // digits in it: each of those leaves a field that looks full and holds
  // something nobody meant to send, and a check started over it spends one of
  // the few attempts the server allows. Then the button is the one who says
  // when, and it stays enabled, because what he typed is still his to send.
  //
  // Which is why the field carries no maxlength. The browser applies that
  // before an event reaches this screen, so a pasted recovery code arrived
  // already cut to the length of the digit field, the parse read a fragment
  // instead of a code, and nothing a person could paste reached the branch
  // below. The length is kept by the parse, which knows both codes.
  if (code.isComplete.value && !code.droppedSymbols.value) {
    void submitSecondFactor();
  }
};

const swapCodeKind = async () => {
  recoveryMode.value = !recoveryMode.value;
  // The field starts empty in the new kind: what stood in it was a code of the
  // other one, and rewriting it under the new rules leaves a fragment nobody
  // entered. The refusal belonged to that code and goes with it, and no check
  // is started here: the reader pressed a switch, not "Подтвердить".
  code.reset();
  codeError.value = "";
  await nextTick();
  code.inputRef.value?.focus();
};

/** What the strip says when the letter did not go out. One sentence, no stop. */
const RESEND_FAILED = "Не удалось отправить письмо";

/** A letter already on the wire, so a second press buys no second letter. */
const resending = ref(false);

/**
 * Send the activation letter again, from here.
 *
 * The link used to hand the reader to the recovery card, which asked him for
 * the address standing in the field right above it and then sent this very
 * request. One press, one letter, and the card states the outcome itself.
 *
 * The receipt is put up only on the answer that says the letter went out. Any
 * other one means the address stopped being an unfinished registration between
 * the refusal and this press, and a receipt over that is a promise of a letter
 * nobody sent.
 */
const handleResendActivation = async () => {
  if (resending.value || rateLimit.waiting.value) return;

  resending.value = true;
  // The refusal goes into the strip under the fields and the wait onto the
  // button, so both are claimed from the interceptor here, at the press: the
  // same endpoint is pressed by the activation page, which shows neither and
  // needs the interceptor to keep speaking for it.
  const { data, error } = await accountApi.recover(
    emailField.value.value.trim(),
    { ownsRefusal: true, ownsRateLimit: true },
  );
  resending.value = false;

  if (error) {
    if (rateLimit.take(error)) return;
    passwordField.setError(describeFailure(error, RESEND_FAILED));
    return;
  }

  if (data?.status !== "ActivationResent") {
    passwordField.setError(RESEND_FAILED);
    return;
  }

  step.value = "activationResent";
};

/**
 * What the dialog takes with it on its way out.
 *
 * The wait, because the modal keeps this component mounted and an interval left
 * running would tick on behind a screen nobody is looking at and shut the button
 * of the next visit. And the receipt, for the same reason: a card left standing
 * on it would open the next visit on a letter sent to an address this reader
 * never typed. The steps of the login itself stay where they are — closing the
 * dialog does not end a challenge the server is still holding.
 */
const onBeforeClose = () => {
  rateLimit.clear();
  if (step.value === "activationResent") step.value = "credentials";
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
  // A refusal about an account is about the account that was named, and the
  // name is being changed.
  accountRefusal.value = null;
  removedAccount.value = false;
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
  <!-- The receipt needs a narrower card than the sign-in did: two lines and a
       way out, against a card sized for two labelled fields. Both words hang
       off the one state, so the card cannot narrow while a field still stands
       in it. -->
  <AuthDialog
    :compact="step === 'activationResent'"
    :center="step === 'activationResent'"
    @close="emit('cancel')"
    @before-close="onBeforeClose"
  >
    <!-- The letter is out, and nothing is left to press: the cross is the way
         out of a receipt. -->
    <template v-if="step === 'activationResent'">
      <h2 class="auth-title">Проверьте почту</h2>

      <p class="auth-text">
        Мы отправили повторное письмо со ссылкой для активации на
        <strong>{{ emailField.value.value.trim() }}</strong>
      </p>
      <p class="auth-note">Ссылка действительна 48 часов</p>
    </template>

    <template v-else-if="step === 'secondFactor'">
      <h2 class="auth-title">Подтверждение входа</h2>

      <form @submit.prevent="submitSecondFactor">
        <!-- Which code is being typed is said by the label, and the other kind
             is offered beside it, in the label row every other field of the
             surface wears. The answer decides what the field accepts, so it is
             asked before the field is reached.
             An account removed between the two factors takes the row over: no
             code opens it, so the kind of code is no longer a question, and the
             one thing left to offer is the way back. -->
        <div class="auth-label-row">
          <label class="auth-label" for="two-factor-code">{{ codeName }}</label>
          <span class="copy-space">{{ " " }}</span>
          <button
            v-if="!removedAccount"
            type="button"
            class="auth-action"
            @click="swapCodeKind"
          >
            {{ otherCodeAction }}
          </button>
          <button
            v-else
            type="button"
            class="auth-action"
            @click="goToRestoration"
          >
            Восстановить аккаунт
          </button>
        </div>
        <div class="auth-field">
          <!-- Bound with :value and @input rather than v-model: the caret is
               placed by the composable, and v-model would write the value back
               a render later, taking the caret to the end with it. -->
          <input
            id="two-factor-code"
            :ref="code.inputRef"
            class="auth-input--code"
            :class="{ 'auth-input--code-recovery': recoveryMode }"
            type="text"
            :inputmode="recoveryMode ? 'text' : 'numeric'"
            autocomplete="one-time-code"
            :autocapitalize="recoveryMode ? 'characters' : 'off'"
            autocorrect="off"
            spellcheck="false"
            enterkeyhint="go"
            aria-describedby="two-factor-code-message"
            :aria-invalid="codeError ? 'true' : undefined"
            :value="code.value.value"
            @input="onCodeInput"
            @keydown="code.onKeydown"
          />
        </div>
        <!-- The strip says what the field is waiting for and gives the place
             up to the server's refusal when one arrives, so the card does not
             change height either way. -->
        <div
          id="two-factor-code-message"
          class="auth-message auth-message--reserved"
          :class="{ 'auth-message--error': !!codeMessage }"
          aria-live="polite"
        >
          {{ codeMessage || codeHint }}
        </div>

        <!-- The caption names the act and keeps naming it while the check is
             out. Waiting is carried by the state the whole site carries it
             with: the control stops answering, wears the disabled hatch and
             says aria-busy. It is not carried by a second wording that remakes
             button under the reader's aim (UI_STANDARDS, "Ожидание запроса"). -->
        <Button
          class="auth-button"
          variant="primary"
          :disabled="
            !code.isComplete.value ||
            rateLimit.waiting.value ||
            !!accountRefusal
          "
          :loading="loading"
          >Подтвердить</Button
        >
      </form>

      <!-- The one way out of this step, for the reader who has neither the
           phone nor the codes. The two kinds of code are chosen above, so the
           foot of the card carries nothing else. -->
      <p class="auth-footnote">
        Нет доступа к приложению и резервным кодам?
        <button
          type="button"
          class="auth-action"
          @click="emit('cantPassSecondFactor')"
        >
          Отключить двухфакторную аутентификацию
        </button>
      </p>
    </template>

    <template v-else>
      <h2 class="auth-title">Войти</h2>

      <form @submit.prevent="submit">
        <label class="auth-label" for="email">Почта</label>
        <div class="auth-field">
          <input
            id="email"
            ref="emailInput"
            v-model="emailField.value.value"
            type="email"
            autocomplete="email"
            aria-describedby="login-email-message"
            :aria-invalid="emailField.error.value ? 'true' : undefined"
            @input="onEmailInput"
            @blur="emailField.onBlur"
          />
        </div>
        <!-- Rendered empty as well as filled: the strip holds the space, so
             nothing below moves when the server answers. -->
        <div
          id="login-email-message"
          class="auth-message"
          :class="{ 'auth-message--error': !!emailField.error.value }"
          role="alert"
        >
          {{ emailField.error.value }}
        </div>

        <div class="auth-label-row">
          <label class="auth-label" for="password">Пароль</label>
          <!-- One of the three buttons below always renders, so the space that
               keeps the row on one line in a copy is unconditional. Each of
               them answers the strip under the field: an unfinished
               registration asks for the letter again, a removed account asks
               moderation to bring it back, and everything else is the standing
               way out. -->
          <span class="copy-space">{{ " " }}</span>
          <button
            v-if="pendingActivation"
            type="button"
            class="auth-action"
            @click="handleResendActivation"
          >
            Отправить письмо повторно
          </button>
          <button
            v-else-if="removedAccount"
            type="button"
            class="auth-action"
            @click="goToRestoration"
          >
            Восстановить аккаунт
          </button>
          <button
            v-else
            type="button"
            class="auth-action"
            @click="emit('cantSignIn', emailField.value.value.trim())"
          >
            Не могу войти
          </button>
        </div>
        <div class="auth-field">
          <PasswordInput
            id="password"
            ref="passwordInput"
            v-model="passwordField.value.value"
            autocomplete="current-password"
            aria-describedby="login-password-message"
            :aria-invalid="passwordField.error.value ? 'true' : undefined"
            @input="onPasswordInput"
            @blur="passwordField.onBlur"
          />
        </div>
        <div
          id="login-password-message"
          class="auth-message"
          :class="{ 'auth-message--error': !!passwordMessage }"
          role="alert"
        >
          {{ passwordMessage }}
        </div>

        <div class="auth-checkline">
          <input id="rememberMe" v-model="rememberMe" type="checkbox" />
          <label for="rememberMe">Запомнить меня</label>
        </div>

        <Button
          class="auth-button"
          variant="primary"
          :disabled="!canSubmit || rateLimit.waiting.value"
          :loading="loading"
          >Войти</Button
        >

        <!-- Honeypot field for bot protection -->
        <input
          name="website"
          v-model="honeypot"
          class="honeypot-field"
          autocomplete="off"
          tabindex="-1"
          aria-hidden="true"
        />
      </form>

      <!-- The way out for a reader who has no account yet. It stands in the
           foot of the card, under the rule, where every other card of the
           surface keeps its one secondary offer. -->
      <p class="auth-footnote">
        Еще нет аккаунта?
        <button type="button" class="auth-action" @click="emit('register')">
          Зарегистрироваться
        </button>
      </p>
    </template>
  </AuthDialog>
</template>

<!-- No styles of its own. The secondary actions of this form are the
     link-shaped control of the guest surface, and that control is drawn once,
     in AuthSurface.sass, under .auth-action: a recipe copied into a scoped
     block is a copy that drifts, and it is emitted again into every sheet that
     copies it. -->

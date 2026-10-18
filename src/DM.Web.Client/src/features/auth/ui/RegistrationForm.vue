<script setup lang="ts">
import {
  ref,
  computed,
  watch,
  onMounted,
  onBeforeUnmount,
  nextTick,
} from "vue";
import { useRouter } from "vue-router";
import type { RegisterCredentials } from "@/shared/api/models/account";

import { useNewPasswordField } from "@/shared/lib/composables/useNewPasswordField";
import {
  useValidatedField,
  validators,
} from "@/shared/lib/composables/useValidatedField";
import { AuthDialog } from "@/shared/ui/Layout";
import { Button } from "@/shared/ui/Button";
import {
  PasswordInput,
  PasswordStrengthIndicator,
} from "@/shared/ui/PasswordInput";
import { Tooltip } from "@/shared/ui/Tooltip";
import { accountApi, register } from "@/entities/user";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";
import { describeFailure, refusalHere } from "@/shared/lib/errors";
import { useRateLimitNotice } from "@/shared/lib/composables/useRateLimitNotice";
import { supportFor } from "@/entities/ticket";

const router = useRouter();

const emit = defineEmits<{
  (e: "success", email: string): void;
  (e: "cancel"): void;
  (e: "openRecovery", email?: string): void;
  (e: "openLogin", email?: string): void;
}>();

// Step management
const step = ref<"email" | "password">("email");
const loading = ref(false);
const honeypot = ref("");
const formLoadTime = ref(0);
const emailInputRef = ref<HTMLInputElement | null>(null);
const passwordInputRef = ref<InstanceType<typeof PasswordInput> | null>(null);
// The rules checkbox unlocks only after the rules link was actually opened.
// The agreement and the privacy policy are not a second checkbox: they are
// accepted by registering, and the footnote under the button says so — a
// consent nobody can withhold is not a choice, and drawing it as one asks the
// reader to make a decision that has already been made for him.
const rulesViewed = ref(false);
const acceptedRules = ref(false);

// The server's own ceiling for an address: UserRegistrationValidator refuses a
// longer one, and the column behind it is the same 100. The field is capped at
// it rather than letting the reader type past it and buying a refusal for
// something nothing but the field could have prevented.
const EMAIL_MAX_LENGTH = 100;

// The address is held by a live account. One sentence for one state, wherever
// it is learned: the check on blur and the server's refusal on the second step
// are the same answer, and two wordings for it read as two different problems.
const EMAIL_TAKEN_MESSAGE = "Аккаунт с этой почтой уже существует";

// Email conflict states (for showing action links). Two of them, not three: a
// removed account's address answers as a live one's. "удален" is a decision
// about an account, and a guest at this form is not owed it; the owner of a
// removed account is shown the way to support after a correct password on the
// sign-in card, or by the recovery letter.
const emailTaken = ref(false);
const emailPending = ref(false);

// The availability check that leaving the field started, while it is still on
// the wire. The press that follows the blur waits for this answer instead of
// asking the server the same question a second time. Typing drops it: a check
// of the previous address says nothing about the one now in the field.
const emailCheck = ref<Promise<void> | null>(null);

// The two answers about the address, dropped together. Each of them carries a
// link in the label row, and both links are an offer about the address that was
// asked about: "Войти" hands it to the sign-in card, and "Отправить письмо
// повторно" asks for the letter again. Left standing over an address the reader
// has started rewriting, the link takes the old one away with it.
const clearEmailVerdict = () => {
  emailTaken.value = false;
  emailPending.value = false;
};

/**
 * The rate limit, when this card runs into one, shown by the card itself.
 *
 * One notice for both steps: the address check and the registration sit on the
 * same budget, and the first of them goes out on every blur, so this is where
 * the limit is usually met first.
 */
const rateLimit = useRateLimitNotice();

// Email field with async availability check
const emailField = useValidatedField({
  validate: validators.combine(validators.required(), validators.email()),
  asyncValidate: async (value) => {
    const { data, error } = await accountApi.checkEmail(value);
    // The limit belongs in the strip under the field, not in the field's own
    // sentence: a field error is cleared by the next keystroke, and the wait is
    // not over because somebody typed. Everything else this check can fail with
    // stays silent, as it was: the address is checked again on submit.
    if (error) {
      rateLimit.take(error);
      return null;
    }
    if (!data) return null; // Silent fail

    if (data.isAvailable) {
      emailTaken.value = false;
      emailPending.value = false;
      return null;
    } else if (data.reason === "PendingActivation") {
      emailTaken.value = false;
      emailPending.value = true;
      return "Регистрация не завершена";
    } else {
      // Everything else is "taken", a removed account's address included: the
      // server no longer tells this form that an account was removed, and a
      // value the wire no longer carries is not given a branch of its own.
      emailTaken.value = true;
      emailPending.value = false;
      return EMAIL_TAKEN_MESSAGE;
    }
  },
});

// Password field with HIBP check
const {
  password: newPassword,
  isValid: isPasswordValid,
  reset: resetPassword,
} = useNewPasswordField();

// What the server said about this password — a breach, above all else. It
// stands in the line of the strength scale instead of the step, because it is
// an answer about this password and not about the form, and it holds the
// button shut until a different password is typed.
const passwordRefusal = ref("");

// A failure that named no field: the rate limit, a letter the mail server would
// not take. It has nowhere to point, so it takes the line of the scale, the
// card's one place for a message about this step. The bar turns red with it:
// a red line over a half-amber bar read as two different answers at once.
const formError = ref("");

// The button is live as soon as what is typed looks like an address. Whether
// that address is free is asked on the press, not before it: a button that
// waits for the check to come back swallows the first press and asks for a
// second one that does exactly what the first would have done. It shuts on the
// only thing pressing again cannot change — a refusal already on screen.
const canSubmitEmail = computed(
  () =>
    emailField.isValid.value &&
    !emailField.error.value &&
    !emailTaken.value &&
    !emailPending.value,
);

/**
 * The strip under the address, and the line above the button on the second
 * step. The rate limit wins both: it is the newest answer, it is the reason the
 * button is shut, and its number keeps moving.
 */
const emailMessage = computed(
  () => rateLimit.message.value || emailField.error.value,
);

const formMessage = computed(() => rateLimit.message.value || formError.value);

// Can register: the password passes what the field can judge, the server has
// not refused this one, and the rules are accepted.
const canSubmitPassword = computed(
  () => isPasswordValid.value && !passwordRefusal.value && acceptedRules.value,
);

// A refusal is about the password that earned it. The moment it is edited the
// server has not seen what is in the field, so the line goes back to the scale.
watch(newPassword, () => {
  if (passwordRefusal.value) passwordRefusal.value = "";
  if (formError.value) formError.value = "";
});

onMounted(() => {
  formLoadTime.value = Date.now();
  nextTick(() => emailInputRef.value?.focus());
});

onBeforeUnmount(() => {
  clearForm();
});

const goToSupport = () => {
  emit("cancel");
  nextTick(() => router.push(supportFor("AccessRecovery")));
};

/** The receipt takes the card once the second letter is out. */
const activationResent = ref(false);

/** A letter already on the wire, so a second press buys no second letter. */
const resending = ref(false);

/** What the strip says when the letter did not go out. One sentence, no stop. */
const RESEND_FAILED = "Не удалось отправить письмо";

/**
 * Send the activation letter again, for an address whose registration was
 * never finished.
 *
 * The link used to open the recovery card, which asked for the address already
 * standing in the field beside it and then sent this very request. One press,
 * one letter, and the card states the outcome itself.
 *
 * The receipt is put up only on the answer that says the letter went out: any
 * other one means the address stopped being an unfinished registration between
 * the check and this press, and a receipt over that promises a letter nobody
 * sent.
 */
const resendActivation = async () => {
  if (resending.value || rateLimit.waiting.value) return;

  resending.value = true;
  // The refusal goes into the strip under the address and the wait onto the
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
    emailField.setError(describeFailure(error, RESEND_FAILED));
    return;
  }

  if (data?.status !== "ActivationResent") {
    emailField.setError(RESEND_FAILED);
    return;
  }

  activationResent.value = true;
};

// Step 1: Submit email
const submitEmail = async () => {
  // Only a press on top of one already being served is dropped: Enter sits on
  // the field itself, so it reaches this without passing the button, and a held
  // key repeats the keydown. A press that lands while the check is on the wire
  // is not dropped — it is what the wait below is for.
  if (loading.value || rateLimit.waiting.value) return;

  loading.value = true;
  try {
    // Clicking the button blurs the field, which starts the check, and the
    // click itself arrives while it is still flying. The press waits for that
    // answer instead of sending a second request for the same address.
    if (emailCheck.value) await emailCheck.value;

    // The check that just landed refused this address, or the limit refused
    // the check. Either way the press has its answer and stays on this step.
    if (
      emailField.error.value ||
      emailTaken.value ||
      emailPending.value ||
      rateLimit.waiting.value
    ) {
      return;
    }

    const isValid = await emailField.validate();
    // A limit met here refused the check, not the address, so nothing is known
    // about the address yet. Advancing on that would carry an unchecked one all
    // the way to the letter, and the reader would learn on the last screen.
    if (
      !isValid ||
      emailTaken.value ||
      emailPending.value ||
      rateLimit.waiting.value
    ) {
      return;
    }

    step.value = "password";
    nextTick(() => passwordInputRef.value?.focus());
  } finally {
    loading.value = false;
  }
};

// Step 2: Submit registration
const submitPassword = async () => {
  // A registration already in flight is not sent a second time. Held down or
  // hit twice, Enter would buy the reader two activation letters, or two
  // registrations racing each other to the 409.
  if (loading.value || !canSubmitPassword.value || rateLimit.waiting.value) {
    return;
  }

  loading.value = true;
  formError.value = "";
  passwordRefusal.value = "";

  // Bot protection - the rest of the minimum form-fill time is sat out, the
  // way the login sits it out, instead of being answered with a refusal. A
  // password manager fills and submits in well under two seconds, so a refusal
  // here punishes the reader for having one; the honeypot and the server's own
  // limit are what catch a bot.
  const timeSinceLoad = Date.now() - formLoadTime.value;
  if (timeSinceLoad < 2000) {
    await new Promise((resolve) => setTimeout(resolve, 2000 - timeSinceLoad));
  }

  const credentials: RegisterCredentials = {
    email: emailField.value.value.trim(),
    password: newPassword.value,
    acceptedRules: acceptedRules.value,
    website: honeypot.value,
  };

  const failure = await register(credentials);
  loading.value = false;

  if (!failure) {
    sessionStorage.setItem("dm_pending_email", emailField.value.value.trim());
    emit("success", emailField.value.value.trim());
    return;
  }

  // The next screen promises a letter, so every refusal has to stop here.
  // This one stops the button too, for as long as the server asked.
  if (rateLimit.take(failure)) return;

  const errors = parseApiErrors(failure);
  // The shared vocabulary reads "Taken" as "Уже занято", which is as true of a
  // name as of an address and names neither. The field here is the address of
  // an account being opened, so the card answers in its own words, and in the
  // same ones the check on blur uses: it is the same state, learned later.
  //
  // And answered the same way down to the offer beside the label. The sentence
  // alone left the reader told that the address is taken and shown no way to
  // the account that holds it, on the one screen where that way is a single
  // click; the flag also shuts the button, which is right here - pressing again
  // earns the same refusal until the address changes, and the first keystroke
  // in the field clears both.
  const emailCodes = errors.email;
  const taken = emailCodes?.[0] === "Taken";
  const emailErr = taken ? EMAIL_TAKEN_MESSAGE : getFieldError(errors, "email");
  if (taken) emailTaken.value = true;
  const passErr = getFieldError(errors, "password");

  if (emailErr) {
    emailField.setError(emailErr);
    step.value = "email";
    nextTick(() => emailInputRef.value?.focus());
    return;
  }

  if (passErr) {
    passwordRefusal.value = passErr;
    return;
  }

  // Nothing named a field: the address is already registered, the letter was
  // not accepted, the attempt hit the rate limit. The next screen is
  // "Проверьте почту", so every one of them has to stop here — that screen
  // promises a letter, and the reader waits for it. The refusals the
  // interceptor speaks for are said here too, its plate taken down; an
  // expired session and a dead connection stay with the plate.
  const said = refusalHere(
    failure,
    "Не удалось выполнить запрос, попробуйте позже",
  );
  if (said) formError.value = said;
};

// Leaving the field asks whether the address is free, and the answer is held
// on to: the press that follows waits for it rather than asking again.
const onEmailBlur = () => {
  const asked = emailField.value.value.trim();
  const check = emailField.onBlur().then(() => {
    // The answer is about the address that was in the field when it was left.
    // A reader who went back and kept typing has already been answered by the
    // form — the red went out on his first character — and an answer landing
    // after that lights it again under his hand, about an address that is no
    // longer there.
    if (emailField.value.value.trim() !== asked) {
      clearEmailVerdict();
      emailField.onInput();
    }
  });
  emailCheck.value = check;
  void check.finally(() => {
    if (emailCheck.value === check) emailCheck.value = null;
  });
};

// Reset email conflict on input
const onEmailInput = () => {
  emailField.onInput();
  // A check of the previous address answers nothing about the one now being
  // typed, so the press must not wait for it.
  emailCheck.value = null;
  clearEmailVerdict();
};

const clearForm = () => {
  rateLimit.clear();
  emailCheck.value = null;
  emailField.reset();
  resetPassword();
  passwordRefusal.value = "";
  formError.value = "";
  clearEmailVerdict();
  rulesViewed.value = false;
  acceptedRules.value = false;
  step.value = "email";
  // The receipt goes with the rest: the modal keeps this component mounted, so
  // a card left on the receipt would open the next visit on a letter sent to
  // somebody else's address.
  activationResent.value = false;
};

const cancel = () => {
  clearForm();
  emit("cancel");
};

const goBack = () => {
  step.value = "email";
  nextTick(() => emailInputRef.value?.focus());
};

const handleLogin = () => {
  emit("openLogin", emailField.value.value.trim() || undefined);
};

const handleRecovery = () => {
  emit("openRecovery", emailField.value.value.trim() || undefined);
};
</script>

<template>
  <!-- The receipt needs a narrower card than the registration did: two lines
       and a way out, against a card sized for a labelled field. Both words hang
       off the one state, so the card cannot narrow while a field still stands
       in it. -->
  <AuthDialog
    :compact="activationResent"
    :center="activationResent"
    @close="cancel"
    @before-close="clearForm"
  >
    <!-- The letter is out, and nothing is left to press: the cross is the way
         out of a receipt. -->
    <template v-if="activationResent">
      <h2 class="auth-title">Проверьте почту</h2>

      <p class="auth-text">
        Мы отправили повторное письмо со ссылкой для активации на
        <strong>{{ emailField.value.value.trim() }}</strong>
      </p>
      <p class="auth-note">Ссылка действительна 48 часов</p>
    </template>

    <template v-else>
      <h2 class="auth-title">Регистрация</h2>

      <!-- Step 1: Email -->
      <form v-if="step === 'email'" novalidate @submit.prevent="submitEmail">
        <div class="auth-warnbox auth-warnbox--notice">
          <b>Создание дополнительных аккаунтов запрещено</b>
          Если вы утратили доступ к аккаунту, воспользуйтесь
          <button type="button" class="auth-action" @click="handleRecovery">
            восстановлением доступа
          </button>
          или обратитесь в
          <button type="button" class="auth-action" @click="goToSupport">
            поддержку
          </button>
        </div>

        <div class="auth-label-row">
          <label class="auth-label" for="registration-email">Почта</label>
          <!-- The space that keeps the row on one line in a copy (Reset.sass,
               .copy-space). Under the same condition as the pair below it: most
               of the time this row is a label and nothing else, and a space
               after it would be a space at the end of the copy. -->
          <span v-if="emailTaken || emailPending" class="copy-space">{{
            " "
          }}</span>
          <button
            v-if="emailTaken"
            type="button"
            class="auth-action"
            @click="handleLogin"
          >
            Войти
          </button>
          <button
            v-else-if="emailPending"
            type="button"
            class="auth-action"
            @click="resendActivation"
          >
            Отправить письмо повторно
          </button>
        </div>
        <div class="auth-field">
          <!-- Enter stays on this field rather than going through the button:
               submitEmail is the one place that runs the check, waits for it and
               holds the gate against a repeat, and Enter reaches it directly. -->
          <input
            id="registration-email"
            ref="emailInputRef"
            v-model="emailField.value.value"
            type="email"
            autocomplete="email"
            :maxlength="EMAIL_MAX_LENGTH"
            title=""
            aria-describedby="registration-email-message"
            :aria-invalid="emailField.error.value ? 'true' : undefined"
            @input="onEmailInput"
            @blur="onEmailBlur"
            @keydown.enter.prevent="submitEmail"
          />
        </div>
        <!--
          The strip under the field is rendered whether it says anything or not:
          the button below it stands in one place, and the card does not change
          height the moment the address turns out to be taken.
        -->
        <div
          id="registration-email-message"
          class="auth-message"
          :class="{ 'auth-message--error': !!emailMessage }"
          role="alert"
        >
          {{ emailMessage }}
        </div>

        <input
          name="website"
          v-model="honeypot"
          class="honeypot-field"
          autocomplete="off"
          tabindex="-1"
          aria-hidden="true"
        />

        <Button
          class="auth-button"
          variant="primary"
          :disabled="!canSubmitEmail || rateLimit.waiting.value"
          :loading="loading"
        >
          Продолжить
        </Button>
      </form>

      <!-- Step 2: Password -->
      <form v-else novalidate @submit.prevent="submitPassword">
        <div class="auth-label-row">
          <span class="auth-label">Почта</span>
          <span class="copy-space">{{ " " }}</span>
          <button type="button" class="auth-action" @click="goBack">
            Изменить
          </button>
        </div>
        <div class="auth-value">{{ emailField.value.value }}</div>

        <label class="auth-label" for="registration-password">Пароль</label>
        <div class="auth-field">
          <!-- No Enter handler of its own: the field is in the form, so Enter
               submits it through the button, and a button the form has disabled
               stops the press instead of letting it past. -->
          <PasswordInput
            id="registration-password"
            ref="passwordInputRef"
            v-model="newPassword"
            autocomplete="new-password"
            aria-describedby="registration-password-message"
            :aria-invalid="passwordRefusal ? 'true' : undefined"
          />
        </div>
        <!--
          The scale takes the strip under the field, and it stands there from the
          start: the minimum is written in its line, and a rule that appears only
          after the reader has broken it is not a rule he was given. It is also
          what the field points at, so the line is read out with the field and
          not hunted for.
        -->
        <PasswordStrengthIndicator
          id="registration-password-message"
          class="auth-meter"
          :password="newPassword"
          :error="passwordRefusal || formMessage || null"
          always
        />

        <!--
          The tooltip that says why the box is dead belongs to the whole row: a
          disabled control swallows the pointer, and the reader who tries to tick
          it aims at the box or at the words beside it without distinction.
        -->
        <div class="consent">
          <Tooltip
            text="Сначала откройте правила сайта"
            :disabled="rulesViewed"
          >
            <div class="auth-checkline auth-checkline--top">
              <input
                id="registration-rules"
                v-model="acceptedRules"
                type="checkbox"
                :disabled="!rulesViewed"
              />
              <span>
                <label for="registration-rules"
                  >Я принимаю
                  <a
                    href="/rules"
                    target="_blank"
                    rel="noopener"
                    @click="rulesViewed = true"
                    >правила сайта</a
                  ></label
                >
              </span>
            </div>
          </Tooltip>
        </div>

        <input
          name="website"
          v-model="honeypot"
          class="honeypot-field"
          autocomplete="off"
          tabindex="-1"
          aria-hidden="true"
        />

        <Button
          class="auth-button"
          variant="primary"
          :disabled="!canSubmitPassword || rateLimit.waiting.value"
          :loading="loading"
        >
          Создать аккаунт
        </Button>

        <p class="auth-footnote">
          Регистрируясь, вы принимаете
          <a href="/agreement" target="_blank" rel="noopener"
            >пользовательское соглашение</a
          >
          и
          <a href="/privacy" target="_blank" rel="noopener"
            >политику конфиденциальности</a
          >
        </p>
      </form>
    </template>
  </AuthDialog>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

// The card's secondary actions wear .auth-action, which the guest surface
// paints for every screen at once. This file used to carry its own copy of that
// recipe, and a copy is a second opinion: the login card had one too, and the
// two drifted.

// The tooltip trigger is an inline span by default, and the consent row inside
// it is a block that hangs its own indent: inside an inline trigger the row
// would take the width of its own words instead of the card's, and the indent
// that holds a wrapped line clear of the box would have nothing to measure.
.consent :deep(.tooltip-trigger)
  display: block

// A disabled control swallows the pointer, and every mouse event over it — the
// hover the tooltip listens for included. Letting the pointer through hands
// both the hover and the click to the row, which is where the explanation
// lives. The hatch of +disabled-hatch keeps drawing: it is a layer, not a
// behaviour.
.auth-checkline input[type="checkbox"]:disabled
  pointer-events: none
</style>

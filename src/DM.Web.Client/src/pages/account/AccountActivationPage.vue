<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, nextTick } from "vue";
import { useRouter } from "vue-router";
import { readConfirmationToken } from "@/shared/lib/utils/confirmationToken";
import { deviceTimeZone } from "@/shared/lib/utils/timeZone";
import { useAuthStore, UsernameInput, accountApi } from "@/entities/user";

import type { User } from "@/shared/api/models/community";
import Button from "@/shared/ui/Button/Button.vue";
import { symbols } from "@/shared/lib/utils/icons";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";
import { unwrapResource } from "@/shared/api";
import { LETTER_NOT_SENT } from "@/shared/lib/errors";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { supportFor } from "@/entities/ticket";

// State machine for activation flow
type ActivationPhase =
  | "loading"
  | "selectUsername"
  | "confirmUsername"
  | "expired"
  | "notFound"
  | "submitting"
  | "success";

/**
 * The one sentence for a name that is not to be had, word for word the one
 * UsernameInput prints under the field.
 *
 * The server answers in codes, and the shared vocabulary reads `Taken` as
 * "Уже занято" - a third wording for the thing the reader has already been
 * told in the field's own line. Every other code keeps the vocabulary's
 * sentence: those are about the characters, not about the owner.
 */
const NAME_UNAVAILABLE = "Это имя занято";

const router = useRouter();
const userStore = useAuthStore();

// State
const phase = ref<ActivationPhase>("loading");

/**
 * Whether the other tab has already finished the activation.
 *
 * Read through a function on purpose: both callers assign a literal phase
 * before they await, and a comparison written inline there is narrowed by the
 * compiler against that literal - it does not know the channel can move the
 * phase while the request is on the wire.
 */
const finishedElsewhere = (): boolean => phase.value === "success";
const pendingEmail = ref<string>("");
const token = ref<string>("");
const username = ref("");
const usernameAvailable = ref(false);
/**
 * The name the finished screen greets, and the whole of what that screen reads.
 *
 * The tab that ran the activation takes it out of the account the server sent
 * back; the tab that only heard about the activation over the channel has the
 * name and nothing else - and needs nothing else. The account itself reaches
 * that tab without this page: `updateUser` writes it to localStorage, and the
 * auth store of every other tab listens for that write.
 */
const activatedName = ref("");
const error = ref<string | null>(null);

// Resend state
const resendEmail = ref("");
const resendLoading = ref(false);
const resendSuccess = ref(false);

// The block the name field stands in. The field itself belongs to a shared
// component, so the caret is placed through the wrapper this page owns rather
// than through a handle bolted onto UsernameInput for one caller.
const nameBlock = ref<HTMLElement | null>(null);

// BroadcastChannel for cross-tab sync
let channel: BroadcastChannel | null = null;

// Check if username is valid to proceed to confirmation
const canProceed = computed(() => {
  return (
    phase.value === "selectUsername" &&
    username.value.length >= 2 &&
    username.value.length <= 20 &&
    usernameAvailable.value
  );
});

// Check if ready to submit (on confirmation step)
const canSubmit = computed(() => {
  return phase.value === "confirmUsername";
});

onMounted(async () => {
  // The value arrives in the fragment and is cleared from the address as it is
  // read: in the path it went into browser history and into the access log of
  // the edge, for a value that is the single factor of the confirmation.
  token.value = readConfirmationToken();

  // No line of our own about what is missing: the card already says the link is
  // not a link, and the only word this page had for the missing value was the
  // name of the thing in the database. `error` carries the server's own
  // sentence and nothing else.
  if (!token.value) {
    phase.value = "notFound";
    return;
  }

  // Setup BroadcastChannel for cross-tab sync
  if (typeof BroadcastChannel !== "undefined") {
    channel = new BroadcastChannel("dm_activation");
    channel.onmessage = (event) => {
      // The letter opened in two tabs, and one of them got there first. This
      // one used to be carried off to the home page and told about it in a
      // toast, over a card that has a finished screen of its own and a message
      // that carries the name to put on it. So both tabs end where the
      // activation ends: on the same card, in the same words.
      //
      // A message with no name in it is not that screen, and there is nothing
      // honest to stand in for the name, so the tab stays where it is: its own
      // attempt comes back from the server as a link that opens nothing.
      const name: unknown = event.data?.username;
      if (
        event.data?.type === "activated" &&
        typeof name === "string" &&
        name
      ) {
        activatedName.value = name;
        phase.value = "success";
      }
    };
  }

  // Try to get saved email from registration
  const savedEmail = sessionStorage.getItem("dm_pending_email");
  if (savedEmail) {
    resendEmail.value = savedEmail;
  }

  // Check token status
  await checkTokenStatus();

  // The screen holds one field, and the reader arrived here to fill it in. The
  // check has to answer first, so the field is a render away from the mount and
  // cannot be focused from the mount itself.
  if (phase.value === "selectUsername") await focusName();
});

async function focusName() {
  await nextTick();
  nameBlock.value?.querySelector("input")?.focus();
}

onUnmounted(() => {
  channel?.close();
});

async function checkTokenStatus() {
  phase.value = "loading";

  const { data, error: apiError } = await accountApi.getActivationInfo(
    token.value,
  );

  // The other tab finished while this check was on the wire. The finished
  // screen is the last word of the flow, and a verdict on a link that is spent
  // by now must not take it off the card.
  if (finishedElsewhere()) return;

  if (apiError) {
    // 404 - token not found
    phase.value = "notFound";
    return;
  }

  if (data) {
    pendingEmail.value = data.email;
    resendEmail.value = data.email;

    if (data.status === "ready") {
      phase.value = "selectUsername";
    } else if (data.status === "expired") {
      phase.value = "expired";
    }
  }
}

function proceedToConfirm() {
  if (!canProceed.value) return;
  phase.value = "confirmUsername";
}

function goBackToSelect() {
  phase.value = "selectUsername";
}

async function submitActivation() {
  if (!canSubmit.value) return;

  phase.value = "submitting";

  // The account is created here, and it starts in the zone of the device it
  // is finished on: the site shows a signed-in reader's time in the zone of
  // the account, and an account without one is UTC. Left out where the
  // browser will not say.
  const { data, error: apiError } = await accountApi.activate(token.value, {
    username: username.value,
    timeZoneId: deviceTimeZone() || undefined,
  });

  // As in checkTokenStatus: the other tab may have finished while this attempt
  // was on the wire, and the refusal it earns for a token already spent would
  // replace the news of the activation with a dead link.
  if (finishedElsewhere()) return;

  if (apiError) {
    const errors = parseApiErrors(apiError);
    // A name that is somebody else's is refused in the sentence the field uses,
    // whichever side of the wire the refusal came from. See NAME_UNAVAILABLE.
    const usernameError = errors["username"]?.includes("Taken")
      ? NAME_UNAVAILABLE
      : getFieldError(errors, "username");

    // Username errors - go back to selection
    if (usernameError) {
      error.value = usernameError;
      phase.value = "selectUsername";
      usernameAvailable.value = false;
      return;
    }

    // Token errors or other errors. What the server said, or nothing: the card
    // names the failure in its own heading, and a sentence invented here to
    // fill the gap says the same thing a second time in worse words.
    error.value = Object.values(errors).flat()[0] ?? null;

    // Check if it might be 410 Gone (expired)
    if (apiError.status === 410) {
      phase.value = "expired";
    } else {
      phase.value = "notFound";
    }
    return;
  }

  // Out of the envelope. Stored as it came, the session held the wrapper
  // instead of the account - and the auth store writes that to localStorage -
  // so right after activation the header greeted a signed-in visitor with no
  // name, the link to the profile led nowhere, and the other tab was told
  // somebody named undefined had activated.
  const activated = unwrapResource<User>(data);
  if (activated) {
    // Success!
    userStore.updateUser(activated);
    activatedName.value = activated.username;
    phase.value = "success";

    // Notify other tabs
    channel?.postMessage({ type: "activated", username: activated.username });

    // Clean up sessionStorage
    sessionStorage.removeItem("dm_pending_email");
  }
}

/**
 * A refused resend, in the line above the button the way this card says its
 * other refusals (.activation-refusal). The offer stays on the card with the
 * button live, so the reader can press again; the plate the interceptor held
 * for the same refusal is taken down.
 */
const resendResult = useActionResult();

async function resend() {
  if (!resendEmail.value) return;

  resendLoading.value = true;
  resendResult.clear();

  try {
    const { error } = await accountApi.recover(resendEmail.value);
    if (error) {
      resendResult.refuse(error, LETTER_NOT_SENT);
      return;
    }
    resendSuccess.value = true;
  } finally {
    resendLoading.value = false;
  }
}

function goToProfile() {
  if (activatedName.value) {
    router.push(`/users/${activatedName.value}`);
  } else {
    router.push("/");
  }
}

// The one way out of a dead link, and the same one on every screen that answers
// one. A card that offers a choice makes the reader decide which door his own
// case fits, and he is the one person who cannot tell: the home page is where
// both doors stand anyway. Owner's decision.
function goHome() {
  router.push("/");
}
</script>

<template>
  <!-- A page and not a dialog: nothing dims the ground behind it, so the card
       carries a frame of its own and there is no cross to close it with. -->
  <div class="auth-surface activation-page">
    <!-- The card says how wide it wants to be, because the sheet cannot work
         that out from what stands inside it. Every answer here is a mark, a
         heading and a line or two, so the narrow measure holds in all of
         them. -->
    <div
      class="auth-card auth-card--framed auth-card--center auth-card--compact"
    >
      <!-- Checking the link. The phase existed and the template had no branch
           for it: someone arriving from the letter on a slow connection saw an
           empty card with "Нужна помощь? Обратитесь в поддержку" under it —
           the signal that something is broken, at the moment nothing is.
           EmailChangePage, the third page of this family, does it this way. -->
      <p v-if="phase === 'loading'" class="auth-note activation-loading">
        Проверяем ссылку...
      </p>

      <!-- Step 1: Username Selection -->
      <template v-else-if="phase === 'selectUsername'">
        <h1 class="auth-title">Выберите имя</h1>

        <!-- The card is centred, the field is not: a label, the box under it
             and the verdict under that read from the left, as everywhere else
             on the site something is filled in. -->
        <div ref="nameBlock" class="activation-name">
          <label class="auth-label" for="activation-username">
            Имя пользователя
          </label>
          <username-input
            id="activation-username"
            v-model="username"
            class="auth-field activation-name__field"
            :class="{ 'activation-name__field--refused': !!error }"
            @availability="usernameAvailable = $event"
          />
        </div>

        <p v-if="error" class="activation-refusal" role="alert">{{ error }}</p>

        <Button
          type="button"
          variant="primary"
          class="auth-button submit-button"
          :disabled="!canProceed"
          @click="proceedToConfirm"
        >
          Продолжить
        </Button>
      </template>

      <!-- Step 2: Confirm Username -->
      <template
        v-else-if="phase === 'confirmUsername' || phase === 'submitting'"
      >
        <h1 class="auth-title">Подтвердите выбор имени</h1>

        <div class="auth-warnbox">
          <b>Будьте внимательны</b>Дальнейшая смена имени возможна лишь в
          исключительных случаях
        </div>

        <div class="auth-label-row">
          <span class="auth-label">Имя пользователя</span>
          <!-- A real space, so the row copies as one line (Reset.sass,
               .copy-space). -->
          <span class="copy-space">{{ " " }}</span>
          <!-- "Изменить" goes back a step in code and nowhere in the address
               bar, so the control is a button, wearing the guest surface's own
               secondary action to read as the <a> it replaces. While the
               request is in flight it is disabled for real: the class it used
               to carry dimmed the link and took the mouse away from it, leaving
               the keyboard free to step back mid-request. -->
          <button
            type="button"
            class="auth-action"
            :disabled="phase === 'submitting'"
            @click="goBackToSelect"
          >
            Изменить
          </button>
        </div>
        <div class="auth-value">{{ username }}</div>

        <p v-if="error" class="activation-refusal" role="alert">{{ error }}</p>

        <Button
          type="button"
          variant="primary"
          class="auth-button submit-button"
          :disabled="!canSubmit"
          :loading="phase === 'submitting'"
          @click="submitActivation"
        >
          Завершить регистрацию
        </Button>
      </template>

      <!-- Success state -->
      <template v-else-if="phase === 'success'">
        <div class="auth-status auth-status--ok" aria-hidden="true">
          {{ symbols.checkmark }}
        </div>
        <h1 class="auth-title">Регистрация завершена</h1>
        <!-- No bang after the name. The heading above already says the whole
             news, and an exclamation mark under it congratulates the reader a
             second time. -->
        <p class="auth-text">
          Добро пожаловать, <b>{{ activatedName }}</b>
        </p>
        <Button
          type="button"
          variant="primary"
          class="auth-button auth-button--spaced"
          @click="goToProfile"
        >
          Перейти в профиль
        </Button>
      </template>

      <!-- Expired state. One attempt from this screen: the offer is replaced by
           the receipt rather than joined by it, so there is no button left to
           press a second time. -->
      <template v-else-if="phase === 'expired' && !resendSuccess">
        <!-- The warning sign by the rule at .auth-status--warn in
             AuthSurface.sass: the site can take the next step for the reader,
             and the button under the card is that step. -->
        <div class="auth-status auth-status--warn" aria-hidden="true">
          {{ symbols.warning }}
        </div>
        <!-- The heading the other two expired links carry. It used to name the
             letter as well, and a reader holding three links from this site
             read three different events where there is one. -->
        <h1 class="auth-title">Ссылка устарела</h1>

        <p class="auth-text">
          Отправить новую ссылку для активации на <b>{{ pendingEmail }}</b
          >?
        </p>
        <p class="auth-note">Ссылка действительна 48 часов</p>
        <p
          v-if="resendResult.refusal.value"
          class="activation-refusal"
          role="alert"
        >
          {{ resendResult.refusal.value }}
        </p>
        <Button
          type="button"
          variant="primary"
          class="auth-button auth-button--spaced"
          :loading="resendLoading"
          :disabled="resendResult.waiting.value"
          @click="resend"
        >
          Отправить новую ссылку
        </Button>
      </template>

      <!-- The letter is out. The same receipt the registration ends on, in the
           same words: a reader who asked for a second letter is waiting for the
           very thing the first one promised, so the card that tells him so is
           the one he has already seen. Nothing has gone wrong here, so the mark
           of a failed state is gone with the offer, and so is the offer of
           help under the card. -->
      <template v-else-if="phase === 'expired'">
        <h1 class="auth-title">Проверьте почту</h1>
        <p class="auth-text" role="status">
          Мы отправили повторное письмо со ссылкой для активации на
          <b>{{ pendingEmail }}</b>
        </p>
        <p class="auth-note">Ссылка действительна 48 часов</p>
      </template>

      <!-- Not Found state -->
      <template v-else-if="phase === 'notFound'">
        <!-- The cross by the rule at .auth-status--warn in AuthSurface.sass:
             a link that opens nothing leaves this card no next step, unlike the
             expired one above it. It is drawn by the surface and not typed, so
             no glyph goes inside it. -->
        <div class="auth-status auth-status--error" aria-hidden="true" />
        <h1 class="auth-title">Ссылка недействительна</h1>

        <!-- Two causes in one line. The link is dead either because the account
             behind it is already activated, or because the pending registration
             it belonged to is gone and there is nothing left to activate. The
             server's own text for this state said the same thing in worse
             words, so it is gone. -->
        <p class="auth-text">
          {{
            error ||
            "Возможно ссылка уже была использована или срок ее действия истек"
          }}
        </p>

        <!-- One control, and the same one the other five dead links carry. The
             card used to offer the sign-in and the registration side by side,
             which asks the reader to pick the cause of a failure he cannot see;
             both doors stand on the home page anyway. -->
        <Button
          type="button"
          variant="primary"
          class="auth-button auth-button--spaced"
          @click="goHome"
        >
          На главную
        </Button>
      </template>

      <!-- Nothing has gone wrong on the two screens that end well - the finished
           registration and the receipt for the new letter - so there is nobody
           to ask for help: the offer stands under every other answer. -->
      <p
        v-if="phase !== 'success' && phase !== 'loading' && !resendSuccess"
        class="auth-footnote help-section"
      >
        Нужна помощь? Обратитесь в
        <router-link :to="supportFor('RegistrationIssue')"
          >поддержку</router-link
        >
      </p>
    </div>
  </div>
</template>

<style scoped lang="sass">
.activation-page
  margin: $major auto
  padding: 0 $medium

// The card says what it is doing rather than standing empty; the line is the
// whole body, so it carries no bottom gap of its own.
.activation-loading
  margin: 0

.activation-name
  text-align: left

  // UsernameInput draws the verdict on the name inside its own root, so the
  // strip AuthSurface hangs on `.auth-field + .auth-message` cannot reach it.
  // It is rebuilt one level in: the field gives up its 2rem, the verdict line
  // starts on the usual 3px and holds the rest of the strip open, and the
  // button below stands 2rem under the field whatever the line says.
  //
  // The extra class carries the specificity — a bare `.auth-field` here ties
  // with the sheet's own two-level selector, and a tie is settled by whichever
  // stylesheet the bundler happened to emit last.
  .activation-name__field
    margin-bottom: 0

    :deep(.username-status)
      margin-top: 3px
      min-height: calc(2rem - 3px)

  // The refusal of the server stands under the field in the page's own line,
  // and the field's verdict goes out while it does. The component checked the
  // name a moment earlier and knows nothing of what the submit came back with,
  // so it would keep printing "Имя свободно" and a green tick over a name the
  // server has just refused as somebody else's - one screen answering the same
  // question twice, in opposite directions.
  &.activation-name__field--refused
    :deep(.username-status)
      display: none

    // The mark inside the field. The component prints the ✗ of the icon set
    // for a refusal; the guest surface draws its crosses from two bars,
    // because every typographic cross in the fonts the site ships is thinner
    // than the ✓ standing beside it and how much thinner depends on which font
    // actually loaded. The component is shared and its markup is not this
    // page's to change, so the recipe of .auth-field-status--error is repeated
    // on the icon it does render.
    :deep(.status-icon.unavailable)
      width: 16px
      height: 16px
      font-size: 0

      &::before,
      &::after
        content: ""
        position: absolute
        left: 50%
        top: 50%
        width: 14px
        height: 2px
        margin: -1px 0 0 -7px
        border-radius: 1px
        background-color: currentColor

      &::before
        transform: rotate(45deg)

      &::after
        transform: rotate(-45deg)

// A refusal that came back from the server after the name had already passed
// the check in the field. It has no place in the strip — the strip is showing
// the verdict on what is typed there now — so it takes a line of its own above
// the button.
.activation-refusal
  margin: 0 0 $small
  font-size: $secondary-font-size
  line-height: 1.4
  color: $accent-red
  text-align: left

// The step back to the name wears the guest surface's own secondary action, so
// the look is not rebuilt here. What is left is the one state no other .auth-
// action has: this page shuts the control while the activation is on the wire,
// and a control that is dead has to look it.
.auth-action:disabled
  opacity: $disabled-opacity
  cursor: default
</style>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, computed, nextTick } from "vue";
import { useRouter } from "vue-router";
import { readConfirmationToken } from "@/shared/lib/utils/confirmationToken";
import { deviceTimeZone } from "@/shared/lib/utils/timeZone";
import { useAuthStore, UsernameInput, accountApi } from "@/entities/user";

import type { User } from "@/shared/api/models/community";
import Button from "@/shared/ui/Button/Button.vue";
import HomeButton from "./HomeButton.vue";
import { symbols } from "@/shared/lib/utils/icons";
import { parseApiErrors } from "@/shared/lib/utils/apiErrors";
import { readFieldError } from "@/shared/lib/errors/validationErrors";
import { unwrapResource } from "@/shared/api";
import { LETTER_NOT_SENT } from "@/shared/lib/errors";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { supportFor } from "@/entities/ticket";
import { ACTIVATION_RESENT, linkLifetimeNote } from "@/features/auth";

type ActivationPhase =
  | "loading"
  | "selectUsername"
  | "confirmUsername"
  | "expired"
  | "notFound"
  | "submitting"
  | "success";

/** A taken name, in the words UsernameInput prints under the field, not the vocabulary's "Уже занято". */
const NAME_UNAVAILABLE = "Это имя занято";

const router = useRouter();
const userStore = useAuthStore();

const phase = ref<ActivationPhase>("loading");

/**
 * Whether the other tab has finished the activation. A function: inline, the
 * compiler narrows the comparison to the phase just assigned before the await.
 */
const finishedElsewhere = (): boolean => phase.value === "success";
const pendingEmail = ref<string>("");
const token = ref<string>("");
const username = ref("");
const usernameAvailable = ref(false);
/** The name the finished screen greets, the one thing a tab told over the channel has. */
const activatedName = ref("");
const error = ref<string | null>(null);

const resendLoading = ref(false);
const resendSuccess = ref(false);
/** How long the link of the letter resent lives, as the answer named it. */
const resentHours = ref<number>();

// The wrapper of the shared field, through which the caret is placed.
const nameBlock = ref<HTMLElement | null>(null);

let channel: BroadcastChannel | null = null;

const canSubmit = computed(() => phase.value === "confirmUsername");

onMounted(async () => {
  // From the fragment, cleared from the address as it is read: in the path it went into history and the access log.
  token.value = readConfirmationToken();

  // No line of our own about the missing value: the card says the link is not a link.
  if (!token.value) {
    phase.value = "notFound";
    return;
  }

  if (typeof BroadcastChannel !== "undefined") {
    channel = new BroadcastChannel("dm_activation");
    channel.onmessage = (event) => {
      // The other tab finished first: this one ends on the same finished screen.
      // A message without a name stays where it is.
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

  await checkTokenStatus();

  // The field is a render after the check, so it is focused after it.
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

  // The other tab finished while the check was out: a verdict on a spent link must not replace the finished screen.
  if (finishedElsewhere()) return;

  if (apiError) {
    phase.value = "notFound";
    return;
  }

  if (data) {
    pendingEmail.value = data.email;

    if (data.status === "ready") {
      phase.value = "selectUsername";
    } else if (data.status === "expired") {
      phase.value = "expired";
    }
  }
}

function proceedToConfirm() {
  phase.value = "confirmUsername";
}

function goBackToSelect() {
  phase.value = "selectUsername";
}

async function submitActivation() {
  if (!canSubmit.value) return;

  phase.value = "submitting";

  // The account starts in the zone of this device, left out where the browser will not say.
  const { data, error: apiError } = await accountApi.activate(token.value, {
    username: username.value,
    timeZoneId: deviceTimeZone() || undefined,
  });

  // As in checkTokenStatus: the refusal for a token already spent must not replace the finished screen.
  if (finishedElsewhere()) return;

  if (apiError) {
    const errors = parseApiErrors(apiError);
    // A taken name in the words of the field, from either side of the wire.
    const usernameError = errors["username"]?.includes("Taken")
      ? NAME_UNAVAILABLE
      : readFieldError(errors, "username");

    if (usernameError) {
      error.value = usernameError;
      phase.value = "selectUsername";
      usernameAvailable.value = false;
      return;
    }

    // What the server said, or nothing: the card's heading names the failure.
    error.value = Object.values(errors).flat()[0] ?? null;

    if (apiError.status === 410) {
      phase.value = "expired";
    } else {
      phase.value = "notFound";
    }
    return;
  }

  // Out of the envelope: stored as it came, the session held the wrapper and not the account.
  const activated = unwrapResource<User>(data);
  if (activated) {
    userStore.updateUser(activated);
    activatedName.value = activated.username;
    phase.value = "success";

    channel?.postMessage({ type: "activated", username: activated.username });
  }
}

/** A refused resend, over the button, which stays live. */
const resendResult = useActionResult();

async function resend() {
  if (!pendingEmail.value) return;

  resendLoading.value = true;
  resendResult.clear();

  try {
    const { data, error } = await accountApi.recover(pendingEmail.value);
    if (error) {
      resendResult.refuse(error, LETTER_NOT_SENT);
      return;
    }
    resentHours.value = data?.linkLifetimeHours;
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
</script>

<template>
  <!-- A page and not a dialog, so the card has a frame of its own and no cross. -->
  <div class="auth-surface activation-page">
    <!-- Every answer is a mark, a heading and a line or two: the narrow measure. -->
    <div
      class="auth-card auth-card--framed auth-card--center auth-card--compact"
    >
      <!-- Checking the link: never an empty card under the support line. -->
      <p v-if="phase === 'loading'" class="auth-note activation-loading">
        Проверяем ссылку...
      </p>

      <template v-else-if="phase === 'selectUsername'">
        <h1 class="auth-title">Выберите имя</h1>

        <!-- The card is centred, the field is not: it reads from the left, as every field of the site. -->
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
          class="auth-button"
          :disabled="!usernameAvailable"
          @click="proceedToConfirm"
        >
          Продолжить
        </Button>
      </template>

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
          <!-- A real space, so the row copies as one line. -->
          <span class="copy-space">{{ " " }}</span>
          <!-- "Изменить" goes back a step and nowhere in the address, so it is a button
               in the guest surface's secondary action, shut while the request is out. -->
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
          class="auth-button"
          :disabled="!canSubmit"
          :loading="phase === 'submitting'"
          @click="submitActivation"
        >
          Завершить регистрацию
        </Button>
      </template>

      <template v-else-if="phase === 'success'">
        <div class="auth-status auth-status--ok" aria-hidden="true">
          {{ symbols.checkmark }}
        </div>
        <h1 class="auth-title">Регистрация завершена</h1>
        <!-- No bang after the name: the heading already says the news. -->
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

      <!-- One attempt: the offer is replaced by the receipt, so nothing is pressed twice. -->
      <template v-else-if="phase === 'expired' && !resendSuccess">
        <!-- The warning sign: the site can take the next step, the button under the card. -->
        <div class="auth-status auth-status--warn" aria-hidden="true">
          {{ symbols.warning }}
        </div>
        <!-- The heading the other two expired links carry. -->
        <h1 class="auth-title">Ссылка устарела</h1>

        <p class="auth-text">
          Отправить новую ссылку для активации на <b>{{ pendingEmail }}</b
          >?
        </p>
        <!-- Before the letter goes out no answer has named the hours: the fallback. -->
        <p class="auth-note">{{ linkLifetimeNote() }}</p>
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

      <!-- The receipt the registration ends on, in its words; no mark and no offer of help. -->
      <template v-else-if="phase === 'expired'">
        <h1 class="auth-title">Проверьте почту</h1>
        <p class="auth-text" role="status">
          {{ ACTIVATION_RESENT }}
          <b>{{ pendingEmail }}</b>
        </p>
        <p v-if="resentHours !== undefined" class="auth-note">
          {{ linkLifetimeNote(resentHours) }}
        </p>
      </template>

      <template v-else-if="phase === 'notFound'">
        <!-- The cross: a link that opens nothing leaves no next step. Drawn by the surface. -->
        <div class="auth-status auth-status--error" aria-hidden="true" />
        <h1 class="auth-title">Ссылка недействительна</h1>

        <!-- Two causes in one line: the account is activated already, or the registration is gone. -->
        <p class="auth-text">
          {{
            error ||
            "Возможно ссылка уже была использована или срок ее действия истек"
          }}
        </p>

        <!-- The one control all the dead links carry. -->
        <HomeButton />
      </template>

      <!-- Help is offered under every answer but the two that end well. -->
      <p
        v-if="phase !== 'success' && phase !== 'loading' && !resendSuccess"
        class="auth-footnote"
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
@use "@/assets/styles/AuthTokens" as *

.activation-page
  margin: $major auto
  padding: 0 $medium

// The line is the whole body, so it has no bottom gap.
.activation-loading
  margin: 0

.activation-name
  text-align: left

  // The verdict is drawn inside UsernameInput, out of reach of the strip of
  // _AuthField.sass, so the strip is rebuilt one level in; the extra class
  // carries the specificity, or the order of the sheets would decide.
  .activation-name__field
    margin-bottom: 0

    :deep(.username-status)
      margin-top: $auth-strip-gap
      min-height: calc(#{$auth-strip} - #{$auth-strip-gap})

  // While the server's refusal stands, the field's own verdict goes: it would
  // still say "Имя свободно" over a name just refused.
  &.activation-name__field--refused
    :deep(.username-status)
      display: none

    // The cross of the guest surface, from two bars, on the icon the shared field
    // renders: a typographic cross is thinner than the tick beside it.
    :deep(.status-icon.unavailable)
      width: 16px
      height: 16px
      font-size: 0

      +auth-bar-cross

// A refusal after the name passed the field's check: a line of its own over the button.
.activation-refusal
  margin: 0 0 $small
  font-size: $secondary-font-size
  line-height: 1.4
  color: $accent-red
  text-align: left

// The secondary action of the guest surface; this page shuts it while the activation is out.
.auth-action:disabled
  opacity: $disabled-opacity
  cursor: default
</style>

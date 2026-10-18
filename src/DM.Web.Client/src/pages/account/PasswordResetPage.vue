<script setup lang="ts">
/**
 * The page the password-recovery letter points at.
 *
 * Four answers on one card: the link is being checked, the link is good and
 * asks for a password, the link has expired, the link is not a link. The card
 * names itself in every one of them, so there is no page heading above it.
 */
import { ref, computed, onMounted, nextTick, watch } from "vue";
import { useRouter } from "vue-router";
import { readConfirmationToken } from "@/shared/lib/utils/confirmationToken";
import { useNewPasswordField } from "@/shared/lib/composables/useNewPasswordField";
import { accountApi } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import {
  PasswordInput,
  PasswordStrengthIndicator,
} from "@/shared/ui/PasswordInput";
import { symbols } from "@/shared/lib/utils/icons";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";
import { supportFor } from "@/entities/ticket";

const router = useRouter();

// Page state: loading -> ready/expired/invalid/completed
type PageState = "loading" | "ready" | "expired" | "invalid" | "completed";
const pageState = ref<PageState>("loading");
const submitting = ref(false);

// Password field with HIBP check
const { password: newPassword, isValid } = useNewPasswordField();

// Not `passwordInput`: the template writes the component in kebab case, and
// <script setup> resolves <password-input> against the camel-cased bindings
// first. A ref by that name wins the lookup and the field renders as nothing.
const newPasswordInput = ref<InstanceType<typeof PasswordInput> | null>(null);

// The value arrives in the fragment and is cleared as it is read, so it is read
// once and kept: in the path it went into browser history and into the access log
// of the edge.
const token = ref("");

onMounted(async () => {
  token.value = readConfirmationToken();
  const { data, error } = await accountApi.getPasswordResetTokenInfo(
    token.value,
  );

  if (error || !data) {
    pageState.value = "invalid";
    return;
  }

  if (data.status === "expired") {
    pageState.value = "expired";
    return;
  }

  pageState.value = "ready";
  // The card holds one field and the reader came here to fill it in. The check
  // answers first, so the field is a render away from the mount.
  await nextTick();
  newPasswordInput.value?.focus();
});

const passwordError = ref("");

// A refusal is about the password that earned it - a breach index, above all
// else. The moment the field is edited the server has not seen what is in it,
// so the line goes back to the strength scale instead of holding a verdict on
// a password that is no longer there. Same rule as the registration form's.
watch(newPassword, () => {
  if (passwordError.value) passwordError.value = "";
});

/**
 * Whether the password may be sent.
 *
 * A refusal already on screen shuts the button: the server turned this exact
 * password down - a breach index, above all else - and pressing again would
 * hand it the very same one. The first edit drops the refusal and the button
 * opens with it. The registration form's second step is shut the same way.
 */
const canSubmit = computed(() => isValid.value && !passwordError.value);

const submit = async () => {
  if (!canSubmit.value) return;

  submitting.value = true;
  passwordError.value = "";

  const { data, error } = await accountApi.completePasswordReset(
    token.value,
    newPassword.value,
  );

  submitting.value = false;

  if (error) {
    const errors = parseApiErrors(error);
    const passErr = getFieldError(errors, "newPassword");

    if (passErr) {
      passwordError.value = passErr;
      return;
    }

    if (errors["token"] || errors["general"]) {
      pageState.value = "expired";
      return;
    }

    pageState.value = "invalid";
    return;
  }

  if (data) {
    pageState.value = "completed";
  }
};

// Every way out of this card leads to a guest dialog, and the guest dialogs
// live in the header: the home page opens the right one from the query, the
// same way the support page hands the reader to recovery.
function goGuest(action: string) {
  router.push({ path: "/", query: { action } });
}

// The one way out of a dead link, and the same one on every screen that answers
// one. See AccountActivationPage: a card that offers a choice asks the reader
// to name the cause of a failure he cannot see.
function goHome() {
  router.push("/");
}
</script>

<template>
  <div class="auth-surface reset-page">
    <!-- The card says how wide it wants to be, because the sheet cannot work
         that out from what stands inside it. The wide measure is kept for the
         password field; the answers that state an outcome in a line take the
         narrow one. -->
    <div
      class="auth-card auth-card--framed"
      :class="{
        'auth-card--center': pageState !== 'ready',
        'auth-card--compact': pageState !== 'ready',
      }"
    >
      <!-- Checking the link — see AccountActivationPage: the state existed and
           the template had no branch for it, so the page from the letter was a
           blank card with a link to support under it. -->
      <p v-if="pageState === 'loading'" class="auth-note">
        Проверяем ссылку...
      </p>

      <!-- The password was accepted. No redirect of its own: the reader chooses
           when to sign in. -->
      <template v-else-if="pageState === 'completed'">
        <div class="auth-status auth-status--ok" aria-hidden="true">
          {{ symbols.checkmark }}
        </div>
        <h1 class="auth-title">Новый пароль сохранен</h1>
        <!-- The line the changed address ends on, in the same place: a heading
             names the outcome and the line under it says it happened. -->
        <p class="auth-text">Ваш пароль был успешно обновлен</p>
        <Button
          class="auth-button auth-button--spaced"
          type="button"
          variant="primary"
          @click="goGuest('login')"
        >
          Войти
        </Button>
      </template>

      <!-- The link ran out of time. It is the one failure with a way forward,
           so the way forward is a control and not a word inside a sentence:
           the same shape the expired activation link wears. -->
      <template v-else-if="pageState === 'expired'">
        <!-- The warning sign by the rule at .auth-status--warn in
             AuthSurface.sass: the site can take the next step for the reader,
             and the button under the card is that step. -->
        <div class="auth-status auth-status--warn" aria-hidden="true">
          {{ symbols.warning }}
        </div>
        <h1 class="auth-title">Ссылка устарела</h1>
        <p class="auth-text">Срок действия сброса пароля истек</p>
        <Button
          class="auth-button auth-button--spaced"
          type="button"
          variant="primary"
          @click="goGuest('recovery')"
        >
          Запросить новую ссылку
        </Button>
      </template>

      <!-- Not a link this server issued, or not one any more. The reader cannot
           tell those apart from where he stands, so the card lists what usually
           happened instead of guessing. -->
      <template v-else-if="pageState === 'invalid'">
        <!-- The cross by the rule at .auth-status--warn in AuthSurface.sass:
             a link that opens nothing leaves this card no next step, unlike the
             expired one above it. -->
        <div class="auth-status auth-status--error" aria-hidden="true" />
        <h1 class="auth-title">Ссылка недействительна</h1>

        <!-- Two causes in one line, and one way out under it: the same control
             the other five dead links carry. The card used to stand the sign-in
             and a new letter side by side, which asks the reader to pick the
             cause of a failure he cannot see. -->
        <p class="auth-text">
          Возможно ссылка уже была использована или срок ее действия истек
        </p>
        <Button
          class="auth-button auth-button--spaced"
          type="button"
          variant="primary"
          @click="goHome"
        >
          На главную
        </Button>
      </template>

      <!-- The form. A label and not a placeholder: a placeholder disappears at
           the first keystroke, and what the field is for disappears with it.
           The rule the password has to meet is written on the strength line,
           which stands there from the start. -->
      <form v-else-if="pageState === 'ready'" @submit.prevent="submit">
        <h1 class="auth-title">Новый пароль</h1>

        <!-- "Пароль" and not "Новый пароль": the heading one line above says
             which password this is, and a label that repeats it makes the
             reader read the same two words twice. A bare noun is what every
             other label of the guest contour is ("Почта", "Пароль"). -->
        <label class="auth-label" for="reset-new-password">Пароль</label>
        <div class="auth-field">
          <!-- The line under the field is the field's own: the rule while
               nothing is typed, the strength once something is, the server's
               refusal once it has answered. Named as the description of the
               input, and the refusal marks the input invalid, so a reader who
               cannot see the red bar is told the same three things. -->
          <password-input
            id="reset-new-password"
            ref="newPasswordInput"
            v-model="newPassword"
            autocomplete="new-password"
            aria-describedby="reset-password-message"
            :aria-invalid="passwordError ? 'true' : undefined"
            :disabled="submitting"
          />
        </div>
        <PasswordStrengthIndicator
          id="reset-password-message"
          class="auth-meter"
          :password="newPassword"
          :error="passwordError || null"
          always
        />

        <Button
          class="auth-button"
          type="submit"
          variant="primary"
          :loading="submitting"
          :disabled="!canSubmit"
        >
          Сохранить
        </Button>
      </form>

      <!-- Everywhere but the terminal success: there the errand is over and
           there is nothing left to need help with. The second class is what
           tokenPages.spec.ts finds the line by. -->
      <p
        v-if="pageState !== 'completed' && pageState !== 'loading'"
        class="auth-footnote help-section"
      >
        Нужна помощь? Обратитесь в
        <router-link :to="supportFor('AccessRecovery')">поддержку</router-link>
      </p>
    </div>
  </div>
</template>

<style scoped lang="sass">
// Everything inside the card is drawn by AuthSurface.sass. What is left here is
// where the card stands on the page.
.reset-page
  margin: $major auto
  padding: 0 $medium
</style>

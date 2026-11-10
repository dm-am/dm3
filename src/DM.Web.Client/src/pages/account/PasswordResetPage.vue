<script setup lang="ts">
/** The page the password-recovery letter points at: four answers on one card, each naming itself. */
import { ref, computed, onMounted, nextTick, watch } from "vue";
import { useRouter } from "vue-router";
import { readConfirmationToken } from "@/shared/lib/utils/confirmationToken";
import { useNewPasswordField } from "@/shared/lib/composables/useNewPasswordField";
import { accountApi } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import HomeButton from "./HomeButton.vue";
import {
  PasswordInput,
  PasswordStrengthIndicator,
} from "@/shared/ui/PasswordInput";
import { symbols } from "@/shared/lib/utils/icons";
import { parseApiErrors } from "@/shared/lib/utils/apiErrors";
import { readFieldError } from "@/shared/lib/errors/validationErrors";
import { supportFor } from "@/entities/ticket";

const router = useRouter();

type PageState = "loading" | "ready" | "expired" | "invalid" | "completed";
const pageState = ref<PageState>("loading");
const submitting = ref(false);

const { password: newPassword, isValid } = useNewPasswordField();

// Not `passwordInput`: the kebab-case <password-input> would resolve to a binding of that name.
const newPasswordInput = ref<InstanceType<typeof PasswordInput> | null>(null);

// From the fragment, cleared from the address as it is read, so kept here.
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
  // The field is a render after the check, so it is focused after it.
  await nextTick();
  newPasswordInput.value?.focus();
});

const passwordError = ref("");

// A refusal is about the password that earned it: an edit drops it, as on the registration form.
watch(newPassword, () => {
  if (passwordError.value) passwordError.value = "";
});

/** A refusal on screen shuts the button: pressing again would send the same password. */
const canSubmit = computed(() => isValid.value && !passwordError.value);

const submit = async () => {
  submitting.value = true;
  passwordError.value = "";

  const { data, error } = await accountApi.completePasswordReset(
    token.value,
    newPassword.value,
  );

  submitting.value = false;

  if (error) {
    const errors = parseApiErrors(error);
    const passErr = readFieldError(errors, "newPassword");

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

// Every way out leads to a guest dialog, which the home page opens from the query.
function goGuest(action: string) {
  router.push({ path: "/", query: { action } });
}
</script>

<template>
  <div class="auth-surface reset-page">
    <!-- The wide measure for the password field, the narrow one for an outcome in a line. -->
    <div
      class="auth-card auth-card--framed"
      :class="{
        'auth-card--center': pageState !== 'ready',
        'auth-card--compact': pageState !== 'ready',
      }"
    >
      <!-- Checking the link: never an empty card under the support line. -->
      <p v-if="pageState === 'loading'" class="auth-note">
        Проверяем ссылку...
      </p>

      <!-- The password is accepted; the reader signs in when he chooses. -->
      <template v-else-if="pageState === 'completed'">
        <div class="auth-status auth-status--ok" aria-hidden="true">
          {{ symbols.checkmark }}
        </div>
        <h1 class="auth-title">Новый пароль сохранен</h1>
        <!-- A heading for the outcome and a line that says it happened. -->
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

      <!-- The one failure with a way forward, and the way forward is a control. -->
      <template v-else-if="pageState === 'expired'">
        <!-- The warning sign: the site can take the next step, the button under the card. -->
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

      <!-- Not a link this server issued, or not one any more: the card cannot tell which. -->
      <template v-else-if="pageState === 'invalid'">
        <!-- The cross: a link that opens nothing leaves no next step. -->
        <div class="auth-status auth-status--error" aria-hidden="true" />
        <h1 class="auth-title">Ссылка недействительна</h1>

        <!-- Two causes in one line, and the one control the dead links carry. -->
        <p class="auth-text">
          Возможно ссылка уже была использована или срок ее действия истек
        </p>
        <HomeButton />
      </template>

      <!-- A label and not a placeholder; the rule of the password is on the strength line from the start. -->
      <form v-else-if="pageState === 'ready'" @submit.prevent="submit">
        <h1 class="auth-title">Новый пароль</h1>

        <!-- "Пароль": the heading above says which password. -->
        <label class="auth-label" for="reset-new-password">Пароль</label>
        <div class="auth-field">
          <!-- The line under the field describes it: the rule, the strength or the refusal, which marks the field invalid. -->
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

      <!-- Help everywhere but on the finished errand. -->
      <p
        v-if="pageState !== 'completed' && pageState !== 'loading'"
        class="auth-footnote"
      >
        Нужна помощь? Обратитесь в
        <router-link :to="supportFor('AccessRecovery')">поддержку</router-link>
      </p>
    </div>
  </div>
</template>

<style scoped lang="sass">
// The card is drawn by AuthSurface.sass; this is where it stands on the page.
.reset-page
  margin: $major auto
  padding: 0 $medium
</style>

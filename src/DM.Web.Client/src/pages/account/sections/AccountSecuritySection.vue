<template>
  <section class="section">
    <h2 class="section-title">Безопасность</h2>

    <div class="security-content">
      <!-- Email Change -->
      <div class="security-block">
        <h3 class="subsection-title">Смена электронной почты</h3>

        <!-- The receipt the other six screens that send a letter already give,
             in their words: the address it went to, in bold, and how long the
             link inside it lives. It stands where the form stood, because the
             block the reader is looking at is free to say this and a plate in
             the corner is what a screen uses when it is not. -->
        <template v-if="emailSentTo">
          <p class="state-line" role="status">
            Мы отправили письмо со ссылкой для смены почты на
            <strong>{{ emailSentTo }}</strong>
          </p>
          <!-- TokenConfiguration.EmailChangeTokenLifetimeHours — 24, the same
               day the two-factor removal letter lives, and said in its
               words. -->
          <p class="state-note">Ссылка действительна 24 часа</p>
        </template>

        <template v-else>
          <div class="current-value">
            Текущая почта: <strong>{{ user.email || "не указана" }}</strong>
          </div>

          <FormField label="Новая почта" name="new-email">
            <input
              id="new-email"
              v-model="emailForm.newEmail"
              type="email"
              autocomplete="email"
            />
          </FormField>

          <FormField label="Пароль для подтверждения" name="email-password">
            <input
              id="email-password"
              v-model="emailForm.password"
              type="password"
              autocomplete="current-password"
            />
          </FormField>

          <Button
            variant="primary"
            :loading="changeEmailAction.loading.value"
            :disabled="!emailForm.newEmail || !emailForm.password"
            @click="changeEmail"
            class="save-button"
          >
            Изменить почту
          </Button>
          <ActionResult
            :text="changeEmailAction.error.value"
            class="error-text"
          />
        </template>
      </div>

      <!-- Password Change -->
      <div class="security-block">
        <h3 class="subsection-title">Смена пароля</h3>

        <!-- What the press did, where the form was, the way the moderation
             block states an outcome in place of its own controls. The ended
             sessions belong in this line and not only in the list below it: the
             server ends them inside the same call (PasswordChangeService), and
             a reader who never scrolls down would otherwise learn about it from
             the device that stopped working. -->
        <template v-if="passwordChanged">
          <p class="state-line" role="status">
            Пароль изменен, остальные сессии завершены
          </p>
          <!-- The notice the server sends right after, named the way the
               receipts above name a letter. An account without an address gets
               no letter (the sender is handed an empty string), so the line is
               not drawn for one. -->
          <p v-if="user.email" class="state-note">
            Мы отправили письмо о смене пароля на
            <strong>{{ user.email }}</strong>
          </p>
        </template>

        <template v-else>
          <FormField label="Текущий пароль" name="old-password">
            <input
              id="old-password"
              v-model="oldPassword"
              type="password"
              autocomplete="current-password"
            />
          </FormField>

          <FormField label="Новый пароль" name="new-password">
            <input
              id="new-password"
              v-model="newPassword"
              type="password"
              autocomplete="new-password"
            />
            <PasswordStrengthIndicator
              :password="newPassword"
              :is-same-as-old="isSameAsOld"
            />
            <template #hint>Минимум 8 символов</template>
          </FormField>

          <FormField
            label="Подтвердите новый пароль"
            name="confirm-password"
            :errors="
              confirmPassword && newPassword !== confirmPassword
                ? ['Пароли не совпадают']
                : []
            "
          >
            <input
              id="confirm-password"
              v-model="confirmPassword"
              type="password"
              autocomplete="new-password"
            />
          </FormField>

          <Button
            variant="primary"
            :loading="changePasswordAction.loading.value"
            :disabled="!isPasswordFormValid"
            @click="changePassword"
            class="save-button"
          >
            Изменить пароль
          </Button>
          <ActionResult
            :text="changePasswordAction.error.value"
            class="error-text"
          />
        </template>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, computed } from "vue";
import { accountApi, fetchUser } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { ActionResult } from "@/shared/ui/ActionResult";
import { FormField } from "@/shared/ui/Form";
import { PasswordStrengthIndicator } from "@/shared/ui/PasswordInput";
import { useAsyncAction } from "@/shared/lib/composables/useAsyncAction";
import { useNewPasswordField } from "@/shared/lib/composables/useNewPasswordField";
import type { User } from "@/shared/api/models/community/users";
import { refusalHere } from "@/shared/lib/errors";

defineProps<{
  user: User;
}>();

const emit = defineEmits<{
  /**
   * The letter for a new address is out. The request writes a journal entry of
   * its own (EmailChangeService logs SecurityEventType.EmailChange), and the
   * block that draws the journal read it once, on mount.
   */
  emailChangeRequested: [];
  /**
   * The password is new. The same server call ends every other session and
   * writes the journal entry (PasswordChangeService), so the two blocks under
   * this one are left showing the account as it was before the press.
   */
  passwordChanged: [];
}>();

// Email form
const emailForm = ref({
  newEmail: "",
  password: "",
});

/**
 * The address the letter went to, and the reason it is kept: the fields are
 * emptied on the way to the receipt, and the receipt has to name the address.
 */
const emailSentTo = ref("");

/** Whether this block has already changed the password in this visit. */
const passwordChanged = ref(false);

// Password form
const oldPassword = ref("");
const confirmPassword = ref("");

// New password field with HIBP check and same-as-old validation
const {
  password: newPassword,
  isSameAsOld,
  isValid: isNewPasswordValid,
} = useNewPasswordField({ oldPassword });

const passwordsMatch = computed(
  () =>
    newPassword.value.length > 0 && newPassword.value === confirmPassword.value,
);

const isPasswordFormValid = computed(
  () =>
    oldPassword.value.length > 0 &&
    isNewPasswordValid.value &&
    passwordsMatch.value,
);

// Change email
const changeEmailAction = useAsyncAction();

const changeEmail = () => {
  changeEmailAction.execute(async () => {
    if (!emailForm.value.newEmail || !emailForm.value.password) return;

    // Read once, before the request, and sent from the same value: the receipt
    // then names the address the letter actually went to and cannot drift from
    // it while the fields are emptied below.
    const sentTo = emailForm.value.newEmail;

    const { error } = await accountApi.changeEmail({
      password: emailForm.value.password,
      email: sentTo,
    });
    // The server answers a 400 with per-field codes — a wrong password is
    // "Invalid" on password, a taken address is "Taken" on email. Collapsing
    // that into one sentence told the reader something was wrong and nothing
    // about what.
    //
    // A refusal the interceptor speaks for (403, 429, 5xx) is said in the
    // same line, and its plate is taken down: the two fields and the button
    // stay exactly where they were, and the line is where the reader looks.
    // No receipt either way - the letter did not go out.
    if (error) {
      const text = refusalHere(error, "Не удалось изменить почту");
      if (text) throw new Error(text);
      return;
    }

    emailForm.value = { newEmail: "", password: "" };
    await fetchUser();
    emailSentTo.value = sentTo;
    emit("emailChangeRequested");
  });
};

// Change password
const changePasswordAction = useAsyncAction();

const changePassword = () => {
  changePasswordAction.execute(async () => {
    if (!isPasswordFormValid.value) return;

    const { error } = await accountApi.changePassword({
      oldPassword: oldPassword.value,
      newPassword: newPassword.value,
    });
    // Same here, and it matters more: "Не удалось изменить пароль" reads
    // identically whether the current password was wrong or the new one broke
    // the policy, and those call for opposite corrections. The statuses the
    // interceptor speaks for are said in the same line too, with its plate
    // taken down: the three fields keep what was typed in them.
    if (error) {
      const text = refusalHere(error, "Не удалось изменить пароль");
      if (text) throw new Error(text);
      return;
    }

    oldPassword.value = "";
    newPassword.value = "";
    confirmPassword.value = "";
    passwordChanged.value = true;
    emit("passwordChanged");
  });
};
</script>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "../AccountPage.styles" as *

.security-content
  display: flex
  flex-direction: column
  gap: $big

.security-block
  padding: $medium
  background-color: $bg-element

.current-value
  margin-bottom: $medium
  color: $text-muted

  strong
    color: $text

// What a block says once its form has nothing left to do: the same quiet line
// the two-factor block leaves behind (.state-line there), with the address it
// names standing out of it the way the current address does above.
.state-line
  margin: 0
  color: $text-muted

  strong
    color: $text

// The second line of a receipt — a validity period or a letter that followed.
// Secondary, exactly as .auth-note is under .auth-text on the six screens this
// receipt is copied from.
.state-note
  margin: $minor 0 0
  font-size: $secondary-font-size
  color: $text-muted

  strong
    color: $text
</style>

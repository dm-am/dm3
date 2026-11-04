<template>
  <section class="section">
    <h2 class="section-title">Безопасность</h2>

    <div class="security-content">
      <div class="security-block">
        <h3>Смена электронной почты</h3>

        <!-- The receipt the other screens that send a letter give: the address in bold and how long the link lives, where the form stood. -->
        <template v-if="emailSentTo">
          <p class="state-line" role="status">
            Мы отправили письмо со ссылкой для смены почты на
            <strong>{{ emailSentTo }}</strong>
          </p>
          <p v-if="emailLinkHours !== undefined" class="state-note">
            {{ linkLifetimeNote(emailLinkHours) }}
          </p>
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
            :loading="changingEmail"
            :disabled="
              !emailForm.newEmail ||
              !emailForm.password ||
              emailResult.waiting.value
            "
            @click="changeEmail"
          >
            Изменить почту
          </Button>
          <ActionResult :result="emailResult" />
        </template>
      </div>

      <div class="security-block">
        <h3>Смена пароля</h3>

        <!-- What the press did, where the form was; the other sessions end in the same call, so the line says so. -->
        <template v-if="passwordChanged">
          <p class="state-line" role="status">
            Пароль изменен, остальные сессии завершены
          </p>
          <!-- The notice the server sends after, named by its address; an account without one gets no letter. -->
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
            <!-- The minimum in the scale's own line from the start, as on the registration: one line, not a hint over it. -->
            <PasswordStrengthIndicator
              :password="newPassword"
              :is-same-as-old="isSameAsOld"
              always
            />
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
            :loading="changingPassword"
            :disabled="!isPasswordFormValid || passwordResult.waiting.value"
            @click="changePassword"
          >
            Изменить пароль
          </Button>
          <ActionResult :result="passwordResult" />
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
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { useNewPasswordField } from "@/shared/lib/composables/useNewPasswordField";
import { unwrapResource } from "@/shared/api/envelope";
import type { MailedLink } from "@/shared/api/models/account";
import type { User } from "@/shared/api/models/community/users";
import { linkLifetimeNote } from "@/features/auth";

defineProps<{
  user: User;
}>();

const emit = defineEmits<{
  /** The letter for a new address is out, and the request wrote a journal entry. */
  emailChangeRequested: [];
  /** The password is new: the same call ended the other sessions and wrote the journal. */
  passwordChanged: [];
}>();

const emailForm = ref({
  newEmail: "",
  password: "",
});

/** The address the letter went to: the fields are emptied before the receipt names it. */
const emailSentTo = ref("");

/** How long the link of that letter lives, as the answer named it. */
const emailLinkHours = ref<number>();

/** Whether this block has already changed the password in this visit. */
const passwordChanged = ref(false);

const oldPassword = ref("");
const confirmPassword = ref("");

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

const emailResult = useActionResult();
const changingEmail = ref(false);

async function changeEmail() {
  if (!emailForm.value.newEmail || !emailForm.value.password) return;

  // Read before the request and sent from the same value, so the receipt names the address the letter went to.
  const sentTo = emailForm.value.newEmail;

  emailResult.clear();
  // Held until the receipt takes the form's place.
  changingEmail.value = true;
  const { data, error } = await accountApi.changeEmail({
    password: emailForm.value.password,
    email: sentTo,
  });
  // A 400 carries per-field codes (a wrong password, a taken address), and every
  // refusal the interceptor speaks for is said in the same line; no receipt.
  if (error) {
    changingEmail.value = false;
    emailResult.refuse(error, "Не удалось изменить почту");
    return;
  }

  emailForm.value = { newEmail: "", password: "" };
  await fetchUser();
  emailLinkHours.value = unwrapResource<MailedLink>(data)?.linkLifetimeHours;
  emailSentTo.value = sentTo;
  changingEmail.value = false;
  emit("emailChangeRequested");
}

const passwordResult = useActionResult();
const changingPassword = ref(false);

async function changePassword() {
  if (!isPasswordFormValid.value) return;

  passwordResult.clear();
  changingPassword.value = true;
  const { error } = await accountApi.changePassword({
    oldPassword: oldPassword.value,
    newPassword: newPassword.value,
  });
  changingPassword.value = false;
  // Here it matters more: a wrong current password and a weak new one call for
  // opposite corrections. The fields keep what was typed.
  if (error) {
    passwordResult.refuse(error, "Не удалось изменить пароль");
    return;
  }

  oldPassword.value = "";
  newPassword.value = "";
  confirmPassword.value = "";
  passwordChanged.value = true;
  emit("passwordChanged");
}
</script>

<style scoped lang="sass">
@use "../AccountPage.styles" as *

.security-content
  display: flex
  flex-direction: column
  gap: $big

.security-block
  padding: $medium
  background-color: $bg-element
  +account-fields

.current-value
  margin-bottom: $medium
  color: $text-muted

  strong
    color: $text

// The quiet line a block leaves once its form is done, as the two-factor block's.
.state-line
  margin: 0
  color: $text-muted

  strong
    color: $text

// The second line of a receipt, secondary as .auth-note under .auth-text.
.state-note
  margin: $minor 0 0
  font-size: $secondary-font-size
  color: $text-muted

  strong
    color: $text
</style>

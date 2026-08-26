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
import { ref } from "vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Button from "@/shared/ui/Button/Button.vue";
import { accountApi } from "@/entities/user";
import {
  useValidatedField,
  validators,
} from "@/shared/lib/composables/useValidatedField";
import { announcedByInterceptor, describeFailure } from "@/shared/lib/errors";

const emit = defineEmits<{
  (e: "cancel"): void;
}>();

const sent = ref(false);
const loading = ref(false);
const serverError = ref("");

const emailField = useValidatedField({
  validate: validators.combine(validators.required(), validators.email()),
});

const submit = async () => {
  if (!(await emailField.validate())) return;

  loading.value = true;
  serverError.value = "";
  const { error } = await accountApi.requestTwoFactorRemoval(
    emailField.value.value.trim(),
  );
  loading.value = false;

  if (error) {
    // A rate limit is already on screen as a toast and must not be said twice;
    // anything else has to be said here, or the reader waits for a letter
    // nobody sent.
    if (!announcedByInterceptor(error.status)) {
      serverError.value = describeFailure(
        error,
        "Не удалось отправить письмо. Попробуйте позже.",
      );
    }
    return;
  }

  sent.value = true;
};

const onEmailInput = () => {
  emailField.onInput();
  serverError.value = "";
};

const clearForm = () => {
  sent.value = false;
  loading.value = false;
  serverError.value = "";
  emailField.reset();
};

const cancel = () => {
  clearForm();
  emit("cancel");
};
</script>

<template>
  <Dialog :narrow="!sent" :auto="sent" @before-close="clearForm">
    <template v-if="sent">
      <div>
        <dialog-title>Проверьте почту</dialog-title>
        <p class="main-text">
          Если аккаунт с такой почтой есть и второй фактор на нем включен,
          письмо со ссылкой отправлено.
        </p>
        <p class="expiry-note">
          Ссылка не снимает второй фактор сразу: она назначает снятие через семь
          дней и завершает все сессии. Отменить снятие можно ссылкой из второго
          письма или входом со вторым фактором.
        </p>
        <Button type="button" @click="cancel"> Закрыть </Button>
      </div>
    </template>

    <template v-else>
      <dialog-title>Снятие второго фактора</dialog-title>

      <Form
        @submit="submit"
        @cancel="cancel"
        :valid="emailField.isReady.value"
        :loading="loading"
        action="Отправить"
        cancel="Отмена"
      >
        <p class="form-description">
          Письмо уйдет на подтвержденную почту аккаунта. Снятие произойдет через
          семь дней, и любой вход со вторым фактором его отменит.
        </p>

        <form-field
          label="Почта"
          name="two-factor-removal-email"
          :errors="
            emailField.error.value
              ? [emailField.error.value]
              : serverError
                ? [serverError]
                : []
          "
        >
          <input
            id="two-factor-removal-email"
            v-model="emailField.value.value"
            type="email"
            autocomplete="email"
            @blur="emailField.onBlur"
            @input="onEmailInput"
          />
        </form-field>
      </Form>
    </template>
  </Dialog>
</template>

<style scoped lang="sass">
.form-description
  margin: 0 0 $small
  color: $text-muted
  font-size: $secondary-font-size

.main-text
  margin: 0 0 $small
  line-height: 1.5

.expiry-note
  margin: 0 0 $medium
  color: $text-muted
  font-size: $secondary-font-size
</style>

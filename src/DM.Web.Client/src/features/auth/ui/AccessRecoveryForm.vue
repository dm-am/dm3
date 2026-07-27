<script setup lang="ts">
import { ref, nextTick } from "vue";
import { useRouter } from "vue-router";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Button from "@/shared/ui/Button/Button.vue";
import { AccountApi } from "@/shared/api";
import {
  useValidatedField,
  validators,
} from "@/shared/lib/composables/useValidatedField";

const router = useRouter();

const props = defineProps<{
  prefillEmail?: string;
}>();

const emit = defineEmits<{
  (e: "cancel"): void;
}>();

// Form state
type Result = "password" | "activation" | "notfound" | null;
const result = ref<Result>(null);
const loading = ref(false);
const serverError = ref("");

// Email field with validation
const emailField = useValidatedField({
  initialValue: props.prefillEmail || "",
  validate: validators.combine(validators.required(), validators.email()),
});

const goToSupport = () => {
  emit("cancel");
  nextTick(() => router.push("/support?reason=access"));
};

const submit = async () => {
  // Validate first
  const isValid = await emailField.validate();
  if (!isValid) return;

  loading.value = true;
  serverError.value = "";

  try {
    const { data, error } = await AccountApi.recover(
      emailField.value.value.trim(),
    );

    if (error || !data) {
      serverError.value = "Произошла ошибка. Попробуйте позже.";
      return;
    }

    if (data.status === "NotFound") {
      result.value = "notfound";
    } else if (data.status === "ActivationResent") {
      result.value = "activation";
    } else {
      result.value = "password";
    }
  } catch {
    serverError.value = "Произошла ошибка. Попробуйте позже.";
  } finally {
    loading.value = false;
  }
};

const tryAgain = () => {
  result.value = null;
  emailField.reset();
  serverError.value = "";
};

const onEmailInput = () => {
  emailField.onInput();
  serverError.value = "";
};

const clearForm = () => {
  result.value = null;
  serverError.value = "";
  loading.value = false;
  emailField.reset();
};

const cancel = () => {
  clearForm();
  emit("cancel");
};
</script>

<template>
  <Dialog :narrow="!result" :auto="!!result" @before-close="clearForm">
    <!-- Result: Password reset sent -->
    <template v-if="result === 'password'">
      <div class="success-content">
        <dialog-title>Проверьте почту</dialog-title>
        <p class="main-text">
          Мы отправили письмо на
          <strong>{{ emailField.value.value }}</strong> со ссылкой для сброса
          пароля.
        </p>
        <p class="expiry-note">Ссылка действительна 48 часов</p>
        <Button type="button" class="confirm-btn" @click="cancel">
          Закрыть
        </Button>
      </div>
    </template>

    <!-- Result: Activation resent -->
    <template v-else-if="result === 'activation'">
      <div class="success-content">
        <dialog-title>Проверьте почту</dialog-title>
        <p class="main-text">
          Мы отправили повторное письмо на
          <strong>{{ emailField.value.value }}</strong> со ссылкой для
          активации.
        </p>
        <p class="expiry-note">Ссылка действительна 48 часов</p>
        <Button type="button" class="confirm-btn" @click="cancel">
          Закрыть
        </Button>
      </div>
    </template>

    <!-- Result: Not found -->
    <template v-else-if="result === 'notfound'">
      <div class="success-content">
        <dialog-title>Аккаунт не найден</dialog-title>
        <p class="main-text">
          Аккаунта с почтой <strong>{{ emailField.value.value }}</strong> не
          существует.
        </p>
        <p class="expiry-note">
          <button type="button" class="field-action" @click="tryAgain">
            Попробовать другую почту?
          </button>
        </p>
        <Button type="button" class="confirm-btn" @click="cancel">
          Закрыть
        </Button>
      </div>
    </template>

    <!-- Email form -->
    <template v-else>
      <dialog-title>Восстановление доступа</dialog-title>

      <Form
        @submit="submit"
        @cancel="cancel"
        :valid="emailField.isReady.value"
        :loading="loading"
        action="Отправить"
        cancel="Отмена"
      >
        <form-field
          name="email"
          :errors="
            emailField.error.value
              ? [emailField.error.value]
              : serverError
                ? [serverError]
                : []
          "
        >
          <template #label>
            <label for="recovery-email">Почта</label>
            <button type="button" class="field-action" @click="goToSupport">
              Нет доступа к почте?
            </button>
          </template>
          <input
            v-model="emailField.value.value"
            id="recovery-email"
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
@import "src/assets/styles/Inputs"

.field-action
  +inline-link-button

.success-content
  text-align: center

.main-text
  margin: 0 0 $small
  line-height: 1.5

.expiry-note
  margin: 0 0 $medium
  color: $text-muted
  font-size: $secondary-font-size

.confirm-btn
  width: 100%
</style>

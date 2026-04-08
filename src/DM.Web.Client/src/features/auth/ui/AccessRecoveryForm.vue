<script setup lang="ts">
import { ref, nextTick } from "vue";
import { useRouter } from "vue-router";
import Lightbox from "@/shared/ui/Layout/Lightbox.vue";
import LightboxTitle from "@/shared/ui/Layout/LightboxTitle.vue";
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
</script>

<template>
  <Lightbox :narrow="!result">
    <!-- Result: Password reset sent -->
    <template v-if="result === 'password'">
      <div class="success-content">
        <lightbox-title>Проверьте почту</lightbox-title>
        <p class="main-text">
          Мы отправили письмо на
          <strong>{{ emailField.value.value }}</strong> со ссылкой для сброса
          пароля.
        </p>
        <p class="expiry-note">Ссылка действительна 48 часов</p>
      </div>
    </template>

    <!-- Result: Activation resent -->
    <template v-else-if="result === 'activation'">
      <div class="success-content">
        <lightbox-title>Проверьте почту</lightbox-title>
        <p class="main-text">
          Мы отправили повторное письмо на
          <strong>{{ emailField.value.value }}</strong> со ссылкой для
          активации.
        </p>
        <p class="expiry-note">Ссылка действительна 48 часов</p>
      </div>
    </template>

    <!-- Result: Not found -->
    <template v-else-if="result === 'notfound'">
      <div class="success-content">
        <lightbox-title>Аккаунт не найден</lightbox-title>
        <p class="main-text">
          Аккаунта с почтой <strong>{{ emailField.value.value }}</strong> не
          существует.
        </p>
        <p class="expiry-note">
          <a href="#" @click.prevent="tryAgain">Попробовать другую почту?</a>
        </p>
      </div>
    </template>

    <!-- Email form -->
    <template v-else>
      <lightbox-title>Восстановление доступа</lightbox-title>

      <Form
        @submit="submit"
        @cancel="emit('cancel')"
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
            <a class="field-action" @click.prevent="goToSupport"
              >Нет доступа к почте?</a
            >
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
  </Lightbox>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.field-action
  cursor: pointer

.success-content
  text-align: center

.main-text
  margin: 0 0 $small
  line-height: 1.5

.expiry-note
  margin: 0
  color: $text-muted
  font-size: $secondary-font-size

  a
    color: $link
</style>

<style lang="sass">
.lightbox:has(.success-content)
  width: auto !important
  max-width: 320px !important
</style>

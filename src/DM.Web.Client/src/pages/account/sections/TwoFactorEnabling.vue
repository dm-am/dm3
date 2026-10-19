<template>
  <!-- Three steps, and the secret exists only from the second, so the fields come in that order. -->
  <Button v-if="!setupOpen" variant="primary" @click="openSetup">
    Включить
  </Button>

  <!-- In place, not in a dialog: a stray click would close it, and the secret would have to be issued again. -->
  <div class="expand-fold" :class="{ open: setupOpen }">
    <div class="expand-fold-clip" :inert="!setupOpen">
      <div class="setup-form">
        <form v-if="!setup" @submit.prevent="issueSecret">
          <p class="form-description">
            Пароль подтверждает, что двухфакторную аутентификацию включает
            владелец аккаунта
          </p>

          <FormField
            label="Текущий пароль"
            name="two-factor-password"
            :errors="setupError ? [setupError] : []"
          >
            <input
              id="two-factor-password"
              v-model="setupPassword"
              type="password"
              autocomplete="current-password"
              @input="setupError = null"
            />
          </FormField>

          <div class="form-actions">
            <Button
              type="submit"
              variant="primary"
              :loading="setupBusy"
              :disabled="!setupPassword"
            >
              Получить код
            </Button>
            <Button type="button" @click="closeSetup">Отмена</Button>
          </div>
        </form>

        <form v-else @submit.prevent="confirmSetup">
          <p class="form-description">
            Поставьте на телефон приложение-аутентификатор и наведите его камеру
            на этот код. Если камеры нет, введите в приложение строку секрета
            ниже. Приложение начнет показывать шесть цифр, которые меняются
            каждые полминуты.
          </p>

          <QrCode
            :value="setup.otpAuthUri"
            label="Секрет для приложения-аутентификатора"
            class="setup-qr"
          />

          <div class="secret-label">Или введите секрет вручную</div>
          <div class="code-block">
            <code>{{ setup.secret }}</code>
            <button
              type="button"
              class="copy-btn"
              @click="secretClip.copy(setup.secret)"
            >
              {{ secretCaption }}
            </button>
          </div>

          <FormField
            label="Код из приложения"
            name="two-factor-code"
            :errors="confirmError ? [confirmError] : []"
          >
            <input
              id="two-factor-code"
              v-model="confirmCode"
              class="code-input"
              type="text"
              inputmode="numeric"
              autocomplete="one-time-code"
              autocapitalize="off"
              autocorrect="off"
              spellcheck="false"
              @input="confirmError = null"
            />
            <template #hint> Настройку надо закончить за 30 минут </template>
          </FormField>

          <div class="form-actions">
            <Button
              type="submit"
              variant="primary"
              :loading="confirmBusy"
              :disabled="!confirmCode.trim()"
            >
              Включить
            </Button>
            <Button type="button" @click="closeSetup">Отмена</Button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * Switching the second factor on: the password that issues the secret, the
 * secret as a square and as text, and the code that confirms it. The codes it
 * hands over and a factor found on already are the section's.
 */
import { ref } from "vue";
import { accountApi } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { FormField } from "@/shared/ui/Form";
import { QrCode } from "@/shared/ui/QrCode";
import { useCopyFeedback } from "@/shared/lib/composables/useCopyFeedback";
import { refusalHere } from "@/shared/lib/errors";
import type { TwoFactorSetup } from "@/shared/api/models/account";

const emit = defineEmits<{
  /** The factor is on; carries the set of codes, in its only showing. */
  enabled: [codes: string[]];
  /** The server says the factor is on already: the state shown is old. */
  stale: [];
}>();

// The copy says its result in its own caption, its own instance.
const secretClip = useCopyFeedback();
const secretCaption = secretClip.label("Скопировать");

const setupOpen = ref(false);
const setupPassword = ref("");
const setup = ref<TwoFactorSetup | null>(null);
const setupError = ref<string | null>(null);
const setupBusy = ref(false);
const confirmCode = ref("");
const confirmError = ref<string | null>(null);
const confirmBusy = ref(false);

function openSetup() {
  setupOpen.value = true;
  setupPassword.value = "";
  setupError.value = null;
}

function closeSetup() {
  setupOpen.value = false;
  setupPassword.value = "";
  setup.value = null;
  setupError.value = null;
  confirmCode.value = "";
  confirmError.value = null;
}

async function issueSecret() {
  if (!setupPassword.value || setupBusy.value) return;

  setupBusy.value = true;
  setupError.value = null;
  const { data, error } = await accountApi.setupTwoFactor(setupPassword.value);
  setupBusy.value = false;

  if (error) {
    // A conflict: the factor is on already, so the section reads its state again.
    if (error.status === 409) {
      closeSetup();
      emit("stale");
      return;
    }
    // Under the field, the refusals the interceptor speaks for included.
    setupError.value = refusalHere(
      error,
      "Не удалось получить код, попробуйте еще раз",
    );
    return;
  }

  setup.value = data?.resource ?? null;
  setupPassword.value = "";
}

async function confirmSetup() {
  if (!confirmCode.value.trim() || confirmBusy.value) return;

  confirmBusy.value = true;
  confirmError.value = null;
  const { data, error } = await accountApi.confirmTwoFactor(confirmCode.value);
  confirmBusy.value = false;

  if (error) {
    // The same rule under the code field.
    confirmError.value = refusalHere(
      error,
      "Не удалось включить двухфакторную аутентификацию",
    );
    return;
  }

  closeSetup();
  emit("enabled", data?.resource?.codes ?? []);
}
</script>

<style scoped lang="sass">
@use "@/assets/styles/Buttons" as *
@use "../AccountPage.styles" as *

// The column of the reveal and of its two forms, as the section's pair form.
.setup-form
  display: flex
  flex-direction: column
  gap: $medium
  padding-top: $medium

.setup-form form
  display: flex
  flex-direction: column
  gap: $medium
  +account-fields

.form-description
  margin: 0
  color: $text-muted
  font-size: $secondary-font-size

.form-actions
  display: flex
  gap: $small

.setup-qr
  align-self: flex-start

.secret-label
  color: $text-muted
  font-size: $secondary-font-size

.code-block
  display: flex
  align-items: center
  gap: $small
  padding: $small $medium
  background-color: $bg-element
  font-family: $code-font

  code
    flex: 1
    word-break: break-all
    color: $text
    user-select: all

.copy-btn
  +button

.code-input
  font-family: $code-font
</style>

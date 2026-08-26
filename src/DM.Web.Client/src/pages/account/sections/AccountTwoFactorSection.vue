<template>
  <section class="section">
    <h2 class="section-title">Второй фактор</h2>

    <div class="two-factor-content">
      <!-- The set of recovery codes, on screen for the only time they exist.
           Ahead of every other branch and not inside them, because the branches
           below are decided by a read of the state, and a read that fails is
           not allowed to take the codes with it: the factor would be on and
           their owner would never have seen one of them.

           A block and not a dialog for the neighbouring reason: a dialog closes
           on a click past it, and that click would do the same thing. -->
      <div v-if="issuedCodes" class="codes-block">
        <h3 class="subsection-title">
          {{ issuedByReissue ? "Новые резервные коды" : "Резервные коды" }}
        </h3>
        <p v-if="!issuedByReissue" class="codes-note">Второй фактор включен.</p>
        <p class="codes-warning">
          Коды показываются один раз и больше не откроются. Сохраните их: каждый
          код срабатывает только один раз и заменяет код из приложения, когда
          телефона нет под рукой.
        </p>
        <p v-if="issuedByReissue" class="codes-note">
          Коды перевыпущены. Прежний набор кодов больше не работает.
        </p>

        <ul class="codes-grid">
          <li v-for="code in issuedCodes" :key="code" class="code-item">
            {{ grouped(code) }}
          </li>
        </ul>

        <Button @click="copyCodes">Скопировать все</Button>

        <label class="codes-saved">
          <input v-model="codesSaved" type="checkbox" />
          Коды сохранены
        </label>

        <Button :disabled="!codesSaved" @click="dismissCodes">Готово</Button>
      </div>

      <div v-else-if="loading" class="loading-state">Загрузка...</div>

      <!-- The state could not be read. Offering "Включить" here would promise
           an operation the server may already have refused as "уже включен". -->
      <div v-else-if="loadError" class="status-card status-card--failed">
        <div class="status-header">
          <span class="status-icon">{{ symbols.cross }}</span>
          <span class="status-title">{{ loadError }}</span>
        </div>
        <p class="status-note">Обновите страницу, чтобы попробовать снова.</p>
      </div>

      <template v-else-if="status">
        <!-- Somebody asked, from a mailbox, to have this factor taken off. The
             only place inside the account where that request is visible. -->
        <div
          v-if="status.removalDueUtc"
          class="status-card status-card--scheduled"
        >
          <div class="status-header">
            <SvgIcon name="clock" class="status-icon" />
            <span class="status-title">
              Снятие второго фактора назначено на
              {{ formatDateFull(status.removalDueUtc) }}
            </span>
          </div>
          <p class="status-note">
            Любой вход со вторым фактором отменяет снятие. Отменить его можно и
            ссылкой из письма.
          </p>
        </div>

        <!-- The factor is on: what it knows about itself, and the two ways out. -->
        <template v-if="status.enabled">
          <div class="status-card status-card--enabled">
            <div class="status-header">
              <SvgIcon name="locked" class="status-icon" />
              <span class="status-title">Второй фактор включен</span>
            </div>
            <div class="status-details">
              <div class="status-row">
                <span class="status-label">Включен:</span>
                <span>{{ formatDateFull(status.enabledUtc) }}</span>
              </div>
              <div class="status-row">
                <span class="status-label">Последнее подтверждение:</span>
                <span>{{ formatDateFull(status.lastVerifiedUtc) }}</span>
              </div>
              <div class="status-row">
                <span class="status-label">Резервных кодов осталось:</span>
                <span>{{ status.recoveryCodesLeft }}</span>
              </div>
            </div>
            <!-- Named in words, not only painted: a reader who cannot tell the
                 colour apart still gets the sentence. -->
            <p v-if="lowOnCodes" class="codes-low">
              Резервных кодов почти не осталось.
              <button
                type="button"
                class="field-action"
                @click="open('reissue')"
              >
                Перевыпустить
              </button>
            </p>
          </div>

          <div v-if="!action" class="two-factor-actions">
            <Button @click="open('reissue')">Перевыпустить коды</Button>
            <Button class="action-danger" @click="open('disable')">
              Отключить
            </Button>
          </div>

          <!-- Both operations cost the same and ask for the same pair, so they
               share one form. A confirmation dialog would not do: it is built
               for a confirmation that takes no input. -->
          <div class="expand-fold" :class="{ open: !!action }">
            <div class="expand-fold-clip" :inert="!action">
              <form class="confirm-form" @submit.prevent="runAction">
                <p class="form-description">
                  {{
                    action === "disable"
                      ? "Чтобы отключить второй фактор, введите пароль и код."
                      : "Чтобы перевыпустить коды, введите пароль и код. Прежний набор перестанет работать."
                  }}
                </p>

                <FormField
                  label="Текущий пароль"
                  name="two-factor-action-password"
                  :errors="actionPasswordError ? [actionPasswordError] : []"
                >
                  <input
                    id="two-factor-action-password"
                    v-model="actionPassword"
                    type="password"
                    autocomplete="current-password"
                    @input="clearActionErrors"
                  />
                </FormField>

                <FormField
                  label="Код"
                  name="two-factor-action-code"
                  :errors="actionCodeError ? [actionCodeError] : []"
                >
                  <input
                    id="two-factor-action-code"
                    v-model="actionCode"
                    class="code-input"
                    type="text"
                    inputmode="text"
                    autocomplete="one-time-code"
                    autocapitalize="off"
                    autocorrect="off"
                    spellcheck="false"
                    @input="clearActionErrors"
                  />
                  <template #hint>
                    Код из приложения или один из резервных кодов
                  </template>
                </FormField>

                <div class="form-actions">
                  <Button
                    type="submit"
                    :class="{ 'action-danger': action === 'disable' }"
                    :loading="actionBusy"
                    :disabled="!actionReady"
                  >
                    {{ action === "disable" ? "Отключить" : "Перевыпустить" }}
                  </Button>
                  <Button type="button" @click="closeAction">Отмена</Button>
                </div>
              </form>
            </div>
          </div>
        </template>

        <!-- The factor is off. Switching it on is three steps and the secret
             does not exist until the second, so the fields appear in that
             order rather than all at once. -->
        <template v-else>
          <div class="state-line">Второй фактор выключен</div>

          <Button v-if="!setupOpen" @click="openSetup">Включить</Button>

          <!-- Revealed in place and not in a dialog: confirming takes minutes,
               a dialog closes on a stray click, and a secret closed away half
               way through has to be issued again. -->
          <div class="expand-fold" :class="{ open: setupOpen }">
            <div class="expand-fold-clip" :inert="!setupOpen">
              <div class="setup-form">
                <form v-if="!setup" @submit.prevent="issueSecret">
                  <p class="form-description">
                    Пароль спрашивается еще раз, чтобы второй фактор нельзя было
                    поставить с чужого устройства, где вход в аккаунт уже
                    открыт.
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
                    Поставьте на телефон приложение-аутентификатор и наведите
                    его камеру на этот код. Если камеры нет, введите в
                    приложение строку секрета ниже. Приложение начнет показывать
                    шесть цифр, которые меняются каждые полминуты.
                  </p>

                  <QrCode
                    :value="setup.otpAuthUri"
                    label="Код для приложения-аутентификатора"
                    class="setup-qr"
                  />

                  <div class="secret-label">Или введите секрет вручную</div>
                  <div class="code-block">
                    <code>{{ setup.secret }}</code>
                    <button
                      type="button"
                      class="copy-btn"
                      @click="copySecret(setup.secret)"
                    >
                      Скопировать
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
                    <template #hint>
                      Настройку надо закончить за 30 минут
                    </template>
                  </FormField>

                  <div class="form-actions">
                    <Button
                      type="submit"
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
      </template>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { accountApi } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { FormField } from "@/shared/ui/Form";
import { SvgIcon } from "@/shared/ui/Icon";
import { QrCode } from "@/shared/ui/QrCode";
import { symbols } from "@/shared/lib/utils/icons";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useToast } from "@/shared/lib/composables/useToast";
import { describeFailure } from "@/shared/lib/errors";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";
import type {
  TwoFactorSetup,
  TwoFactorStatus,
} from "@/shared/api/models/account";
import type { BadRequestError, GeneralError } from "@/shared/api/models/common";

const toast = useToast();

const LOAD_FAILED = "Не удалось узнать состояние второго фактора";

/** Below this many codes left the row stops being a fact and becomes a warning. */
const LOW_CODES = 2;

const loading = ref(true);
const loadError = ref<string | null>(null);
const status = ref<TwoFactorStatus | null>(null);

// Switching the factor on.
const setupOpen = ref(false);
const setupPassword = ref("");
const setup = ref<TwoFactorSetup | null>(null);
const setupError = ref<string | null>(null);
const setupBusy = ref(false);
const confirmCode = ref("");
const confirmError = ref<string | null>(null);
const confirmBusy = ref(false);

// The set of codes, held only for as long as it is on screen. It is in no
// answer but the one that issued it, and in no store: a copy kept anywhere
// would outlive the single showing the whole design rests on.
const issuedCodes = ref<string[] | null>(null);
const issuedByReissue = ref(false);
const codesSaved = ref(false);

/** Reissuing the codes and switching the factor off cost the same pair. */
type ConfirmedAction = "reissue" | "disable";
const action = ref<ConfirmedAction | null>(null);
const actionPassword = ref("");
const actionCode = ref("");
const actionPasswordError = ref<string | null>(null);
const actionCodeError = ref<string | null>(null);
const actionBusy = ref(false);

const lowOnCodes = computed(
  () => (status.value?.recoveryCodesLeft ?? 0) <= LOW_CODES,
);

const actionReady = computed(
  () => actionPassword.value.length > 0 && actionCode.value.trim().length > 0,
);

/** "ABCD-EFGH-IJKL-MNOP". The server drops the separators back out on the way in. */
const grouped = (code: string) => code.replace(/(.{4})(?=.)/g, "$1-");

onMounted(loadStatus);

async function loadStatus() {
  loading.value = true;
  const { data, error } = await accountApi.getTwoFactorStatus();
  loading.value = false;

  loadError.value = error ? describeFailure(error, LOAD_FAILED) : null;
  status.value = data?.resource ?? null;
}

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
    // A conflict says the factor is already on: this section is looking at a
    // state somebody else changed, and re-reading it is the answer, not a
    // sentence under the password field.
    if (error.status === 409) {
      closeSetup();
      await loadStatus();
      return;
    }
    setupError.value = describeFailure(
      error,
      "Не удалось получить код. Попробуйте еще раз.",
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
    confirmError.value = describeFailure(
      error,
      "Не удалось включить второй фактор",
    );
    return;
  }

  showCodes(data?.resource?.codes ?? [], false);
  closeSetup();
  await loadStatus();
}

function open(next: ConfirmedAction) {
  action.value = next;
  actionPassword.value = "";
  actionCode.value = "";
  clearActionErrors();
}

function closeAction() {
  action.value = null;
  actionPassword.value = "";
  actionCode.value = "";
  clearActionErrors();
}

function clearActionErrors() {
  actionPasswordError.value = null;
  actionCodeError.value = null;
}

/**
 * Puts the refusal under the field it belongs to. A wrong password comes back
 * named ("password"), a code that did not match comes back as one sentence
 * with no field on it - and under the password field that sentence would send
 * the reader to retype a password that was right.
 */
function reportActionFailure(
  failure: GeneralError | BadRequestError,
  fallback: string,
) {
  const named = getFieldError(parseApiErrors(failure), "password");
  if (named) {
    actionPasswordError.value = named;
    return;
  }
  actionCodeError.value = describeFailure(failure, fallback);
}

async function runAction() {
  if (!action.value || !actionReady.value || actionBusy.value) return;

  const request = { password: actionPassword.value, code: actionCode.value };
  const reissuing = action.value === "reissue";

  actionBusy.value = true;
  clearActionErrors();
  const { data, error } = reissuing
    ? await accountApi.reissueRecoveryCodes(request)
    : await accountApi.disableTwoFactor(request);
  actionBusy.value = false;

  if (error) {
    reportActionFailure(
      error,
      reissuing
        ? "Не удалось перевыпустить коды"
        : "Не удалось отключить второй фактор",
    );
    return;
  }

  closeAction();
  if (reissuing) showCodes(data?.resource?.codes ?? [], true);
  else toast.success("Второй фактор отключен");
  await loadStatus();
}

function showCodes(codes: string[], afterReissue: boolean) {
  issuedCodes.value = codes;
  issuedByReissue.value = afterReissue;
  codesSaved.value = false;
}

function dismissCodes() {
  issuedCodes.value = null;
  codesSaved.value = false;
}

async function copySecret(secret: string) {
  try {
    await navigator.clipboard.writeText(secret);
    toast.success("Секрет скопирован");
  } catch {
    toast.error("Не удалось скопировать");
  }
}

async function copyCodes() {
  if (!issuedCodes.value) return;
  try {
    await navigator.clipboard.writeText(
      issuedCodes.value.map(grouped).join("\n"),
    );
    toast.success("Резервные коды скопированы");
  } catch {
    toast.error("Не удалось скопировать");
  }
}
</script>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "../AccountPage.styles" as *

.two-factor-content
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.loading-state
  padding: $medium

.state-line
  margin-bottom: $medium
  color: $text-muted

.subsection-title
  margin: 0 0 $small
  color: $heading

// Status cards — the same recipe the neighbouring sections use.
.status-card
  padding: $medium
  border-radius: $border-radius
  margin-bottom: $medium

  &--enabled
    +tint($accent-green, 15%)
    border: 1px solid $accent-green

  &--scheduled
    +tint($accent-yellow, 15%)
    border: 1px solid $accent-yellow

  &--failed
    +tint($accent-red, 15%)
    border: 1px solid $accent-red

.status-header
  display: flex
  align-items: center
  gap: $small
  margin-bottom: $small

.status-icon
  font-size: 1.2em

  .status-card--enabled &
    color: $accent-green

  .status-card--scheduled &
    color: $accent-yellow

  .status-card--failed &
    color: $accent-red

.status-title
  font-weight: 600

  .status-card--enabled &
    color: $accent-green

  .status-card--scheduled &
    color: $accent-yellow

  .status-card--failed &
    color: $accent-red

.status-details
  display: flex
  flex-direction: column
  gap: $tiny

.status-row
  display: flex
  gap: $small
  font-size: $secondary-font-size

.status-label
  color: $text-muted
  flex-shrink: 0

.status-note
  margin: 0
  font-size: $secondary-font-size
  color: $text-muted

.codes-low
  margin: $small 0 0
  font-size: $secondary-font-size
  color: $accent-yellow

.field-action
  +inline-link-button

.two-factor-actions
  display: flex
  flex-wrap: wrap
  gap: $small

// Switching the factor off is destructive, and it is marked the way the rest
// of the site marks a destructive control: the shared danger fill. The class
// lands on the shared Button, whose root carries this scope as well.
.action-danger
  +button-danger

// Forms
.setup-form,
.confirm-form
  display: flex
  flex-direction: column
  gap: $medium
  padding-top: $medium

.setup-form form
  display: flex
  flex-direction: column
  gap: $medium

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
  background-color: $bg
  border-radius: $border-radius
  font-family: monospace

  code
    flex: 1
    word-break: break-all
    color: $text
    user-select: all

.copy-btn
  +button

.code-input
  font-family: monospace

// The set of codes
.codes-block
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $medium

.codes-warning
  margin: 0
  padding: $small $medium
  +tint($accent-yellow, 15%)
  border: 1px solid $accent-yellow
  color: $text

.codes-note
  margin: 0
  font-size: $secondary-font-size
  color: $text-muted

.codes-grid
  display: grid
  grid-template-columns: repeat(2, minmax(0, 1fr))
  gap: $small
  width: 100%
  margin: 0
  padding: 0
  list-style: none

  @media (max-width: $bp-mobile)
    grid-template-columns: minmax(0, 1fr)

.code-item
  padding: $small $medium
  background-color: $bg
  border-radius: $border-radius
  font-family: monospace
  user-select: all

.codes-saved
  display: flex
  align-items: center
  gap: $small
</style>

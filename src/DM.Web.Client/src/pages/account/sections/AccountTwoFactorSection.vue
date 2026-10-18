<template>
  <section class="section">
    <h2 class="section-title">Двухфакторная аутентификация</h2>

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
        <p v-if="!issuedByReissue" class="codes-note">
          Двухфакторная аутентификация включена
        </p>
        <p class="codes-warning">
          Коды показываются один раз и больше не откроются. Сохраните их: каждый
          код срабатывает только один раз и заменяет код из приложения, когда
          телефона нет под рукой.
        </p>
        <p v-if="issuedByReissue" class="codes-note">
          Коды перевыпущены. Новый набор заменил прежний.
        </p>

        <ul class="codes-grid">
          <li v-for="code in issuedCodes" :key="code" class="code-item">
            {{ grouped(code) }}
          </li>
        </ul>

        <Button @click="copyCodes">{{ codesCaption }}</Button>

        <label class="codes-saved">
          <input v-model="codesSaved" type="checkbox" />
          Коды сохранены
        </label>

        <Button
          type="button"
          variant="primary"
          :disabled="!codesSaved"
          @click="dismissCodes"
        >
          Готово
        </Button>
      </div>

      <div v-else-if="loading" class="loading-state">Загрузка...</div>

      <!-- The state could not be read. Offering "Включить" here would promise
           an operation the server may already have refused as "уже включена". -->
      <div v-else-if="loadError" class="status-card status-card--failed">
        <div class="status-header">
          <span class="status-title">{{ loadError }}</span>
        </div>
        <p class="status-note">Обновите страницу, чтобы попробовать снова</p>
      </div>

      <template v-else-if="status">
        <!-- Somebody asked, from a mailbox, to have this factor taken off. The
             only place inside the account where that request is visible. -->
        <div
          v-if="status.removalDueUtc"
          class="status-card status-card--scheduled"
        >
          <div class="status-header">
            <span class="status-title">
              Отключение двухфакторной аутентификации назначено на
              {{ formatDateFull(status.removalDueUtc) }}
            </span>
          </div>
          <p class="status-note">
            Любой вход с кодом отменяет отключение. Отменить его можно и ссылкой
            из письма.
          </p>
        </div>

        <!-- The factor is on: what it knows about itself, and the two ways out. -->
        <template v-if="status.enabled">
          <div class="status-card status-card--enabled">
            <div class="status-header">
              <span class="status-title">
                Двухфакторная аутентификация включена
              </span>
            </div>
            <!-- Lines of text and not flex rows, so each copies as
                 "Включена: 01.09.2026 в 09:37"; a value is never broken in
                 two, and where a line has no room for it beside its label
                 it goes under the label whole. -->
            <div class="status-details">
              <div
                v-for="row in factorRows"
                :key="row.label"
                class="status-row"
              >
                <span class="status-label">{{ row.label }}</span
                >{{ " " }}<span class="status-value">{{ row.value }}</span>
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
            <Button @click="open('disable')">Отключить</Button>
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
                      ? "Чтобы отключить двухфакторную аутентификацию, введите пароль и код"
                      : "Чтобы перевыпустить коды, введите пароль и код. Новый набор заменит прежний."
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
                    variant="primary"
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
          <div class="state-line">Двухфакторная аутентификация выключена</div>

          <Button v-if="!setupOpen" variant="primary" @click="openSetup">
            Включить
          </Button>

          <!-- Revealed in place and not in a dialog: confirming takes minutes,
               a dialog closes on a stray click, and a secret closed away half
               way through has to be issued again. -->
          <div class="expand-fold" :class="{ open: setupOpen }">
            <div class="expand-fold-clip" :inert="!setupOpen">
              <div class="setup-form">
                <form v-if="!setup" @submit.prevent="issueSecret">
                  <p class="form-description">
                    Пароль подтверждает, что двухфакторную аутентификацию
                    включает владелец аккаунта
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
                    Поставьте на телефон приложение-аутентификатор и наведите
                    его камеру на этот код. Если камеры нет, введите в
                    приложение строку секрета ниже. Приложение начнет показывать
                    шесть цифр, которые меняются каждые полминуты.
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
                      @click="copySecret(setup.secret)"
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
                    <template #hint>
                      Настройку надо закончить за 30 минут
                    </template>
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
      </template>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { accountApi } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { FormField } from "@/shared/ui/Form";
import { QrCode } from "@/shared/ui/QrCode";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useCopyFeedback } from "@/shared/lib/composables/useCopyFeedback";
import {
  describeFailure,
  describeRateLimit,
  refusalHere,
} from "@/shared/lib/errors";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";
import type {
  TwoFactorSetup,
  TwoFactorStatus,
} from "@/shared/api/models/account";
import type { BadRequestError, GeneralError } from "@/shared/api/models/common";

// The clipboard is the one result the screen cannot show, so each copy control
// answers for itself in its own caption. One instance per control: a shared one
// would have a press on either of them answer in both captions at once.
const secretClip = useCopyFeedback();
const secretCaption = secretClip.label("Скопировать");
const codesClip = useCopyFeedback();
const codesCaption = codesClip.label("Скопировать все");

const LOAD_FAILED = "Не удалось узнать состояние двухфакторной аутентификации";

/**
 * The read this section answers for itself.
 *
 * A failed read is drawn as a card standing in place of the whole block, and
 * the way in to switching the factor on goes with it, so the card is where the
 * reader is looking and there is nothing for a plate in the corner to add. Set
 * here and not inside the request: the same endpoint read from a screen that
 * shows nothing must keep its plate.
 */
const OWNED_BY_CARD = { ownsRefusal: true, ownsRateLimit: true } as const;

/** Below this many codes left the row stops being a fact and becomes a warning. */
const LOW_CODES = 2;

const loading = ref(true);
const loadError = ref<string | null>(null);
const status = ref<TwoFactorStatus | null>(null);

/** What the factor knows about itself, a line each under its state. */
const factorRows = computed(() => [
  { label: "Включена:", value: formatDateFull(status.value?.enabledUtc) },
  {
    label: "Последнее подтверждение:",
    value: formatDateFull(status.value?.lastVerifiedUtc),
  },
  {
    label: "Резервных кодов осталось:",
    value: String(status.value?.recoveryCodesLeft ?? 0),
  },
]);

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

/**
 * What the card says about a read that failed.
 *
 * The server's own sentence wherever there is one. The card stands instead of
 * the whole block, so it is the one carrier of the failure, and a refusal names
 * something the section cannot work out for itself - a rank this block is
 * closed for, a state the account is in. There is no plate over it saying the
 * same thing a line higher: the request took the refusal and the limit over.
 *
 * Two statuses keep the section's own line, because the interceptor still
 * speaks for them and knows more about them than this block does: a session
 * that ended is about the session and not about the factor, and a request that
 * never went out has no sentence of its own to relay.
 *
 * The wait is built rather than relayed. The seconds the server named travel in
 * Retry-After and the transport copies them onto the failure; the title of a
 * 429 carries no number at all, and a wait with no number is the one thing the
 * reader cannot act on.
 */
function describeLoadFailure(failure: GeneralError): string {
  if (failure.status === 0 || failure.status === 401) return LOAD_FAILED;
  if (failure.status === 429)
    return describeRateLimit(failure.retryAfterSeconds);
  return describeFailure(failure, LOAD_FAILED);
}

async function loadStatus() {
  loading.value = true;
  const { data, error } = await accountApi.getTwoFactorStatus(OWNED_BY_CARD);
  loading.value = false;

  loadError.value = error ? describeLoadFailure(error) : null;
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
    // Under the field, the refusals the interceptor speaks for included: the
    // password field is still there, and its plate is taken down.
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
    // Same rule as the password above: the code field stays on screen, and
    // the line under it says what the plate used to.
    confirmError.value = refusalHere(
      error,
      "Не удалось включить двухфакторную аутентификацию",
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
  // Neither field for an expired session or a dead connection, which stay with
  // the plate. Every other refusal is said here, and the plate the interceptor
  // held for it is taken down.
  const text = refusalHere(failure, fallback);
  if (text === null) return;

  const named = getFieldError(parseApiErrors(failure), "password");
  if (named) {
    actionPasswordError.value = named;
    return;
  }
  actionCodeError.value = text;
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
        : "Не удалось отключить двухфакторную аутентификацию",
    );
    return;
  }

  closeAction();
  // Only the reissue branch has something to show; switching the factor off
  // says so by itself - loadStatus swaps the section to its disabled state.
  if (reissuing) showCodes(data?.resource?.codes ?? [], true);
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
  await secretClip.copy(secret);
}

async function copyCodes() {
  if (!issuedCodes.value) return;
  // Copied the way they are read: grouped by four, one code to a line.
  await codesClip.copy(issuedCodes.value.map(grouped).join("\n"));
}
</script>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "../AccountPage.styles" as *

.two-factor-content
  padding: $medium
  background-color: $bg-element

.loading-state
  padding: $medium

.state-line
  margin-bottom: $medium
  color: $text-muted

.subsection-title
  margin: 0 0 $small
  color: $heading

// The state of the factor is written in words, as a state is across the site,
// and not drawn as a panel: no fill, no frame, no mark beside it, the same as
// the username change further down. Green for a factor in force, red for a
// state that could not be read; a removal that is only scheduled keeps the
// text's own colour, and its date says the rest.
.status-card
  margin-bottom: $medium

.status-header
  margin-bottom: $small

.status-title
  font-weight: 600

  .status-card--enabled &
    color: $accent-green

  .status-card--failed &
    color: $accent-red

.status-details
  display: flex
  flex-direction: column
  gap: $tiny

.status-row
  font-size: $secondary-font-size

.status-label
  margin-right: $tiny
  color: $text-muted

.status-value
  white-space: nowrap

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
  border: 1px solid $border-accent-yellow
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

.code-item
  padding: $small $medium
  background-color: $bg
  font-family: monospace
  user-select: all

.codes-saved
  display: flex
  align-items: center
  gap: $small
</style>

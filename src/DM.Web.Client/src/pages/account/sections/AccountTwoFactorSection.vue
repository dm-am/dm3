<template>
  <section class="section">
    <h2 class="section-title">Двухфакторная аутентификация</h2>

    <div class="two-factor-content">
      <!-- The recovery codes, on screen the one time they exist: ahead of every
           branch, so a failed read of the state cannot take them, and a block, not a
           dialog a stray click would close. -->
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

      <!-- The state line and its press wait in their shape, without a word. -->
      <AccountSectionSkeleton v-else-if="loading" shape="rows" :count="1" />

      <!-- The state could not be read: no "Включить" over a factor that may be on. -->
      <ErrorState
        v-else-if="loadError"
        :message="loadError"
        :retry="loadStatus"
      />

      <template v-else-if="status">
        <!-- A removal asked for from a mailbox, the one place in the account that shows it. -->
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

        <!-- The factor is on: what it knows about itself and the two ways out. -->
        <template v-if="status.enabled">
          <div class="status-card status-card--enabled">
            <div class="status-header">
              <span class="status-title">
                Двухфакторная аутентификация включена
              </span>
            </div>
            <!-- Lines of text, each copied whole; a value never breaks and goes under its label whole. -->
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
            <!-- Named in words, not only in colour. -->
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

          <!-- Both cost the same pair, so they share one form; a confirmation takes no input. -->
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

        <!-- The factor is off: the state in words and the way to switch it on. -->
        <template v-else>
          <div class="state-line">Двухфакторная аутентификация выключена</div>

          <TwoFactorEnabling @enabled="enabled" @stale="loadStatus" />
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
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { useCopyFeedback } from "@/shared/lib/composables/useCopyFeedback";
import { refusalHere } from "@/shared/lib/errors";
import { parseApiErrors } from "@/shared/lib/utils/apiErrors";
import { readFieldError } from "@/shared/lib/errors/validationErrors";
import type { TwoFactorStatus } from "@/shared/api/models/account";
import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import { ErrorState } from "@/shared/ui/ErrorState";
import TwoFactorEnabling from "./TwoFactorEnabling.vue";
import AccountSectionSkeleton from "./AccountSectionSkeleton.vue";
import { describeLoadFailure, OWNED_BY_CARD } from "./loadFailure";

// The copy says its result in its own caption, one instance per control.
const codesClip = useCopyFeedback();
const codesCaption = codesClip.label("Скопировать все");

const LOAD_FAILED = "Не удалось узнать состояние двухфакторной аутентификации";

/** Below this many codes left, the row is a warning. */
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

// The codes, held only while on screen: no answer but the issuing one has them, and no store.
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

/** "ABCD-EFGH-IJKL-MNOP"; the server drops the separators on the way in. */
const grouped = (code: string) => code.replace(/(.{4})(?=.)/g, "$1-");

onMounted(loadStatus);

async function loadStatus() {
  loading.value = true;
  const { data, error } = await accountApi.getTwoFactorStatus(OWNED_BY_CARD);
  loading.value = false;

  loadError.value = error ? describeLoadFailure(error, LOAD_FAILED) : null;
  status.value = data?.resource ?? null;
}

/** The factor is on: its codes go up ahead of every branch, and the state is read again. */
async function enabled(codes: string[]) {
  showCodes(codes, false);
  await loadStatus();
}

function open(next: ConfirmedAction) {
  closeAction();
  action.value = next;
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

/** A refusal under its field: a named wrong password under the password, an unnamed sentence under the code. */
function reportActionFailure(
  failure: GeneralError | BadRequestError,
  fallback: string,
) {
  // Neither field for an expired session or a dead connection, the plate's.
  const text = refusalHere(failure, fallback);
  if (text === null) return;

  const named = readFieldError(parseApiErrors(failure), "password");
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
  // Only the reissue has something to show; the off state redraws itself on the re-read.
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

async function copyCodes() {
  if (!issuedCodes.value) return;
  // Copied as read: grouped by four, a code to a line.
  await codesClip.copy(issuedCodes.value.map(grouped).join("\n"));
}
</script>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *
@use "../AccountPage.styles" as *

.two-factor-content
  padding: $medium
  background-color: $bg-element

.state-line
  margin-bottom: $medium
  color: $text-muted

.subsection-title
  margin: 0 0 $small
  color: $heading

// The state in words, not a panel: green for a factor in force.
.status-card
  margin-bottom: $medium

.status-header
  margin-bottom: $small

.status-title
  font-weight: 700

  .status-card--enabled &
    color: $accent-green

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

// The form of the pair, as the forms of TwoFactorEnabling stand.
.confirm-form
  display: flex
  flex-direction: column
  gap: $medium
  padding-top: $medium
  +account-fields

.form-description
  margin: 0
  color: $text-muted
  font-size: $secondary-font-size

.form-actions
  display: flex
  gap: $small

.code-input
  font-family: $code-font

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
  background-color: $bg-element
  font-family: $code-font
  user-select: all

.codes-saved
  display: flex
  align-items: center
  gap: $small
</style>

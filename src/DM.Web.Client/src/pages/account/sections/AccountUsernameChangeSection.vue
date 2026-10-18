<template>
  <section class="section">
    <h2 class="section-title">Смена имени пользователя</h2>

    <div class="username-change-content">
      <div class="current-value">
        Текущее имя: <strong>{{ user.username }}</strong>
      </div>

      <!-- Loading state -->
      <div v-if="loading" class="loading-state">Загрузка...</div>

      <!-- The state could not be read: offering the form here would promise
           something the server may refuse. -->
      <div v-else-if="loadError" class="status-card status-card--failed">
        <div class="status-header">
          <span class="status-title">{{ loadError }}</span>
        </div>
        <p class="status-note">Обновите страницу, чтобы попробовать снова</p>
      </div>

      <!-- Pending request status card -->
      <div
        v-else-if="existingRequest && existingRequest.status === 'Pending'"
        class="status-card status-card--pending"
      >
        <div class="status-header">
          <span class="status-title">Заявка на рассмотрении</span>
        </div>
        <div class="status-details">
          <div class="status-row">
            <span class="status-label">Причина:</span>
            <span>{{ existingRequest.reason }}</span>
          </div>
          <div class="status-row">
            <span class="status-label">Подано:</span>
            <span>{{ formatDate(existingRequest.createdUtc) }}</span>
          </div>
        </div>
        <p class="status-note">
          Заявка рассматривается модераторами. После одобрения вы сможете
          выбрать новое имя.
        </p>
      </div>

      <!-- Rejected request notification -->
      <div
        v-else-if="existingRequest && existingRequest.status === 'Rejected'"
        class="status-card status-card--rejected"
      >
        <div class="status-header">
          <span class="status-title">Заявка отклонена</span>
        </div>
        <div class="status-details">
          <div v-if="existingRequest.resolverComment" class="status-row">
            <span class="status-label">Комментарий:</span>
            <span>{{ existingRequest.resolverComment }}</span>
          </div>
          <div v-if="existingRequest.resolvedByUsername" class="status-row">
            <span class="status-label">Модератор:</span>
            <span>{{ existingRequest.resolvedByUsername }}</span>
          </div>
        </div>
        <p class="status-note">Вы можете подать новую заявку</p>
      </div>

      <!-- Approved notification -->
      <div
        v-else-if="existingRequest && existingRequest.status === 'Approved'"
        class="status-card status-card--approved"
      >
        <div class="status-header">
          <span class="status-title">Заявка одобрена</span>
        </div>
        <p class="status-note">
          Ваша заявка на смену имени одобрена. Проверьте почту для получения
          ссылки на выбор нового имени.
        </p>
      </div>

      <!-- Expired notification -->
      <div
        v-else-if="existingRequest && existingRequest.status === 'Expired'"
        class="status-card status-card--expired"
      >
        <div class="status-header">
          <span class="status-title">Заявка истекла</span>
        </div>
        <div class="status-details">
          <div v-if="existingRequest.resolverComment" class="status-row">
            <span class="status-label">Комментарий:</span>
            <span>{{ existingRequest.resolverComment }}</span>
          </div>
          <div v-if="existingRequest.resolvedByUsername" class="status-row">
            <span class="status-label">Модератор:</span>
            <span>{{ existingRequest.resolvedByUsername }}</span>
          </div>
        </div>
        <p class="status-note">{{ expiryNote }}</p>
      </div>

      <!-- Completed notification -->
      <div
        v-else-if="existingRequest && existingRequest.status === 'Completed'"
        class="status-card status-card--completed"
      >
        <div class="status-header">
          <span class="status-title">Имя изменено</span>
        </div>
        <p class="status-note">Заявка выполнена. Вы можете подать новую.</p>
      </div>

      <ActionResult :text="conflictRefusal" />

      <!-- Create request form -->
      <form
        v-if="canCreateRequest"
        class="request-form"
        @submit.prevent="submitRequest"
      >
        <p class="form-description">
          Для смены имени пользователя необходимо одобрение модератора. После
          одобрения вы получите ссылку на выбор нового имени.
        </p>

        <FormField
          label="Причина смены"
          name="username-reason"
          :errors="submitError ? [submitError] : []"
        >
          <textarea
            id="username-reason"
            v-model="reason"
            :disabled="submitting"
            rows="3"
            maxlength="500"
            placeholder="Объясните, почему хотите сменить имя..."
          ></textarea>
          <div class="char-count">{{ reason.length }}/500</div>
          <template #hint>Причина смены имени видна модераторам</template>
        </FormField>

        <div class="form-note">
          Заявка будет рассмотрена модераторами. Обычно это занимает 1-3 дня.
        </div>

        <Button
          type="submit"
          variant="primary"
          class="request-submit"
          :loading="submitting"
          :disabled="!canSubmit"
        >
          Отправить заявку
        </Button>
      </form>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { formatDate } from "@/shared/lib/utils/datetime";
import { accountApi } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { FormField } from "@/shared/ui/Form";
import {
  describeFailure,
  describeRateLimit,
  refusalHere,
} from "@/shared/lib/errors";
import { ActionResult } from "@/shared/ui/ActionResult";
import type { User } from "@/shared/api/models/community";
import type { GeneralError } from "@/shared/api/models/common";
import type { UsernameChangeRequest } from "@/shared/api/models/account";

defineProps<{
  user: User;
}>();

const loading = ref(true);
const existingRequest = ref<UsernameChangeRequest | null>(null);

const reason = ref("");
const submitting = ref(false);
const submitError = ref<string | null>(null);
/**
 * Why the request was turned down as a conflict, under the card of the state
 * that stands in the form's place once the section is read again.
 */
const conflictRefusal = ref<string | null>(null);
const loadError = ref<string | null>(null);

const LOAD_FAILED = "Не удалось узнать состояние заявки";

/**
 * The read this section answers for itself.
 *
 * A failed read is a card in place of the state and the gate that shuts the
 * form under it, so the card is the one carrier of the failure and a plate in
 * the corner has nothing to add to it. Set here and not inside the request: the
 * profile page reads the same thing and shows nothing about a failure, and a
 * flag fixed in the service would take its plate away too.
 */
const OWNED_BY_CARD = { ownsRefusal: true, ownsRateLimit: true } as const;

// A request in flight is one awaiting a moderator, or an approval whose name is
// not yet chosen: those hold a change already asked for. Every other state holds
// nothing - rejection and both expiries granted no name, completion spent its
// grant - and asking again is the only way forward. The server draws the same
// line, and used to be the more permissive of the two.
const IN_FLIGHT = ["Pending", "Approved"];

const canCreateRequest = computed(() => {
  if (loading.value || loadError.value) return false;
  if (!existingRequest.value) return true;
  return !IN_FLIGHT.includes(existingRequest.value.status);
});

const expiryNote = computed(() => {
  const lapsed = existingRequest.value?.expiryReason === "ApprovalLapsed";
  return lapsed
    ? "Заявка была одобрена, но новое имя не выбрано в отведенное время. Вы можете подать новую."
    : "Модераторы не рассмотрели заявку за отведенный срок. Вы можете подать новую.";
});

const canSubmit = computed(() => {
  return reason.value.trim().length >= 10 && !submitting.value;
});

onMounted(async () => {
  await loadExistingRequest();
});

/**
 * What the card says about a read that failed.
 *
 * A failed read used to be indistinguishable from "never asked": the section
 * offered the form to someone whose request was already in flight, and the
 * server refused it with a conflict that landed as a validation note under the
 * reason field, where there was nothing to correct. So the card stands in place
 * of the state and closes the form.
 *
 * Which makes it the one carrier of the failure, and it says what the server
 * said: a refusal names why this block is shut, where the section's own line
 * names only that something is missing. Nothing stands over it repeating that -
 * the request took the refusal and the limit over from the interceptor.
 *
 * Two statuses keep the section's own line, because the interceptor still
 * speaks for them and knows more about them than this block does: a session
 * that ended is about the session, and a request that never went out has no
 * sentence of its own.
 *
 * The wait is built rather than relayed: the seconds the server named ride in
 * Retry-After and the transport copies them onto the failure, while the title
 * of a 429 carries no number at all.
 */
function describeLoadFailure(failure: GeneralError): string {
  if (failure.status === 0 || failure.status === 401) return LOAD_FAILED;
  if (failure.status === 429)
    return describeRateLimit(failure.retryAfterSeconds);
  return describeFailure(failure, LOAD_FAILED);
}

async function loadExistingRequest() {
  loading.value = true;
  const { data, error } =
    await accountApi.getUsernameChangeRequest(OWNED_BY_CARD);
  loading.value = false;

  loadError.value = error ? describeLoadFailure(error) : null;
  existingRequest.value = data ?? null;
}

async function submitRequest() {
  if (!canSubmit.value) return;

  submitting.value = true;
  submitError.value = null;
  conflictRefusal.value = null;

  const { data, error } = await accountApi.createUsernameChangeRequest({
    reason: reason.value.trim(),
  });

  submitting.value = false;

  if (error) {
    // A conflict means the section is looking at a stale state - the request was
    // filed elsewhere, or an approval is still live. Re-read it and let the card
    // say so, with the server's sentence under it, in the place the form stood.
    if (error.status === 409) {
      await loadExistingRequest();
      conflictRefusal.value = describeFailure(
        error,
        "Не удалось отправить заявку на смену имени",
      );
      return;
    }
    // Under the reason field, the refusals the interceptor speaks for included:
    // the form and the reason typed into it stay on screen, and the plate the
    // interceptor held for the same refusal is taken down.
    submitError.value = refusalHere(
      error,
      "Не удалось отправить заявку на смену имени",
    );
    return;
  }

  if (data) {
    existingRequest.value = data;
    reason.value = "";
  }
}
</script>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "../AccountPage.styles" as *

.username-change-content
  padding: $medium
  background-color: $bg-element

.current-value
  margin-bottom: $medium
  color: $text-muted

  strong
    color: $text

.loading-state
  padding: $medium

// The state of the request is written in words, as a state is across the
// site, and not drawn as a panel: no fill, no frame, no mark beside it. The
// sentence takes the colour of what it means: green for what is granted, red
// for what was refused or could not be read, grey for what lapsed, the text's
// own for what still waits.
.status-card
  margin-bottom: $medium

.status-header
  margin-bottom: $small

.status-title
  font-weight: 600

  .status-card--rejected &,
  .status-card--failed &
    color: $accent-red

  .status-card--approved &,
  .status-card--completed &
    color: $accent-green

  .status-card--expired &
    color: $text-muted

.status-details
  display: flex
  flex-direction: column
  gap: $tiny
  margin-bottom: $small

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

// Form
.request-form
  display: flex
  flex-direction: column
  gap: $medium

.form-description
  margin: 0
  color: $text-muted
  font-size: $secondary-font-size

.char-count
  font-size: $secondary-font-size
  color: $text-muted
  text-align: right

.form-note
  font-size: $secondary-font-size
  color: $text-muted

// At the width of its caption, as every button of a page is: the column would
// otherwise stretch it across the card.
.request-submit
  align-self: flex-start
</style>

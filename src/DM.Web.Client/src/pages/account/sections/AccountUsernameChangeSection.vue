<template>
  <section id="username-change" ref="sectionEl" class="section">
    <h2 class="section-title">Смена имени пользователя</h2>

    <div class="username-change-content">
      <div class="current-value">
        Текущее имя: <strong>{{ user.username }}</strong>
      </div>

      <!-- The form waits in its shape, without a word. -->
      <AccountSectionSkeleton v-if="loading" shape="fields" :count="1" />

      <!-- The state could not be read: no form over something the server may refuse. -->
      <ErrorState
        v-else-if="loadError"
        :message="loadError"
        :retry="loadExistingRequest"
      />

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
            :maxlength="USERNAME_CHANGE_REASON_MAX_LENGTH"
            placeholder="Объясните, почему хотите сменить имя..."
          ></textarea>
          <div class="char-count">
            {{ reason.length }}/{{ USERNAME_CHANGE_REASON_MAX_LENGTH }}
          </div>
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
import { ref, computed, onBeforeUnmount, onMounted } from "vue";
import { formatDate } from "@/shared/lib/utils/datetime";
import { accountApi } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { FormField } from "@/shared/ui/Form";
import { describeFailure, refusalHere } from "@/shared/lib/errors";
import { ActionResult } from "@/shared/ui/ActionResult";
import type { User } from "@/shared/api/models/community";
import type { UsernameChangeRequest } from "@/shared/api/models/account";
import { ErrorState } from "@/shared/ui/ErrorState";
import { describeLoadFailure, OWNED_BY_CARD } from "./loadFailure";
import AccountSectionSkeleton from "./AccountSectionSkeleton.vue";
import { USERNAME_CHANGE_REASON_MAX_LENGTH } from "@/shared/lib/constants/user";
import { holdAnchor } from "@/shared/lib/scroll";

defineProps<{
  user: User;
}>();

const loading = ref(true);
const existingRequest = ref<UsernameChangeRequest | null>(null);

const reason = ref("");
const submitting = ref(false);
const submitError = ref<string | null>(null);
/** A conflict's refusal, under the card that replaces the form once the state is read again. */
const conflictRefusal = ref<string | null>(null);
const loadError = ref<string | null>(null);

const LOAD_FAILED = "Не удалось узнать состояние заявки";

// A request awaiting a moderator, or an approval whose name is not chosen yet,
// holds a change already asked for; every other state lets the reader ask again,
// as the server does.
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

const canSubmit = computed(() => !!reason.value.trim() && !submitting.value);

const sectionEl = ref<HTMLElement | null>(null);
let releaseAnchor = () => {};

// The profile links here ("Сменить имя пользователя"): the blocks above load
// after the router has scrolled, so the section holds its place until they do.
// The address is read as the router has already written it for this page.
onMounted(async () => {
  const section = sectionEl.value;
  if (section?.parentElement && window.location.hash === `#${section.id}`)
    releaseAnchor = holdAnchor(section, section.parentElement);
  await loadExistingRequest();
});
onBeforeUnmount(() => releaseAnchor());

async function loadExistingRequest() {
  loading.value = true;
  const { data, error } =
    await accountApi.getUsernameChangeRequest(OWNED_BY_CARD);
  loading.value = false;

  loadError.value = error ? describeLoadFailure(error, LOAD_FAILED) : null;
  existingRequest.value = data ?? null;
}

async function submitRequest() {
  if (!canSubmit.value) return;

  submitting.value = true;
  submitError.value = null;
  conflictRefusal.value = null;

  const { data, error } = await accountApi.createUsernameChangeRequest({
    reason: reason.value,
  });

  submitting.value = false;

  if (error) {
    // A conflict: the state is stale, so it is read again and the card says so with the server's sentence.
    if (error.status === 409) {
      await loadExistingRequest();
      conflictRefusal.value = describeFailure(
        error,
        "Не удалось отправить заявку на смену имени",
      );
      return;
    }
    // Under the reason field, the refusals the interceptor speaks for included; the reason stays typed.
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
@use "../AccountPage.styles" as *

.username-change-content
  padding: $medium
  background-color: $bg-element

.current-value
  margin-bottom: $medium
  color: $text-muted

  strong
    color: $text

// The state in words, not a panel: green for what is granted, red for a
// refusal, grey for what lapsed, the text's own for what waits.
.status-card
  margin-bottom: $medium

.status-header
  margin-bottom: $small

.status-title
  font-weight: 700

  .status-card--rejected &
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

// At the width of its caption, not stretched across the card.
.request-submit
  align-self: flex-start
</style>

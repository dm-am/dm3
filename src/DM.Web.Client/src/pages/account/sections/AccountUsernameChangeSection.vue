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
          <span class="status-icon">{{ symbols.cross }}</span>
          <span class="status-title">{{ loadError }}</span>
        </div>
        <p class="status-note">Обновите страницу, чтобы попробовать снова.</p>
      </div>

      <!-- Pending request status card -->
      <div
        v-else-if="existingRequest && existingRequest.status === 'Pending'"
        class="status-card status-card--pending"
      >
        <div class="status-header">
          <SvgIcon name="clock" class="status-icon" />
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
          <span class="status-icon">{{ symbols.cross }}</span>
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
        <p class="status-note">Вы можете подать новую заявку.</p>
      </div>

      <!-- Approved notification -->
      <div
        v-else-if="existingRequest && existingRequest.status === 'Approved'"
        class="status-card status-card--approved"
      >
        <div class="status-header">
          <span class="status-icon">{{ symbols.checkmark }}</span>
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
          <SvgIcon name="clock" class="status-icon" />
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
          <span class="status-icon">{{ symbols.checkmark }}</span>
          <span class="status-title">Имя изменено</span>
        </div>
        <p class="status-note">Заявка выполнена. Вы можете подать новую.</p>
      </div>

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

        <Button type="submit" :loading="submitting" :disabled="!canSubmit">
          Отправить заявку
        </Button>
      </form>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { symbols } from "@/shared/lib/utils/icons";
import { SvgIcon } from "@/shared/ui/Icon";
import { formatDate } from "@/shared/lib/utils/datetime";
import { accountApi } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { FormField } from "@/shared/ui/Form";
import { useToast } from "@/shared/lib/composables/useToast";
import { describeFailure } from "@/shared/lib/errors";
import type { User } from "@/shared/api/models/community";
import type { UsernameChangeRequest } from "@/shared/api/models/account";

defineProps<{
  user: User;
}>();

const toast = useToast();

const loading = ref(true);
const existingRequest = ref<UsernameChangeRequest | null>(null);

const reason = ref("");
const submitting = ref(false);
const submitError = ref<string | null>(null);
const loadError = ref<string | null>(null);

const LOAD_FAILED = "Не удалось узнать состояние заявки";

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

async function loadExistingRequest() {
  loading.value = true;
  const { data, error } = await accountApi.getUsernameChangeRequest();
  loading.value = false;

  // A failed read used to be indistinguishable from "never asked": the section
  // offered the form to someone whose request was already in flight, and the
  // server refused it with a conflict that landed as a validation note under
  // the reason field, where there was nothing to correct.
  loadError.value = error ? describeFailure(error, LOAD_FAILED) : null;
  existingRequest.value = data ?? null;
}

async function submitRequest() {
  if (!canSubmit.value) return;

  submitting.value = true;
  submitError.value = null;

  const { data, error } = await accountApi.createUsernameChangeRequest({
    reason: reason.value.trim(),
  });

  submitting.value = false;

  if (error) {
    // A conflict means the section is looking at a stale state - the request was
    // filed elsewhere, or an approval is still live. Re-read it and let the card
    // say so, instead of explaining it under the reason field.
    if (error.status === 409) {
      await loadExistingRequest();
      toast.error(describeFailure(error, "Не удалось отправить заявку"));
      return;
    }
    submitError.value = describeFailure(error, "Не удалось отправить заявку");
    return;
  }

  if (data) {
    existingRequest.value = data;
    reason.value = "";
    toast.success("Заявка отправлена на рассмотрение");
  }
}
</script>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"
@import "../AccountPage.styles"

.username-change-content
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.current-value
  margin-bottom: $medium
  color: $text-muted

  strong
    color: $text

.loading-state
  padding: $medium

// Status cards
.status-card
  padding: $medium
  border-radius: $border-radius
  margin-bottom: $medium

  &--pending
    +tint($link, 15%)
    border: 1px solid $link

  &--rejected
    +tint($accent-red, 15%)
    border: 1px solid $accent-red

  &--approved
    +tint($accent-green, 15%)
    border: 1px solid $accent-green

  &--expired
    +tint($text-muted, 15%)
    border: 1px solid $text-muted

  &--completed
    +tint($accent-green, 15%)
    border: 1px solid $accent-green

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

  .status-card--pending &
    color: $link

  .status-card--rejected &
    color: $accent-red

  .status-card--approved &
    color: $accent-green

  .status-card--expired &
    color: $text-muted

  .status-card--completed &
    color: $accent-green

  .status-card--failed &
    color: $accent-red

.status-title
  font-weight: 600

  .status-card--pending &
    color: $link

  .status-card--rejected &
    color: $accent-red

  .status-card--approved &
    color: $accent-green

  .status-card--expired &
    color: $text-muted

  .status-card--completed &
    color: $accent-green

  .status-card--failed &
    color: $accent-red

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
</style>

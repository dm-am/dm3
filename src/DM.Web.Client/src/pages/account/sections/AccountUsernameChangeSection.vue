<template>
  <section class="section">
    <h2 class="section-title">Смена имени пользователя</h2>

    <div class="username-change-content">
      <div class="current-value">
        Текущее имя: <strong>{{ user.username }}</strong>
      </div>

      <!-- Loading state -->
      <div v-if="loading" class="loading-state">Загрузка...</div>

      <!-- Pending request status card -->
      <div
        v-else-if="existingRequest && existingRequest.status === 'Pending'"
        class="status-card status-card--pending"
      >
        <div class="status-header">
          <span class="status-icon">{{ symbols.clock }}</span>
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

        <div class="form-group">
          <label class="form-label">Причина смены</label>
          <textarea
            v-model="reason"
            :disabled="submitting"
            class="form-textarea"
            rows="3"
            maxlength="500"
            placeholder="Объясните, почему хотите сменить имя..."
          ></textarea>
          <div class="char-count">{{ reason.length }}/500</div>
        </div>

        <div v-if="submitError" class="error-message" role="alert">
          {{ submitError }}
        </div>

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
import dayjs from "dayjs";
import { AccountApi } from "@/shared/api";
import Button from "@/shared/ui/Button/Button.vue";
import { useToast } from "@/shared/lib/composables/useToast";
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

// Can create new request: no pending request, and either no request or rejected
const canCreateRequest = computed(() => {
  if (loading.value) return false;
  if (!existingRequest.value) return true;
  return existingRequest.value.status === "Rejected";
});

const canSubmit = computed(() => {
  return reason.value.trim().length >= 10 && !submitting.value;
});

onMounted(async () => {
  await loadExistingRequest();
});

async function loadExistingRequest() {
  loading.value = true;
  const { data } = await AccountApi.getUsernameChangeRequest();
  loading.value = false;

  if (data) {
    existingRequest.value = data;
  }
}

function formatDate(dateStr: string): string {
  return dayjs(dateStr).format("DD.MM.YYYY");
}

async function submitRequest() {
  if (!canSubmit.value) return;

  submitting.value = true;
  submitError.value = null;

  const { data, error } = await AccountApi.createUsernameChangeRequest({
    reason: reason.value.trim(),
  });

  submitting.value = false;

  if (error) {
    submitError.value = error.message || "Не удалось отправить заявку";
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
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
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
  color: $text-muted
  text-align: center
  padding: $medium

// Status cards
.status-card
  padding: $medium
  border-radius: $border-radius
  margin-bottom: $medium

  &--pending
    background-color: rgba($link, 0.1)
    border: 1px solid $link

  &--rejected
    background-color: rgba($accent-red, 0.1)
    border: 1px solid $accent-red

  &--approved
    background-color: rgba($accent-green, 0.1)
    border: 1px solid $accent-green

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

.status-title
  font-weight: 600

  .status-card--pending &
    color: $link

  .status-card--rejected &
    color: $accent-red

  .status-card--approved &
    color: $accent-green

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

.form-group
  display: flex
  flex-direction: column
  gap: $tiny

.form-label
  font-weight: 500
  color: $text

.form-textarea
  width: 100%
  padding: $small
  border: 1px solid $border
  border-radius: $border-radius
  background-color: $bg
  color: $text
  font-family: inherit
  font-size: inherit
  resize: vertical
  min-height: 80px

  &:focus
    outline: none
    border-color: $link

  &:disabled
    opacity: 0.6
    cursor: not-allowed

.char-count
  font-size: $secondary-font-size
  color: $text-muted
  text-align: right

.form-note
  font-size: $secondary-font-size
  color: $text-muted

.error-message
  padding: $small
  background-color: rgba($accent-red, 0.1)
  border: 1px solid $accent-red
  border-radius: $border-radius
  color: $accent-red
  font-size: $secondary-font-size
</style>

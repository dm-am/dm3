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
import { formatDate } from "@/shared/lib/utils/datetime";
import { accountApi } from "@/shared/api";
import Button from "@/shared/ui/Button/Button.vue";
import { FormField } from "@/shared/ui/Form";
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
  const { data } = await accountApi.getUsernameChangeRequest();
  loading.value = false;

  if (data) {
    existingRequest.value = data;
  }
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
  color: $text-muted
  text-align: center
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

.char-count
  font-size: $secondary-font-size
  color: $text-muted
  text-align: right

.form-note
  font-size: $secondary-font-size
  color: $text-muted
</style>

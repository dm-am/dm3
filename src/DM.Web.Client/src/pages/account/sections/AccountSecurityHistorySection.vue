<template>
  <section class="section">
    <h2 class="section-title">Журнал безопасности</h2>

    <div class="security-history-content">
      <div v-if="loading" class="loading-state">Загрузка...</div>

      <EmptyState
        v-else-if="events.length === 0"
        title="Нет событий безопасности"
      />

      <div v-else class="events-list">
        <div
          v-for="event in events"
          :key="event.id"
          class="event-item"
          :class="eventClass(event.eventType)"
        >
          <div class="event-info">
            <div class="event-title">{{ eventTitle(event.eventType) }}</div>
            <div class="event-details">
              <span v-if="event.deviceInfo" class="event-device">
                {{ event.deviceInfo }}
              </span>
              <span v-if="event.ipAddress" class="event-ip">
                {{ event.ipAddress }}
              </span>
              <span class="event-time">{{
                formatDateFull(event.timestampUtc)
              }}</span>
            </div>
            <div v-if="event.details" class="event-extra">
              {{ event.details }}
            </div>
          </div>
        </div>
      </div>

      <div v-if="!loading && events.length > 0" class="events-footer">
        Показано {{ events.length }} {{ eventsNoun }}
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, onMounted } from "vue";
import { accountApi } from "@/entities/user";
import { EmptyState } from "@/shared/ui/EmptyState";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { pluralize } from "@/shared/lib/utils/pluralize";
import type {
  SecurityEvent,
  SecurityEventType,
} from "@/shared/api/models/account";

const events = ref<SecurityEvent[]>([]);
const loading = ref(true);

// The footer counted with one noun form, so it read "последние 3 событий" on a
// screen a person opens when something already worried them.
const eventsNoun = computed(() =>
  pluralize(
    events.value.length,
    "последнее событие",
    "последних события",
    "последних событий",
  ),
);

onMounted(async () => {
  await loadEvents();
});

async function loadEvents() {
  loading.value = true;
  const { data, error } = await accountApi.getSecurityHistory(30);
  loading.value = false;

  if (!error && data) {
    events.value = data.resources;
  }
}

function eventTitle(type: SecurityEventType): string {
  switch (type) {
    case "LoginSuccess":
      return "Успешный вход";
    case "LoginFailure":
      return "Неудачная попытка входа";
    case "Logout":
      return "Выход из системы";
    case "PasswordChange":
      return "Изменение пароля";
    case "EmailChange":
      return "Изменение почты";
    case "SessionTerminated":
      return "Сессия завершена";
    case "LogoutElsewhere":
      return "Выход на другом устройстве";
    case "PasswordResetRequest":
      return "Запрос сброса пароля";
    case "PasswordResetComplete":
      return "Пароль сброшен";
    case "AccountLocked":
      return "Аккаунт заблокирован";
    case "SuspiciousLogin":
      return "Подозрительный вход";
    default:
      return "Событие безопасности";
  }
}

function eventClass(type: SecurityEventType): string {
  switch (type) {
    case "LoginSuccess":
    case "PasswordChange":
    case "PasswordResetComplete":
      return "event-item--success";
    case "LoginFailure":
    case "AccountLocked":
    case "SuspiciousLogin":
      return "event-item--warning";
    case "SessionTerminated":
    case "Logout":
    case "LogoutElsewhere":
      return "event-item--neutral";
    default:
      return "";
  }
}
</script>

<style scoped lang="sass">
@import "../AccountPage.styles"

.security-history-content
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.loading-state
  color: $text-muted
  padding: $medium

.events-list
  display: flex
  flex-direction: column
  gap: $small

.event-item
  display: flex
  gap: $small
  padding: $small $medium
  background-color: $bg
  border-radius: $border-radius
  border-left: 3px solid $border

  &--success
    border-left-color: $accent-green

  &--warning
    border-left-color: $accent-yellow

  &--neutral
    border-left-color: $text-muted

.event-icon
  flex-shrink: 0
  width: 24px
  height: 24px
  display: flex
  align-items: center
  justify-content: center
  font-size: 1rem

  .event-item--success &
    color: $accent-green

  .event-item--warning &
    color: $accent-yellow

  .event-item--neutral &
    color: $text-muted

.event-info
  flex: 1
  min-width: 0
  display: flex
  flex-direction: column
  gap: $tiny

.event-title
  font-weight: 500
  color: $text

.event-details
  display: flex
  flex-wrap: wrap
  gap: $small
  font-size: $secondary-font-size
  color: $text-muted

.event-device
  &::after
    content: "\2022"
    margin-left: $small

.event-ip
  &::after
    content: "\2022"
    margin-left: $small

.event-extra
  font-size: $secondary-font-size
  color: $text-muted
  font-style: italic

.events-footer
  margin-top: $medium
  text-align: center
  font-size: $secondary-font-size
  color: $text-muted
</style>

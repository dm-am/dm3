<template>
  <section class="section">
    <h2 class="section-title">Журнал безопасности</h2>

    <div class="security-history-content">
      <!-- The entries wait in their own shape; "Нет событий безопасности" is said only by an answer. -->
      <AccountSectionSkeleton
        v-if="loading && !loaded"
        shape="rows"
        :count="3"
      />

      <ErrorState
        v-else-if="loadError && !loaded"
        :message="loadError"
        :retry="loadEvents"
      />

      <EmptyState
        v-else-if="events.length === 0"
        title="Нет событий безопасности"
      />

      <div v-else class="events-list">
        <div v-for="event in events" :key="event.id" class="event-item">
          <div class="event-info">
            <!-- What happened and when, one sentence; the date after the comma never breaks. -->
            <div class="event-heading">
              <span class="event-title">{{ eventTitle(event.eventType) }}</span
              ><span class="event-time"
                >, <span class="event-date">{{ dateOf(event) }}</span></span
              >
            </div>
            <!-- From where, a caption line under it. -->
            <CaptionLine
              v-if="event.deviceInfo || event.ipAddress"
              size="secondary"
              class="event-details"
            >
              <span v-if="event.deviceInfo" class="event-device">{{
                event.deviceInfo
              }}</span>
              <span v-if="event.ipAddress" class="event-ip">{{
                event.ipAddress
              }}</span>
            </CaptionLine>
            <div v-if="event.details" class="event-extra">
              {{ event.details }}
            </div>
          </div>
        </div>
      </div>

      <div v-if="loaded && events.length > 0" class="events-footer">
        Показано {{ events.length }} {{ eventsNoun }}
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, ref, onMounted } from "vue";
import { accountApi } from "@/entities/user";
import { EmptyState } from "@/shared/ui/EmptyState";
import { ErrorState } from "@/shared/ui/ErrorState";
import { CaptionLine } from "@/shared/ui/CaptionLine";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { pluralize } from "@/shared/lib/utils/pluralize";
import type {
  SecurityEvent,
  SecurityEventType,
} from "@/shared/api/models/account";
import AccountSectionSkeleton from "./AccountSectionSkeleton.vue";
import { OWNED_BY_CARD } from "./loadFailure";

const LOAD_FAILED = "Не удалось загрузить журнал безопасности";

const events = ref<SecurityEvent[]>([]);
const loading = ref(true);
/** An answer has come: from then on a re-read keeps the entries on screen. */
const loaded = ref(false);
const loadError = ref<string | null>(null);

// The noun agrees with the count.
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
  const { data, error } = await accountApi.getSecurityHistory(
    30,
    undefined,
    OWNED_BY_CARD,
  );
  loading.value = false;

  if (!error && data) {
    events.value = data.resources;
    loaded.value = true;
    loadError.value = null;
  } else {
    loadError.value = LOAD_FAILED;
  }
}

/** When the entry was written, as the journal heading says it. */
const dateOf = (event: SecurityEvent) => formatDateFull(event.timestampUtc);

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
    // Written when the new address is asked for, not when it moves: the
    // confirmation writes no entry.
    case "EmailChange":
      return "Запрос смены почты";
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
    case "TwoFactorEnabled":
      return "Двухфакторная аутентификация включена";
    case "TwoFactorDisabled":
      return "Двухфакторная аутентификация отключена";
    case "TwoFactorRecoveryCodeUsed":
      return "Использован резервный код";
    case "TwoFactorRecoveryCodesReissued":
      return "Резервные коды перевыпущены";
    case "TwoFactorRemovalScheduled":
      return "Назначено отключение двухфакторной аутентификации";
    case "TwoFactorRemovalCancelled":
      return "Отключение двухфакторной аутентификации отменено";
    case "TwoFactorRemovedByAdmin":
      return "Двухфакторная аутентификация отключена администратором";
    case "TwoFactorSetupFailure":
      return "Неверный код при настройке двухфакторной аутентификации";
    default:
      return "Событие безопасности";
  }
}

// Read once on mount; the page reads the journal again through this when the security block adds an entry.
defineExpose({ reload: loadEvents });
</script>

<style scoped lang="sass">
@use "../AccountPage.styles" as *

.security-history-content
  padding: $medium
  background-color: $bg-element

// A tile of its own, its text 16 in, 8 to the next, as a row of "Сессии".
.events-list
  display: flex
  flex-direction: column
  gap: $small

.event-item
  display: flex
  gap: $small
  padding: $small $medium
  background-color: $bg-element

.event-info
  flex: 1
  min-width: 0
  display: flex
  flex-direction: column
  gap: $tiny

.event-heading
  color: $text

.event-title
  font-weight: 400

// The date in the grey and size of the caption under it.
.event-time
  font-size: $secondary-font-size
  color: $text-muted

.event-date
  white-space: nowrap

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

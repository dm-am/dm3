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
            <!-- What happened and when, as one sentence: "Успешный вход,
                 05.10.2026 в 21:42". A long name breaks between its words,
                 as a heading does, and the date after the comma is never
                 broken. -->
            <div class="event-heading">
              <span class="event-title">{{ eventTitle(event.eventType) }}</span
              ><span class="event-time"
                >, <span class="event-date">{{ dateOf(event) }}</span></span
              >
            </div>
            <!-- From where, under it: a caption line, the bar of a caption
                 between the parts, wrapping between them where it is short.
            -->
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
import { CaptionLine } from "@/shared/ui/CaptionLine";
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
    // Written when the new address is asked for, not when it moves: the server
    // logs it in EmailChangeService.Change, beside the warning letter to the
    // old address, and the confirmation step that actually moves the address
    // writes no journal entry at all. "Изменение почты" claimed a move that
    // had not happened yet, on the one screen a person opens to learn what
    // somebody else may have done to the account.
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

function eventClass(type: SecurityEventType): string {
  switch (type) {
    // The two-factor entries are grouped by what they cost the reader, not by
    // whether they succeeded: switching the factor on and reissuing the codes
    // leave the account better locked than before.
    case "LoginSuccess":
    case "PasswordChange":
    case "PasswordResetComplete":
    case "TwoFactorEnabled":
    case "TwoFactorRecoveryCodesReissued":
      return "event-item--success";
    // A spent recovery code and a mistyped setup code are not failures. They
    // are the entries a person scanning this journal for somebody else's hand
    // in their account is looking for.
    case "LoginFailure":
    case "AccountLocked":
    case "SuspiciousLogin":
    case "TwoFactorRecoveryCodeUsed":
    case "TwoFactorSetupFailure":
    case "TwoFactorRemovalScheduled":
    case "TwoFactorRemovedByAdmin":
      return "event-item--warning";
    // Switching the factor off is the owner's own doing, and it leaves nothing
    // to look into.
    case "SessionTerminated":
    case "Logout":
    case "LogoutElsewhere":
    case "TwoFactorDisabled":
    case "TwoFactorRemovalCancelled":
      return "event-item--neutral";
    default:
      return "";
  }
}

// Read once on mount, while the security block above writes entries into this
// journal through the server: a password change and a request for a new address
// each add one. The page re-reads the block through this door.
defineExpose({ reload: loadEvents });
</script>

<style scoped lang="sass">
@use "../AccountPage.styles" as *

.security-history-content
  padding: $medium
  background-color: $bg-element

.loading-state
  padding: $medium

// A tile of its own, its text 16 in, as the text of a row of "Сессии"
// above stands inside its frame, 8 to the next.
.events-list
  display: flex
  flex-direction: column
  gap: $small

.event-item
  display: flex
  gap: $small
  padding: $small $medium
  background-color: $bg

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

.event-heading
  color: $text

.event-title
  font-weight: 500

// The date in the grey of the caption under it, at its size.
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

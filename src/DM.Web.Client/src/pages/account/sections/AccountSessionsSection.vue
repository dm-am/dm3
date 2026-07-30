<template>
  <section class="section">
    <h2 class="section-title">Сессии</h2>

    <div class="sessions-content">
      <!-- Sessions list -->
      <div v-if="loading" class="loading-state">Загрузка...</div>
      <EmptyState
        v-else-if="sessions.length === 0"
        title="Нет активных сессий"
      />
      <div v-else class="sessions-list">
        <div
          v-for="session in sessions"
          :key="session.id"
          class="session-item"
          :class="{ 'session-item--current': session.isCurrent }"
        >
          <div class="session-info">
            <div class="session-device">
              {{ session.deviceInfo || "Неизвестное устройство" }}
              <span v-if="session.isCurrent" class="current-badge"
                >текущая</span
              >
            </div>
            <div class="session-details">
              <span v-if="session.ipAddress" class="session-ip">{{
                session.ipAddress
              }}</span>
              <span
                v-if="session.ipAddress"
                class="meta-sep"
                aria-hidden="true"
                >{{ " | " }}</span
              >
              <span class="session-date">{{
                formatDateFull(session.createdUtc)
              }}</span>
            </div>
          </div>
          <button
            v-if="!session.isCurrent"
            class="terminate-btn"
            :disabled="terminatingId === session.id"
            @click="terminateSession(session.id)"
          >
            {{ terminatingId === session.id ? "..." : "Завершить" }}
          </button>
        </div>
      </div>

      <!-- Logout all button -->
      <div class="sessions-actions">
        <span v-if="logoutAllAction.error.value" class="error-text">
          {{ logoutAllAction.error.value }}
        </span>
        <Button
          v-if="hasOtherSessions"
          :loading="logoutAllAction.loading.value"
          @click="logoutFromAll"
        >
          Выйти со всех других устройств
        </Button>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { accountApi } from "@/entities/user";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import Button from "@/shared/ui/Button/Button.vue";
import { EmptyState } from "@/shared/ui";
import { useAsyncAction } from "@/shared/lib/composables/useAsyncAction";
import { useToast } from "@/shared/lib/composables/useToast";
import type { SessionInfo } from "@/shared/api/models/account";
import { describeFailure } from "@/shared/lib/errors";

const toast = useToast();

const sessions = ref<SessionInfo[]>([]);
const loading = ref(true);
const terminatingId = ref<string | null>(null);

const hasOtherSessions = computed(() =>
  sessions.value.some((s) => !s.isCurrent),
);

onMounted(async () => {
  await loadSessions();
});

async function loadSessions() {
  loading.value = true;
  const { data, error } = await accountApi.getSessions();
  loading.value = false;

  if (!error && data) {
    sessions.value = data.resources;
  }
}

async function terminateSession(sessionId: string) {
  terminatingId.value = sessionId;
  const { error } = await accountApi.terminateSession(sessionId);
  terminatingId.value = null;

  if (error) {
    toast.error("Не удалось завершить сессию");
  } else {
    sessions.value = sessions.value.filter((s) => s.id !== sessionId);
    toast.success("Сессия завершена");
  }
}

// Logout from all devices
const logoutAllAction = useAsyncAction();

const logoutFromAll = () => {
  logoutAllAction.execute(async () => {
    const { error } = await accountApi.logoutAll();
    if (error)
      throw new Error(describeFailure(error, "Не удалось завершить сессии"));
    // Keep only current session
    sessions.value = sessions.value.filter((s) => s.isCurrent);
    toast.success("Вы вышли со всех других устройств");
  });
};
</script>

<style scoped lang="sass">
@import "../AccountPage.styles"

.sessions-content
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.loading-state
  color: $text-muted
  text-align: center
  padding: $medium

.sessions-list
  display: flex
  flex-direction: column
  gap: $small

.session-item
  display: flex
  justify-content: space-between
  align-items: center
  padding: $small $medium
  background-color: $bg-element
  border-radius: $border-radius
  border: 1px solid $border

  &--current
    border-color: $link
    +tint($link, 15%)

.session-info
  display: flex
  flex-direction: column
  gap: $tiny

.session-device
  font-weight: 500
  color: $text
  display: flex
  align-items: center
  gap: $small

.current-badge
  font-size: $secondary-font-size
  font-weight: normal
  color: $link
  +tint($link, 15%)
  padding: 2px $tiny
  border-radius: 3px

.session-details
  display: flex
  font-size: $secondary-font-size
  color: $text-muted

.meta-sep
  color: $text-muted

.terminate-btn
  background: none
  border: 1px solid $border-accent-red
  color: $accent-red
  padding: $tiny $small
  border-radius: $border-radius
  cursor: pointer
  font-size: $secondary-font-size

  &:hover:not(:disabled)
    +tint($accent-red, 15%)

  &:disabled
    opacity: 0.5
    cursor: default

.sessions-actions
  margin-top: $medium
  display: flex
  flex-direction: column
  gap: $small
</style>

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
      <!-- This device's session stands apart under a caption of its own and
           looks like every other row: what sets it apart is where it stands
           and that it has no "Завершить", not a pill, a fill or a frame. -->
      <div v-else class="sessions-list">
        <div v-for="group in groups" :key="group.title" class="session-group">
          <p class="session-group-title">{{ group.title }}</p>
          <template v-for="session in group.sessions" :key="session.id">
            <div class="session-item">
              <div class="session-info">
                <!-- Whole, breaking between words if it has to: the device is
                     the row's main word. -->
                <div class="session-device">
                  {{ session.deviceInfo || "Неизвестное устройство" }}
                </div>
                <!-- Every part whole: where the line is short it wraps
                     between the address and the date, and the long address
                     of a mobile network stands on a line of its own. -->
                <CaptionLine size="secondary" class="session-details">
                  <span v-if="session.ipAddress" class="session-ip">{{
                    session.ipAddress
                  }}</span>
                  <span class="session-date">{{
                    formatDateFull(session.createdUtc)
                  }}</span>
                </CaptionLine>
              </div>
              <Button
                v-if="!session.isCurrent"
                type="button"
                class="terminate-btn"
                :loading="terminatingId === session.id"
                @click="terminateSession(session.id)"
              >
                Завершить
              </Button>
            </div>
            <!-- A refused "Завершить", under the row it was pressed in. -->
            <ActionResult
              v-if="terminateRefusal?.id === session.id"
              :text="terminateRefusal.text"
              class="session-refusal"
            />
          </template>
        </div>
      </div>

      <!-- Logout all button -->
      <div class="sessions-actions">
        <Button
          v-if="hasOtherSessions"
          class="logout-all"
          :loading="logoutAllAction.loading.value"
          @click="logoutFromAll"
        >
          Выйти со всех других устройств
        </Button>
        <ActionResult
          :text="logoutAllAction.error.value"
          class="error-text sessions-refusal"
        />
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from "vue";
import { accountApi } from "@/entities/user";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import Button from "@/shared/ui/Button/Button.vue";
import { EmptyState } from "@/shared/ui/EmptyState";
import { useAsyncAction } from "@/shared/lib/composables/useAsyncAction";
import type { SessionInfo } from "@/shared/api/models/account";
import { refusalHere } from "@/shared/lib/errors";
import { ActionResult } from "@/shared/ui/ActionResult";
import { CaptionLine } from "@/shared/ui/CaptionLine";

const sessions = ref<SessionInfo[]>([]);
const loading = ref(true);
const terminatingId = ref<string | null>(null);

const hasOtherSessions = computed(() =>
  sessions.value.some((s) => !s.isCurrent),
);

/** The list in two groups, this device's first; a group with nobody in it is not drawn. */
const groups = computed(() =>
  [
    {
      title: "Текущая сессия",
      sessions: sessions.value.filter((s) => s.isCurrent),
    },
    {
      title: "Другие сессии",
      sessions: sessions.value.filter((s) => !s.isCurrent),
    },
  ].filter((group) => group.sessions.length > 0),
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

/** The last refused "Завершить", under the row of the session it was about. */
const terminateRefusal = ref<{ id: string; text: string } | null>(null);

async function terminateSession(sessionId: string) {
  terminatingId.value = sessionId;
  terminateRefusal.value = null;
  const { error } = await accountApi.terminateSession(sessionId);
  terminatingId.value = null;

  if (error) {
    const text = refusalHere(error, "Не удалось завершить сессию");
    if (text) terminateRefusal.value = { id: sessionId, text };
  } else {
    sessions.value = sessions.value.filter((s) => s.id !== sessionId);
  }
}

// Logout from all devices
const logoutAllAction = useAsyncAction();

const logoutFromAll = () => {
  logoutAllAction.execute(async () => {
    const { error } = await accountApi.logoutAll();
    if (error) {
      // The list is untouched in either branch, so the reader is left looking
      // at the sessions that are still open, and the line beside the button
      // says why. The plate the interceptor held for the same refusal is taken
      // down; an expired session and a dead connection stay with the plate.
      const text = refusalHere(error, "Не удалось завершить сессии");
      if (text) throw new Error(text);
      return;
    }
    // Keep only current session
    sessions.value = sessions.value.filter((s) => s.isCurrent);
  });
};

// Read once on mount, and the list goes stale whenever something outside this
// block ends a session: changing the password ends every other one on the
// server. The page above re-reads the block through this door.
defineExpose({ reload: loadSessions });
</script>

<style scoped lang="sass">
@use "../AccountPage.styles" as *

.sessions-content
  padding: $medium
  background-color: $bg-element

.loading-state
  padding: $medium

.sessions-list
  display: flex
  flex-direction: column
  gap: $medium

.session-group
  display: flex
  flex-direction: column
  gap: $small

.session-group-title
  margin: 0
  font-size: $secondary-font-size
  color: $text-muted

// Spaced by the gap of the group, at the left edge of the row above it.
.session-refusal
  --action-result-margin: 0

// The device and its line on the left, "Завершить" level with them on the
// right, at every width: a phone shows the row as the full version does.
.session-item
  display: grid
  grid-template-columns: minmax(0, 1fr) auto
  column-gap: $medium
  row-gap: $small
  align-items: center
  padding: $small $medium
  background-color: $bg-element
  border: 1px solid $border

.session-info
  display: flex
  flex-direction: column
  gap: $tiny
  min-width: 0

.session-device
  font-weight: 500
  color: $text

// A row action: the shared secondary button, with no colour of its own.
.terminate-btn
  justify-self: start

.sessions-actions
  margin-top: $medium
  display: flex
  flex-direction: column
  gap: $small

// At the width of its caption, as every button of a page is: the column would
// otherwise stretch it across the card.
.logout-all
  align-self: flex-start

// Under the button, spaced by the gap of the column.
.sessions-refusal
  --action-result-margin: 0
</style>

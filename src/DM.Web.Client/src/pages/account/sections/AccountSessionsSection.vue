<template>
  <section class="section">
    <h2 class="section-title">Сессии</h2>

    <div class="sessions-content">
      <!-- The rows wait in their own shape; "Нет активных сессий" is said only by an answer. -->
      <AccountSectionSkeleton
        v-if="loading && !loaded"
        shape="rows"
        :count="2"
      />
      <ErrorState
        v-else-if="loadError && !loaded"
        :message="loadError"
        :retry="loadSessions"
      />
      <EmptyState
        v-else-if="sessions.length === 0"
        title="Нет активных сессий"
      />
      <!-- This device's session stands apart by its place and its missing "Завершить", not by a pill, a fill or a frame. -->
      <div v-else class="sessions-list">
        <div v-for="group in groups" :key="group.title" class="session-group">
          <p class="session-group-title">{{ group.title }}</p>
          <template v-for="session in group.sessions" :key="session.id">
            <div class="session-item">
              <div class="session-info">
                <!-- Whole, breaking between words: the row's main word. -->
                <div class="session-device">
                  {{ session.deviceInfo || "Неизвестное устройство" }}
                </div>
                <!-- Every part whole: the line wraps between the address and the date. -->
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
            <!-- A refused "Завершить", under its row. -->
            <ActionResult
              v-if="terminateRefusal?.id === session.id"
              :text="terminateRefusal.text"
              class="session-refusal"
            />
          </template>
        </div>
      </div>

      <div class="sessions-actions">
        <Button
          v-if="hasOtherSessions"
          class="logout-all"
          :loading="loggingOutAll"
          :disabled="logoutAllResult.waiting.value"
          @click="logoutFromAll"
        >
          Выйти со всех других устройств
        </Button>
        <ActionResult :result="logoutAllResult" class="sessions-refusal" />
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
import { ErrorState } from "@/shared/ui/ErrorState";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import type { SessionInfo } from "@/shared/api/models/account";
import { refusalHere } from "@/shared/lib/errors";
import { ActionResult } from "@/shared/ui/ActionResult";
import { CaptionLine } from "@/shared/ui/CaptionLine";
import AccountSectionSkeleton from "./AccountSectionSkeleton.vue";
import { OWNED_BY_CARD } from "./loadFailure";

const LOAD_FAILED = "Не удалось загрузить сессии";

const sessions = ref<SessionInfo[]>([]);
const loading = ref(true);
/** An answer has come: from then on a re-read keeps the rows on screen. */
const loaded = ref(false);
const loadError = ref<string | null>(null);
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
  const { data, error } = await accountApi.getSessions(OWNED_BY_CARD);
  loading.value = false;

  if (!error && data) {
    sessions.value = data.resources;
    loaded.value = true;
    loadError.value = null;
  } else {
    loadError.value = LOAD_FAILED;
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

const logoutAllResult = useActionResult();
const loggingOutAll = ref(false);

async function logoutFromAll() {
  logoutAllResult.clear();
  loggingOutAll.value = true;
  const { error } = await accountApi.logoutAll();
  loggingOutAll.value = false;
  if (error) {
    // The list is untouched, so the line beside the button says why; an expired
    // session and a dead connection stay the plate's.
    logoutAllResult.refuse(error, "Не удалось завершить сессии");
    return;
  }
  sessions.value = sessions.value.filter((s) => s.isCurrent);
}

// Read once on mount; a new password ends the other sessions, and the page reads the list again through this.
defineExpose({ reload: loadSessions });
</script>

<style scoped lang="sass">
@use "../AccountPage.styles" as *

.sessions-content
  padding: $medium
  background-color: $bg-element

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

// Spaced by the gap of the group.
.session-refusal
  --action-result-margin: 0

// The device and its line on the left, "Завершить" level with them on the right, at every width.
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
  font-weight: 400
  color: $text

// The shared secondary button, with no colour of its own.
.terminate-btn
  justify-self: start

.sessions-actions
  margin-top: $medium
  display: flex
  flex-direction: column
  gap: $small

// At the width of its caption, not stretched across the card.
.logout-all
  align-self: flex-start

// Under the button, spaced by the gap of the column.
.sessions-refusal
  --action-result-margin: 0
</style>

<script setup lang="ts">
import { ref, onMounted, computed, reactive, type Ref } from "vue";
import { useModal } from "vue-final-modal";
import { refusalHere } from "@/shared/lib/errors";
import {
  moderationApi,
  UsernameChangeRequestStatus,
  type UsernameChangeRequest,
  type ResolveUsernameChangeRequest,
} from "@/entities/moderation";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import UsernameChangeRejectDialog from "./dialogs/UsernameChangeRejectDialog.vue";
import { useRoleGate } from "./lib/useRoleGate";
import { sectionRole } from "./lib/sections";

const { hasAccess, deniedText } = useRoleGate(
  sectionRole("moderation-username-changes"),
);
const requests = ref<UsernameChangeRequest[]>([]);
const loading = ref(false);
const processing = ref<string | null>(null);

const LOAD_FAILED = "Не удалось загрузить запросы на смену имени";

/**
 * Why the list did not arrive, printed where the list would stand.
 *
 * A failed load is the state of the page and not a plate over it: there is no
 * list underneath to go back to, and a plate in the corner over an empty
 * section explains nothing (UI_STANDARDS, "Всплывающие сообщения"). The api
 * method has no seam for `ownsRefusal`, so the refusal is taken over from the
 * interceptor (refusalHere): it stands here in the server's words and the
 * plate held for it is taken down. An expired session and a dead connection
 * stay with the plate. Cleared by the next read.
 */
const loadRefusal = ref("");

/**
 * Whether the last read was refused. "Нет активных запросов" is a claim about
 * the queue, and a refused read makes none: without this a dead server read
 * as an empty queue.
 */
const loadFailed = ref(false);

const pendingRequests = computed(() =>
  requests.value.filter(
    (r) => r.status === UsernameChangeRequestStatus.Pending,
  ),
);

const fetchRequests = async () => {
  loading.value = true;
  loadRefusal.value = "";
  loadFailed.value = false;
  try {
    const { data, error } =
      await moderationApi.getPendingUsernameChangeRequests();
    if (error) {
      // The rows of an earlier read are not this answer, and a re-read after
      // a resolved request would otherwise keep showing the row it resolved.
      requests.value = [];
      loadFailed.value = true;
      loadRefusal.value = refusalHere(error, LOAD_FAILED) ?? "";
      return;
    }
    requests.value = data?.resources || [];
  } finally {
    loading.value = false;
  }
};

// --- Approve (ConfirmDialog) ---
const approveTarget = ref<UsernameChangeRequest | null>(null);

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone, so the interceptor keeps quiet about it.
// The row stays shut until the queue is read again without it.
const confirmApprove = async () => {
  const target = approveTarget.value;
  if (!target) return;
  processing.value = target.id;
  const resolve: ResolveUsernameChangeRequest = {
    status: UsernameChangeRequestStatus.Approved,
  };
  const result = await moderationApi.resolveUsernameChangeRequest(
    target.id,
    resolve,
    { ownsRefusal: true, ownsRateLimit: true },
  );
  if (result.error) {
    processing.value = null;
    return result;
  }
  void fetchRequests().finally(() => (processing.value = null));
  return result;
};

// --- Reject dialog (shared Dialog idiom) ---
// Null until the first openRejectModal sets it; the dialog only mounts
// after that, so the non-null cast below is safe.
const rejectingRequest = ref<UsernameChangeRequest | null>(null);

const { open: openRejectDialog, close: closeRejectDialog } = useModal({
  component: UsernameChangeRejectDialog,
  attrs: reactive({
    request: rejectingRequest as Ref<UsernameChangeRequest>,
    onSuccess: async () => {
      closeRejectDialog();
      await fetchRequests();
    },
    onCancel: () => closeRejectDialog(),
  }),
});

const openRejectModal = (request: UsernameChangeRequest) => {
  rejectingRequest.value = request;
  openRejectDialog();
};

onMounted(() => fetchRequests());
</script>

<template>
  <page-title>Запросы на смену имени</page-title>

  <SecondaryText v-if="!hasAccess">{{ deniedText }}</SecondaryText>

  <div v-else class="username-changes">
    <secondary-text v-if="loading">Загрузка...</secondary-text>

    <!-- Why the list did not arrive, in its place. A plate in the corner
         used to say this over an empty section. -->
    <p v-else-if="loadRefusal" class="form-error" role="alert">
      {{ loadRefusal }}
    </p>

    <template v-else-if="pendingRequests.length === 0 && !loadFailed">
      <secondary-text>Нет активных запросов</secondary-text>
    </template>

    <div v-else-if="pendingRequests.length > 0" class="requests-list">
      <div
        v-for="request in pendingRequests"
        :key="request.id"
        class="request-card"
      >
        <!-- No word of the state: the queue holds the waiting requests
             alone, and a word the same on every row says nothing. -->
        <div class="request-header">
          <router-link
            :to="{
              name: 'profile',
              params: { username: request.currentUsername },
            }"
            >{{ request.currentUsername }}</router-link
          >
          <div class="request-date">
            <human-date :date="request.createdUtc" />
          </div>
        </div>

        <div class="request-reason">
          <strong>Причина:</strong>
          <p>{{ request.reason }}</p>
        </div>

        <div v-if="request.requestedUsername" class="requested-username">
          <strong>Запрошенное имя:</strong> {{ request.requestedUsername }}
        </div>

        <div class="request-actions">
          <button
            class="approve-btn"
            :disabled="processing === request.id"
            @click="approveTarget = request"
          >
            {{ processing === request.id ? "..." : "Одобрить" }}
          </button>
          <button
            class="reject-btn"
            :disabled="processing === request.id"
            @click="openRejectModal(request)"
          >
            Отклонить
          </button>
        </div>
      </div>
    </div>

    <!-- Approve confirmation -->
    <ConfirmDialog
      :show="approveTarget !== null"
      title="Одобрить смену имени?"
      :message="`&quot;${approveTarget?.currentUsername ?? ''}&quot; сможет занять новое имя`"
      confirm-label="Одобрить"
      failure="Не удалось одобрить запрос"
      :action="confirmApprove"
      @update:show="(v) => !v && (approveTarget = null)"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.requests-list
  display: flex
  flex-direction: column
  gap: $medium

// The refusal line of a form, in place of the list: the same red and the same
// secondary size as .form-error in the character form and the grant form.
.form-error
  margin: $small 0 0
  color: $accent-red
  font-size: $secondary-font-size

.request-card
  +card()

// The name on the left, the date level with it on the right while the list is
// wide.
.request-header
  display: flex
  justify-content: space-between
  align-items: center
  gap: $medium
  margin-bottom: $small

// A date is not broken: "04.10.2026" and "в 18:20" on two lines read as two
// facts.
.request-date
  font-size: 0.85rem
  color: $text-muted
  white-space: nowrap

.request-reason
  margin-bottom: $small

  strong
    color: $text

  p
    margin: $minor 0 0 0
    color: $text-muted

.requested-username
  margin-bottom: $small
  color: $text

  strong
    color: $text

.request-actions
  display: flex
  gap: $small
  margin-top: $medium

.approve-btn
  +button

.reject-btn
  +button
</style>

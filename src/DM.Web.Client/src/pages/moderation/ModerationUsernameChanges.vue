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
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { ErrorState } from "@/shared/ui/ErrorState";
import UsernameChangeRejectDialog from "./dialogs/UsernameChangeRejectDialog.vue";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const requests = ref<UsernameChangeRequest[]>([]);
const loading = ref(false);
const processing = ref<string | null>(null);

const LOAD_FAILED = "Не удалось загрузить запросы на смену имени";

/**
 * Why the list did not arrive, printed where the list would stand. The api
 * method has no seam for `ownsRefusal`, so the refusal is taken over
 * (refusalHere). Cleared by the next read.
 */
const loadRefusal = ref("");

/** A refused read makes no claim that the queue is empty. */
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
      // The rows of an earlier read are not this answer: one may be a resolved request.
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

const approveTarget = ref<UsernameChangeRequest | null>(null);

// The window waits for it, prints a refusal in itself and closes on success
// alone. The row stays shut until the queue is read again without it.
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
    OWNS_REFUSAL,
  );
  if (result.error) {
    processing.value = null;
    return result;
  }
  void fetchRequests().finally(() => (processing.value = null));
  return result;
};

// Set before the dialog first mounts, so the non-null cast below is safe.
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

  <div class="username-changes">
    <!-- Text, not a skeleton: a reason runs to any length. A read again keeps the cards. -->
    <secondary-text v-if="loading && !requests.length"
      >Загрузка...</secondary-text
    >

    <!-- Why the list did not arrive, in its place. -->
    <ErrorState
      v-else-if="loadRefusal"
      :message="loadRefusal"
      :retry="fetchRequests"
    />

    <template v-else-if="pendingRequests.length === 0 && !loadFailed">
      <secondary-text>Нет активных запросов</secondary-text>
    </template>

    <div
      v-else-if="pendingRequests.length > 0"
      class="requests-list"
      :aria-busy="loading || undefined"
    >
      <div
        v-for="request in pendingRequests"
        :key="request.id"
        class="request-card"
      >
        <!-- No word of the state: every request here is waiting. -->
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
@use "@/assets/styles/Buttons" as *

.requests-list
  display: flex
  flex-direction: column
  gap: $medium

.request-card
  +card()

// The name on the left, the date level with it on the right while the list is wide.
.request-header
  display: flex
  justify-content: space-between
  align-items: center
  gap: $medium
  margin-bottom: $small

// A date is not broken: on two lines it reads as two facts.
.request-date
  font-size: $secondary-font-size
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

.approve-btn,
.reject-btn
  +button
</style>

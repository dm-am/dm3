<script setup lang="ts">
import { ref, onMounted, computed, reactive, type Ref } from "vue";
import { useModal } from "vue-final-modal";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "@/shared/lib/errors";
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

const toast = useToast();
const requests = ref<UsernameChangeRequest[]>([]);
const loading = ref(false);
const processing = ref<string | null>(null);

const getStatusLabel = (status: UsernameChangeRequestStatus): string => {
  switch (status) {
    case UsernameChangeRequestStatus.Pending:
      return "Ожидает";
    case UsernameChangeRequestStatus.Approved:
      return "Одобрено";
    case UsernameChangeRequestStatus.Rejected:
      return "Отклонено";
    case UsernameChangeRequestStatus.Completed:
      return "Завершено";
    case UsernameChangeRequestStatus.Expired:
      return "Истекло";
    default:
      return "Неизвестно";
  }
};

const getStatusClass = (status: UsernameChangeRequestStatus): string => {
  switch (status) {
    case UsernameChangeRequestStatus.Pending:
      return "pending";
    case UsernameChangeRequestStatus.Approved:
      return "approved";
    case UsernameChangeRequestStatus.Rejected:
      return "rejected";
    case UsernameChangeRequestStatus.Completed:
      return "completed";
    case UsernameChangeRequestStatus.Expired:
      return "expired";
    default:
      return "";
  }
};

const pendingRequests = computed(() =>
  requests.value.filter(
    (r) => r.status === UsernameChangeRequestStatus.Pending,
  ),
);

const fetchRequests = async () => {
  loading.value = true;
  try {
    const { data, error } =
      await moderationApi.getPendingUsernameChangeRequests();
    if (error) {
      notifyFailure(error, "Не удалось загрузить запросы");
      return;
    }
    requests.value = data?.resources || [];
  } finally {
    loading.value = false;
  }
};

// --- Approve (ConfirmDialog) ---
const approveTarget = ref<UsernameChangeRequest | null>(null);
const approving = ref(false);

const confirmApprove = async () => {
  if (!approveTarget.value || approving.value) return;
  approving.value = true;
  processing.value = approveTarget.value.id;
  try {
    const resolve: ResolveUsernameChangeRequest = {
      status: UsernameChangeRequestStatus.Approved,
    };
    const { error } = await moderationApi.resolveUsernameChangeRequest(
      approveTarget.value.id,
      resolve,
    );
    if (error) {
      notifyFailure(error, "Не удалось одобрить запрос");
      return;
    }
    toast.success(`Запрос от ${approveTarget.value.currentUsername} одобрен`);
    approveTarget.value = null;
    await fetchRequests();
  } finally {
    approving.value = false;
    processing.value = null;
  }
};

// --- Reject dialog (shared Dialog idiom) ---
// Null until the first openRejectModal sets it; the dialog only mounts
// after that, so the non-null cast below is safe.
const rejectingRequest = ref<UsernameChangeRequest | null>(null);

const { open: openRejectDialog, close: closeRejectDialog } = useModal({
  component: UsernameChangeRejectDialog,
  attrs: reactive({
    request: rejectingRequest as Ref<UsernameChangeRequest>,
    onSuccess: async (request: UsernameChangeRequest) => {
      toast.success(`Запрос от ${request.currentUsername} отклонен`);
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
  <div class="username-changes">
    <h3>Запросы на смену имени пользователя</h3>

    <secondary-text v-if="loading">Загрузка...</secondary-text>

    <template v-else-if="pendingRequests.length === 0">
      <secondary-text>Нет активных запросов</secondary-text>
    </template>

    <div v-else class="requests-list">
      <div
        v-for="request in pendingRequests"
        :key="request.id"
        class="request-card"
      >
        <div class="request-header">
          <div class="user-info">
            <router-link
              :to="{
                name: 'profile',
                params: { username: request.currentUsername },
              }"
              >{{ request.currentUsername }}</router-link
            >
            <span class="status-badge" :class="getStatusClass(request.status)">
              {{ getStatusLabel(request.status) }}
            </span>
          </div>
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
      title="Одобрение запроса"
      :message="`Одобрить запрос на смену имени от ${approveTarget?.currentUsername ?? ''}?`"
      confirm-label="Одобрить"
      :loading="approving"
      @update:show="(v) => !v && (approveTarget = null)"
      @confirm="confirmApprove"
    />
  </div>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.username-changes
  h3
    color: $text
    margin-bottom: $medium

.requests-list
  display: flex
  flex-direction: column
  gap: $medium

.request-card
  +card()

.request-header
  display: flex
  justify-content: space-between
  align-items: center
  margin-bottom: $small

.user-info
  display: flex
  align-items: center
  gap: $small

.status-badge
  font-size: 0.75rem
  padding: 2px 8px
  border-radius: $border-radius
  text-transform: uppercase
  font-weight: 500

  &.pending
    +tint($accent-yellow, 20%)
    color: $accent-yellow

  &.approved
    +tint($accent-green, 20%)
    color: $accent-green

  &.rejected
    +tint($accent-red, 20%)
    color: $accent-red

  &.completed
    +tint($link, 20%)
    color: $link

  &.expired
    +tint($text-muted, 20%)
    color: $text-muted

.request-date
  font-size: 0.85rem
  color: $text-muted

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

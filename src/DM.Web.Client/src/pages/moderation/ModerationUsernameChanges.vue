<script setup lang="ts">
import { ref, onMounted, computed } from "vue";
import { symbols } from "@/shared/lib/utils/icons";
import { useToast } from "@/shared/lib/composables/useToast";
import moderationApi, {
  UsernameChangeRequestStatus,
  type UsernameChangeRequest,
  type ResolveUsernameChangeRequest,
} from "@/shared/api/moderationApi";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import HumanDate from "@/shared/ui/Date/HumanDate.vue";
import UserLink from "@/entities/user/ui/UserLink.vue";

const toast = useToast();
const requests = ref<UsernameChangeRequest[]>([]);
const loading = ref(false);
const processing = ref<string | null>(null);

// Modal state
const showRejectModal = ref(false);
const rejectingRequest = ref<UsernameChangeRequest | null>(null);
const rejectComment = ref("");

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
    const { data } = await moderationApi.getPendingUsernameChangeRequests();
    requests.value = data?.resources || [];
  } catch (error) {
    toast.error("Не удалось загрузить запросы");
  } finally {
    loading.value = false;
  }
};

const approveRequest = async (request: UsernameChangeRequest) => {
  if (!confirm(`Одобрить запрос на смену имени от ${request.currentUsername}?`))
    return;

  processing.value = request.id;
  try {
    const resolve: ResolveUsernameChangeRequest = {
      status: UsernameChangeRequestStatus.Approved,
    };
    await moderationApi.resolveUsernameChangeRequest(request.id, resolve);
    toast.success(`Запрос от ${request.currentUsername} одобрен`);
    await fetchRequests();
  } catch (error) {
    toast.error("Не удалось одобрить запрос");
  } finally {
    processing.value = null;
  }
};

const openRejectModal = (request: UsernameChangeRequest) => {
  rejectingRequest.value = request;
  rejectComment.value = "";
  showRejectModal.value = true;
};

const closeRejectModal = () => {
  showRejectModal.value = false;
  rejectingRequest.value = null;
  rejectComment.value = "";
};

const confirmReject = async () => {
  if (!rejectingRequest.value) return;

  if (!rejectComment.value.trim()) {
    toast.error("Укажите причину отклонения");
    return;
  }

  processing.value = rejectingRequest.value.id;
  try {
    const resolve: ResolveUsernameChangeRequest = {
      status: UsernameChangeRequestStatus.Rejected,
      comment: rejectComment.value,
    };
    await moderationApi.resolveUsernameChangeRequest(
      rejectingRequest.value.id,
      resolve,
    );
    toast.success(
      `Запрос от ${rejectingRequest.value.currentUsername} отклонен`,
    );
    closeRejectModal();
    await fetchRequests();
  } catch (error) {
    toast.error("Не удалось отклонить запрос");
  } finally {
    processing.value = null;
  }
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
            @click="approveRequest(request)"
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

    <!-- Reject Modal -->
    <div
      v-if="showRejectModal"
      class="modal-overlay"
      @click.self="closeRejectModal"
    >
      <div class="modal">
        <div class="modal-header">
          <h4>Отклонение запроса</h4>
          <button class="close-btn" @click="closeRejectModal">
            {{ symbols.close }}
          </button>
        </div>
        <div class="modal-body">
          <p>
            Отклонить запрос на смену имени от
            <strong>{{ rejectingRequest?.currentUsername }}</strong
            >?
          </p>
          <div class="form-field">
            <label for="reject-reason">Причина отклонения</label>
            <textarea
              id="reject-reason"
              v-model="rejectComment"
              placeholder="Укажите причину отклонения..."
              rows="4"
            ></textarea>
          </div>
        </div>
        <div class="modal-footer">
          <button class="cancel-btn" @click="closeRejectModal">Отмена</button>
          <button
            class="confirm-reject-btn"
            :disabled="processing !== null"
            @click="confirmReject"
          >
            {{ processing ? "Отклонение..." : "Отклонить" }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"
@import "src/assets/styles/ZIndex"

.username-changes
  h3
    color: $text
    margin-bottom: $medium

.requests-list
  display: flex
  flex-direction: column
  gap: $medium

.request-card
  border: 1px solid $border
  border-radius: $border-radius
  padding: $medium
  background: $bg-element

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
    background: rgba($accent-yellow, 0.2)
    color: $accent-yellow

  &.approved
    background: rgba($accent-green, 0.2)
    color: $accent-green

  &.rejected
    background: rgba($accent-red, 0.2)
    color: $accent-red

  &.completed
    background: rgba($link, 0.2)
    color: $link

  &.expired
    background: rgba($text-muted, 0.2)
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

// Modal styles
.modal-overlay
  position: fixed
  top: 0
  left: 0
  right: 0
  bottom: 0
  background: rgba(0, 0, 0, 0.5)
  display: flex
  align-items: center
  justify-content: center
  z-index: $z-modal

.modal
  background: $bg-element
  border-radius: $border-radius
  width: 100%
  max-width: 500px
  margin: $medium

.modal-header
  display: flex
  justify-content: space-between
  align-items: center
  padding: $medium
  border-bottom: 1px solid $border

  h4
    margin: 0
    color: $text

.close-btn
  width: 32px
  height: 32px
  border: none
  border-radius: 50%
  background: transparent
  color: $text-muted
  cursor: pointer
  font-size: 1.5rem
  line-height: 1
  display: flex
  align-items: center
  justify-content: center

  &:hover
    background: $hover-overlay
    color: $text

.modal-body
  padding: $medium

  p
    margin: 0 0 $medium 0
    color: $text

.form-field
  label
    display: block
    margin-bottom: $minor
    font-weight: 500
    color: $text

  textarea
    width: 100%
    padding: $small
    border: 1px solid $border
    border-radius: $border-radius
    background: $input-bg
    color: $text
    font-family: inherit
    font-size: 1rem
    resize: vertical

    &:focus
      outline: none
      border-color: $accent-red

.modal-footer
  display: flex
  justify-content: flex-end
  gap: $small
  padding: $medium
  border-top: 1px solid $border

.cancel-btn
  +button

.confirm-reject-btn
  +button
</style>

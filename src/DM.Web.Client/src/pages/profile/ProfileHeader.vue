<script setup lang="ts">
import { computed, ref, onMounted, watch } from "vue";
import { storeToRefs } from "pinia";
import { useModal } from "vue-final-modal";
import {
  useUserStore,
  useCommunityStore,
  type User,
  type Username,
  type UsernameHistoryEntry,
  UserRole,
} from "@/entities/user";
import { useSubscriptionsStore } from "@/shared/stores/subscriptions";
import type { BlacklistEntry } from "@/shared/api/models/personal";
import { SubscriptionTargetType } from "@/shared/api/models/subscriptions";
import { communityApi } from "@/shared/api";
import { blacklistApi } from "@/shared/api";
import { ROLE_INFO, STAFF_ROLES } from "@/shared/config/roles";
import defaultPicture from "@/assets/images/userpic.png";
import ProfilePicture from "./ProfilePicture.vue";
import Button from "@/shared/ui/Button/Button.vue";
import { EditableField } from "@/shared/ui/EditableField";
import BlockUserLightbox from "@/pages/account/BlockUserLightbox.vue";
import { useToast } from "@/shared/lib/composables/useToast";
import dayjs from "dayjs";

const props = defineProps<{
  user: User;
  isEditMode: boolean;
  canEdit: boolean;
  hasChanges: boolean;
  isSaving: boolean;
}>();

const emit = defineEmits<{
  (e: "toggleEdit"): void;
  (e: "save"): void;
  (e: "cancel"): void;
  (e: "updateField", field: string, value: string): void;
}>();

const { user: currentUser } = storeToRefs(useUserStore());
const subscriptionsStore = useSubscriptionsStore();
const toast = useToast();

// Block functionality
const isBlocked = ref(false);
const isBlockLoading = ref(false);

// Subscribe functionality
const isSubscribeLoading = ref(false);
const isSubscribed = computed(() => {
  if (!props.user.id) return false;
  return subscriptionsStore.isSubscribed(
    SubscriptionTargetType.User,
    props.user.id,
  );
});

async function toggleSubscribe() {
  if (!props.user.id) return;
  isSubscribeLoading.value = true;
  try {
    if (isSubscribed.value) {
      await subscriptionsStore.unsubscribeByTarget(
        SubscriptionTargetType.User,
        props.user.id,
      );
      toast.success(`Вы отписались от ${props.user.username}`);
    } else {
      await subscriptionsStore.subscribe(
        SubscriptionTargetType.User,
        props.user.id,
      );
      toast.success(`Вы подписались на ${props.user.username}`);
    }
  } catch {
    toast.error(
      isSubscribed.value ? "Не удалось отписаться" : "Не удалось подписаться",
    );
  } finally {
    isSubscribeLoading.value = false;
  }
}

const { open: openBlockModal, close: closeBlockModal } = useModal({
  component: BlockUserLightbox,
  attrs: {
    initialUsername: props.user.username,
    onSuccess: (entry: BlacklistEntry) => {
      isBlocked.value = true;
      closeBlockModal();
      toast.success(`${entry.username} заблокирован`);
    },
    onCancel: () => closeBlockModal(),
  },
});

async function checkIfBlocked() {
  if (!currentUser.value || isOwnProfile.value) return;
  const { data } = await blacklistApi.getBlacklist();
  if (data?.resources) {
    isBlocked.value = data.resources.some(
      (e) => e.username === props.user.username,
    );
  }
}

async function unblockUser() {
  const confirmed = window.confirm(`Разблокировать ${props.user.username}?`);
  if (!confirmed) return;

  isBlockLoading.value = true;
  const { error } = await blacklistApi.unblockUser(props.user.username);
  isBlockLoading.value = false;
  if (error) {
    toast.error("Не удалось разблокировать пользователя");
  } else {
    isBlocked.value = false;
    toast.success(`${props.user.username} разблокирован`);
  }
}

const pictureUrl = computed(
  () =>
    props.user.mediumPictureUrl ||
    props.user.originalPictureUrl ||
    defaultPicture,
);

const userRoles = computed(() => {
  const roles = (props.user.roles ?? [])
    .filter((r) => STAFF_ROLES.includes(r as UserRole))
    .map((r) => ROLE_INFO[r as UserRole].nickname)
    .filter(Boolean);
  if (props.user.isHonorary) {
    roles.push("Почетный гоблин");
  }
  return roles;
});

const registrationDate = computed(() => {
  if (!props.user.registrationUtc) return "";
  return dayjs(props.user.registrationUtc).format("DD.MM.YYYY");
});

const lastOnline = computed(() => {
  if (!props.user.lastActivityUtc) return "";
  return dayjs(props.user.lastActivityUtc).format("DD.MM.YYYY HH:mm");
});

const isOwnProfile = computed(
  () => currentUser.value?.username === props.user.username,
);

// Username history for tooltip
const usernameHistory = ref<UsernameHistoryEntry[]>([]);
const showUsernameHistory = ref(false);

onMounted(async () => {
  const { data } = await communityApi.getUsernameHistory(
    props.user.username as Username,
  );
  if (data?.resources) {
    usernameHistory.value = data.resources;
  }
  await checkIfBlocked();
  // Load subscriptions if logged in
  if (currentUser.value && !isOwnProfile.value) {
    subscriptionsStore.fetchSubscriptions();
  }
});

const handleStatusChange = (value: string) => {
  emit("updateField", "status", value);
};
</script>

<template>
  <div class="profile-header">
    <div class="header-left">
      <div class="avatar-wrapper">
        <img :src="pictureUrl" :alt="user.username" class="avatar" />
        <profile-picture
          v-if="isEditMode && canEdit"
          :username="user.username as Username"
        />
      </div>
    </div>

    <div class="header-main">
      <div class="name-row">
        <h1
          class="username"
          @mouseenter="showUsernameHistory = true"
          @mouseleave="showUsernameHistory = false"
        >
          {{ user.username }}
          <div
            v-if="showUsernameHistory && usernameHistory.length"
            class="username-history-tooltip"
          >
            <div class="tooltip-title">Прошлые имена</div>
            <div
              v-for="entry in usernameHistory"
              :key="entry.id"
              class="history-entry"
            >
              <span class="old-username">{{ entry.oldUsername }}</span>
              <span class="date">{{
                dayjs(entry.changedUtc).format("DD.MM.YYYY")
              }}</span>
            </div>
          </div>
        </h1>
        <span v-if="userRoles.length" class="roles">{{
          userRoles.join(", ")
        }}</span>
      </div>

      <div class="status-row">
        <editable-field
          :modelValue="user.status"
          :editing="isEditMode"
          label="Статус"
          placeholder="Введите статус"
          empty="Статус не указан"
          @update:modelValue="handleStatusChange"
        />
      </div>

      <div class="stats-row">
        <div v-if="user.rating?.isEnabled" class="rating">
          <span class="rating-value">{{ user.rating.totalRating }}</span>
          <span class="rating-label">рейтинг</span>
        </div>
        <div class="activity">
          <div class="activity-item">
            <span class="activity-label">Регистрация:</span>
            <span class="activity-value">{{ registrationDate }}</span>
          </div>
          <div class="activity-item">
            <span class="activity-label">Был онлайн:</span>
            <span class="activity-value">{{ lastOnline }}</span>
          </div>
        </div>
      </div>
    </div>

    <div class="header-actions">
      <template v-if="canEdit">
        <template v-if="isEditMode">
          <Button v-if="hasChanges" :disabled="isSaving" @click="emit('save')">
            {{ isSaving ? "Сохранение..." : "Сохранить" }}
          </Button>
          <Button secondary @click="emit('cancel')">Отмена</Button>
        </template>
        <Button v-else @click="emit('toggleEdit')">Редактировать</Button>
      </template>
      <template v-else-if="currentUser && !isOwnProfile">
        <router-link
          :to="{ name: 'direct-message', params: { username: user.username } }"
          class="message-link"
        >
          <Button>Написать</Button>
        </router-link>
        <Button
          :disabled="isSubscribeLoading"
          :class="{ subscribed: isSubscribed }"
          @click="toggleSubscribe"
        >
          {{
            isSubscribeLoading
              ? "..."
              : isSubscribed
                ? "Отписаться"
                : "Подписаться"
          }}
        </Button>
        <Button
          v-if="isBlocked"
          secondary
          :disabled="isBlockLoading"
          @click="unblockUser"
        >
          {{ isBlockLoading ? "..." : "Разблокировать" }}
        </Button>
        <Button v-else secondary @click="openBlockModal">
          Заблокировать
        </Button>
      </template>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.profile-header
  display: flex
  gap: $big
  padding: $medium
  background: $bg-element
  border-radius: $border-radius
  margin-bottom: $medium

.header-left
  flex-shrink: 0

.avatar-wrapper
  position: relative
  width: $grid-step * 30
  height: $grid-step * 30

.avatar
  width: 100%
  height: 100%
  object-fit: cover
  border-radius: $border-radius

.header-main
  flex: 1
  min-width: 0

.name-row
  display: flex
  align-items: baseline
  gap: $small
  flex-wrap: wrap
  margin-bottom: $small

.username
  font-size: 1.5rem
  font-weight: bold
  color: $text
  margin: 0
  position: relative
  cursor: default

.username-history-tooltip
  position: absolute
  top: 100%
  left: 0
  z-index: 10
  background: $bg-element-overlay
  border: 1px solid $border
  border-radius: $border-radius
  padding: $small
  min-width: 200px
  box-shadow: 0 2px 8px $shadow-color

.tooltip-title
  font-size: $secondary-font-size
  color: $text-muted
  margin-bottom: $tiny
  font-weight: normal

.history-entry
  display: flex
  justify-content: space-between
  gap: $small
  padding: $tiny 0
  font-size: $secondary-font-size
  font-weight: normal

.old-username
  color: $text
  text-decoration: line-through

.date
  color: $text-meta

.roles
  color: $text-muted
  font-size: $secondary-font-size

.status-row
  margin-bottom: $small

.stats-row
  display: flex
  align-items: flex-start
  gap: $big

.rating
  display: flex
  flex-direction: column
  align-items: center

.rating-value
  font-size: 1.5rem
  font-weight: bold
  color: $accent-green

.rating-label
  font-size: $secondary-font-size
  color: $text-muted

.activity
  display: flex
  flex-direction: column
  gap: $tiny

.activity-item
  font-size: $secondary-font-size

.activity-label
  color: $text-muted

.activity-value
  color: $text
  margin-left: $tiny

.header-actions
  display: flex
  flex-direction: column
  gap: $small
  align-self: flex-start

.message-link
  text-decoration: none
</style>

<script setup lang="ts">
import { computed, ref, onMounted } from "vue";
import { storeToRefs } from "pinia";
import { useUserStore, useCommunityStore } from "@/stores";
import type { User, UserLogin, LoginHistoryEntry } from "@/api/models/community";
import { UserRole } from "@/api/models/community";
import communityApi from "@/api/requests/communityApi";
import { ROLE_INFO, STAFF_ROLES } from "@/constants/roles";
import defaultPicture from "@/assets/images/userpic.png";
import ProfilePicture from "./ProfilePicture.vue";
import TheButton from "@/components/inputs/TheButton.vue";
import EditableField from "@/components/inputs/EditableField.vue";
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

const pictureUrl = computed(
  () => props.user.mediumPictureUrl || props.user.originalPictureUrl || defaultPicture,
);

const userRoles = computed(() => {
  const roles =
    props.user.roles
      .filter((r) => STAFF_ROLES.includes(r as UserRole))
      .map((r) => ROLE_INFO[r as UserRole].nickname)
      .filter(Boolean) || [];
  if (props.user.isHonorary) {
    roles.push("Почетный гоблин");
  }
  return roles;
});

const registrationDate = computed(() => {
  if (!props.user.registrationDateUtc) return "";
  return dayjs(props.user.registrationDateUtc).format("DD.MM.YYYY");
});

const lastOnline = computed(() => {
  if (!props.user.lastActivityUtc) return "";
  return dayjs(props.user.lastActivityUtc).format("DD.MM.YYYY HH:mm");
});

const isOwnProfile = computed(
  () => currentUser.value?.login === props.user.login,
);

// Login history for tooltip
const loginHistory = ref<LoginHistoryEntry[]>([]);
const showLoginHistory = ref(false);

onMounted(async () => {
  const { data } = await communityApi.getLoginHistory(props.user.login as UserLogin);
  if (data?.resources) {
    loginHistory.value = data.resources;
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
        <img
          :src="pictureUrl"
          :alt="user.login"
          class="avatar"
        />
        <profile-picture
          v-if="isEditMode && canEdit"
          :login="(user.login as UserLogin)"
        />
      </div>
    </div>

    <div class="header-main">
      <div class="name-row">
        <h1
          class="login"
          @mouseenter="showLoginHistory = true"
          @mouseleave="showLoginHistory = false"
        >
          {{ user.login }}
          <div v-if="showLoginHistory && loginHistory.length" class="login-history-tooltip">
            <div class="tooltip-title">Прошлые имена</div>
            <div
              v-for="entry in loginHistory"
              :key="entry.id"
              class="history-entry"
            >
              <span class="old-login">{{ entry.oldLogin }}</span>
              <span class="date">{{ dayjs(entry.changedUtc).format("DD.MM.YYYY") }}</span>
            </div>
          </div>
        </h1>
        <span v-if="userRoles.length" class="roles">{{ userRoles.join(", ") }}</span>
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
          <the-button
            v-if="hasChanges"
            :disabled="isSaving"
            @click="emit('save')"
          >
            {{ isSaving ? "Сохранение..." : "Сохранить" }}
          </the-button>
          <the-button secondary @click="emit('cancel')">Отмена</the-button>
        </template>
        <the-button v-else @click="emit('toggleEdit')">Редактировать</the-button>
      </template>
      <router-link
        v-else-if="currentUser && !isOwnProfile"
        :to="{ name: 'direct-message', params: { login: user.login } }"
        class="message-link"
      >
        <the-button>Написать</the-button>
      </router-link>
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

.login
  font-size: 1.5rem
  font-weight: bold
  color: $text
  margin: 0
  position: relative
  cursor: default

.login-history-tooltip
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

.old-login
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

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { symbols } from "@/shared/lib/utils/icons";
import {
  notificationApi,
  notificationLink,
  notificationTitle,
} from "@/entities/notification";
import type { UserNotification } from "@/shared/api/models/notifications";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "@/shared/lib/errors";

const toast = useToast();
const notifications = ref<UserNotification[]>([]);
const loading = ref(false);
const hasMore = ref(true);
const skip = ref(0);
const take = 20;

const filteredNotifications = computed(() => {
  return notifications.value;
});

const getNotificationDescription = (notification: UserNotification): string => {
  const payload = notification.payload;
  if (!payload) return "";

  // Try to build a description from payload
  const parts: string[] = [];

  if (payload.authorUsername) {
    parts.push(payload.authorUsername);
  }
  if (payload.likerUsername) {
    parts.push(payload.likerUsername);
  }
  if (payload.inviterUsername) {
    parts.push(`от ${payload.inviterUsername}`);
  }
  if (payload.gameTitle) {
    parts.push(`в "${payload.gameTitle}"`);
  }
  if (payload.topicTitle) {
    parts.push(`"${payload.topicTitle}"`);
  }
  if (payload.publicationTitle) {
    parts.push(`"${payload.publicationTitle}"`);
  }
  if (payload.blogTitle) {
    parts.push(`в блоге "${payload.blogTitle}"`);
  }
  if (payload.contextTitle) {
    parts.push(`в "${payload.contextTitle}"`);
  }

  return parts.join(" ");
};

const fetchNotifications = async (reset = false) => {
  if (loading.value) return;

  if (reset) {
    skip.value = 0;
    notifications.value = [];
    hasMore.value = true;
  }

  loading.value = true;
  try {
    const { data, error } = await notificationApi.getNotifications(
      skip.value,
      take,
    );
    if (error) {
      // hasMore is left alone: a failed page is not the end of the list, and
      // clearing it would turn a transient error into "больше ничего нет".
      notifyFailure(error, "Не удалось загрузить уведомления");
      return;
    }
    const newItems = data?.resources || [];
    notifications.value = [...notifications.value, ...newItems];
    hasMore.value = newItems.length === take;
    skip.value += newItems.length;
  } finally {
    loading.value = false;
  }
};

const markAllAsRead = async () => {
  const { error } = await notificationApi.markAsRead();
  if (error) {
    notifyFailure(error, "Не удалось отметить уведомления");
    return;
  }
  toast.success("Все уведомления отмечены как прочитанные");
};

const markAsRead = async (id: string) => {
  const { error } = await notificationApi.markAsRead(id);
  if (error) {
    notifyFailure(error, "Не удалось отметить уведомление");
    return;
  }
  notifications.value = notifications.value.filter((n) => n.id !== id);
};

onMounted(() => fetchNotifications());
</script>

<template>
  <div class="notifications-page">
    <div class="page-header">
      <page-title>Уведомления</page-title>
      <button
        v-if="notifications.length > 0"
        class="mark-all-btn"
        @click="markAllAsRead"
      >
        Отметить все прочитанными
      </button>
    </div>

    <secondary-text v-if="loading && notifications.length === 0"
      >Загрузка...</secondary-text
    >

    <template v-else-if="notifications.length === 0">
      <secondary-text>Нет уведомлений</secondary-text>
    </template>

    <ul v-else class="notification-list">
      <li
        v-for="notification in filteredNotifications"
        :key="notification.id"
        class="notification-item"
      >
        <div class="notification-content">
          <span class="notification-type">
            {{ notificationTitle(notification.eventType) }}
          </span>
          <p class="notification-description">
            {{ getNotificationDescription(notification) }}
          </p>
        </div>

        <div class="notification-actions">
          <router-link
            v-if="notificationLink(notification)"
            :to="notificationLink(notification)!"
            class="view-link"
          >
            Перейти
          </router-link>
          <Tooltip text="Отметить прочитанным">
            <button
              class="dismiss-btn"
              @click="markAsRead(notification.id)"
              aria-label="Отметить прочитанным"
            >
              {{ symbols.close }}
            </button>
          </Tooltip>
        </div>
      </li>
    </ul>

    <div v-if="hasMore && notifications.length > 0" class="load-more">
      <button
        class="load-more-btn"
        :disabled="loading"
        @click="fetchNotifications()"
      >
        <template v-if="loading">Загрузка...</template>
        <template v-else>Показать еще</template>
      </button>
    </div>
  </div>
</template>

<style scoped lang="sass">
.notifications-page
  padding: $medium

.page-header
  display: flex
  justify-content: space-between
  align-items: center
  margin-bottom: $medium

  h1
    margin: 0

.mark-all-btn
  padding: $minor $small
  border: 1px solid $accent-green
  border-radius: $border-radius
  background: transparent
  color: $accent-green
  cursor: pointer
  font-size: 0.85rem

  // Заливка тинтом, а не сплошным акцентом: в темной теме $text-on-green и
  // $accent-green — один и тот же hex, текст исчезал.
  &:hover
    +tint($accent-green, 20%)

.notification-list
  list-style: none
  padding: 0
  margin: 0

.notification-item
  display: flex
  align-items: flex-start
  gap: $small
  padding: $small 0
  border-bottom: 1px solid $border

  &:last-child
    border-bottom: none

.notification-icon
  width: 32px
  height: 32px
  display: flex
  align-items: center
  justify-content: center
  border-radius: 50%
  background: $bg-element-overlay
  color: $text-muted
  flex-shrink: 0

  &.blog
    +tint($accent-green, 10%)
    color: $accent-green

  &.comment
    +tint($link, 10%)
    color: $link

  &.dice
    +tint($accent-yellow, 10%)
    color: $accent-yellow

  &.bell
    +tint($link, 10%)
    color: $link

.notification-content
  flex: 1
  min-width: 0

.notification-type
  font-weight: 500
  color: $text
  display: block
  margin-bottom: 2px

.notification-description
  margin: 0
  font-size: 0.9rem
  color: $text-muted
  white-space: nowrap
  overflow: hidden
  text-overflow: ellipsis

.notification-actions
  display: flex
  align-items: center
  gap: $minor
  flex-shrink: 0

.view-link
  padding: $minor $small
  border: 1px solid $link
  border-radius: $border-radius
  background: transparent
  color: $link
  cursor: pointer
  font-size: 0.8rem
  text-decoration: none

  &:hover
    background: $link
    color: $text-on-fill

.dismiss-btn
  width: 24px
  height: 24px
  border: none
  border-radius: 50%
  background: transparent
  color: $text-muted
  cursor: pointer
  font-size: 1.2rem
  line-height: 1
  display: flex
  align-items: center
  justify-content: center

  &:hover
    background: $hover-overlay
    color: $accent-red

.load-more
  display: flex
  justify-content: center
  margin-top: $medium

.load-more-btn
  padding: $small $medium
  border: 1px solid $border
  border-radius: $border-radius
  background: transparent
  color: $text
  cursor: pointer

  &:hover:not(:disabled)
    background: $hover-overlay

  &:disabled
    opacity: $disabled-opacity
    cursor: default
</style>

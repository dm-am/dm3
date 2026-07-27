<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { symbols } from "@/shared/lib/utils/icons";
import notificationApi from "@/shared/api/notificationApi";
import {
  NotificationType,
  type UserNotification,
} from "@/shared/api/models/notifications";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { useToast } from "@/shared/lib/composables/useToast";

const toast = useToast();
const notifications = ref<UserNotification[]>([]);
const loading = ref(false);
const hasMore = ref(true);
const skip = ref(0);
const take = 20;

const filteredNotifications = computed(() => {
  return notifications.value;
});

const getNotificationTypeLabel = (type: NotificationType): string => {
  switch (type) {
    case NotificationType.NewPublication:
      return "Новая публикация";
    case NotificationType.LikedPublication:
      return "Лайк публикации";
    case NotificationType.NewBlogComment:
      return "Комментарий в блоге";
    case NotificationType.LikedBlogComment:
      return "Лайк комментария";
    case NotificationType.BlogInvitationCreated:
      return "Приглашение в блог";
    case NotificationType.BlogInvitationAccepted:
      return "Приглашение принято";
    case NotificationType.BlogInvitationRejected:
      return "Приглашение отклонено";
    case NotificationType.NewTopicInSubscribedBoard:
      return "Новый топик в разделе";
    case NotificationType.NewCommentInSubscribedTopic:
      return "Комментарий в топике";
    case NotificationType.NewGameFromSubscribedAuthor:
      return "Новая игра автора";
    case NotificationType.NewPostInSubscribedGame:
      return "Новый пост в игре";
    case NotificationType.UserMentioned:
      return "Упоминание";
    case NotificationType.NewBlogFromSubscribedAuthor:
      return "Новый блог автора";
    case NotificationType.NewTopicFromSubscribedAuthor:
      return "Топик от автора";
    case NotificationType.NewForumTopic:
      return "Новый топик форума";
    case NotificationType.LikedTopic:
      return "Лайк топика";
    case NotificationType.NewForumComment:
      return "Комментарий форума";
    case NotificationType.LikedForumComment:
      return "Лайк комментария";
    case NotificationType.NewGame:
      return "Новая игра";
    case NotificationType.NewCharacter:
      return "Новый персонаж";
    default:
      return "Уведомление";
  }
};

const getNotificationLink = (notification: UserNotification): string | null => {
  const payload = notification.payload;
  if (!payload) return null;

  switch (notification.eventType) {
    case NotificationType.NewPublication:
    case NotificationType.LikedPublication:
    case NotificationType.NewBlogFromSubscribedAuthor:
      return payload.blogId ? `/blogs/${payload.blogId}` : null;

    case NotificationType.NewBlogComment:
    case NotificationType.LikedBlogComment:
      return payload.blogId ? `/blogs/${payload.blogId}` : null;

    case NotificationType.BlogInvitationCreated:
    case NotificationType.BlogInvitationAccepted:
    case NotificationType.BlogInvitationRejected:
      return payload.blogId ? `/blogs/${payload.blogId}` : null;

    case NotificationType.NewTopicInSubscribedBoard:
    case NotificationType.NewTopicFromSubscribedAuthor:
      return payload.topicId ? `/forum-topic/${payload.topicId}` : null;

    case NotificationType.NewCommentInSubscribedTopic:
      return payload.topicId ? `/forum-topic/${payload.topicId}` : null;

    case NotificationType.NewGameFromSubscribedAuthor:
    case NotificationType.NewGame:
      return payload.gameId ? `/game/${payload.gameId}` : null;

    case NotificationType.NewPostInSubscribedGame:
      return payload.gameId ? `/game/${payload.gameId}` : null;

    case NotificationType.NewCharacter:
      return payload.gameId ? `/game/${payload.gameId}/characters` : null;

    case NotificationType.LikedTopic:
    case NotificationType.NewForumTopic:
      return payload.topicId ? `/forum-topic/${payload.topicId}` : null;

    case NotificationType.NewForumComment:
    case NotificationType.LikedForumComment:
      return payload.topicId ? `/forum-topic/${payload.topicId}` : null;

    default:
      return null;
  }
};

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
    const { data } = await notificationApi.getNotifications(skip.value, take);
    const newItems = data?.resources || [];
    notifications.value = [...notifications.value, ...newItems];
    hasMore.value = newItems.length === take;
    skip.value += newItems.length;
  } catch (error) {
    toast.error("Не удалось загрузить уведомления");
  } finally {
    loading.value = false;
  }
};

const markAllAsRead = async () => {
  try {
    await notificationApi.markAsRead();
    toast.success("Все уведомления отмечены как прочитанные");
  } catch (error) {
    toast.error("Не удалось отметить уведомления");
  }
};

const markAsRead = async (id: string) => {
  try {
    await notificationApi.markAsRead(id);
    // Remove from list or mark as read in UI
    notifications.value = notifications.value.filter((n) => n.id !== id);
  } catch (error) {
    toast.error("Не удалось отметить уведомление");
  }
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
      >Загрузка…</secondary-text
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
            {{ getNotificationTypeLabel(notification.eventType) }}
          </span>
          <p class="notification-description">
            {{ getNotificationDescription(notification) }}
          </p>
        </div>

        <div class="notification-actions">
          <router-link
            v-if="getNotificationLink(notification)"
            :to="getNotificationLink(notification)!"
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
        <template v-if="loading">Загрузка…</template>
        <template v-else>Загрузить еще</template>
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

  &:hover
    background: $accent-green
    color: $text-on-green

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
    background: rgba($accent-green, 0.1)
    color: $accent-green

  &.comment
    background: rgba($link, 0.1)
    color: $link

  &.dice
    background: rgba($accent-yellow, 0.1)
    color: $accent-yellow

  &.bell
    background: rgba($link, 0.1)
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
    color: white

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

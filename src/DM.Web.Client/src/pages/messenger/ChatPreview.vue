<script setup lang="ts">
import { computed } from "vue";
import type { Chat } from "@/entities/message";
import type { User } from "@/shared/api/models/community";
import defaultPicture from "@/assets/images/userpic.png";
import dayjs from "dayjs";

const props = defineProps<{
  chat: Chat;
  interlocutor?: User;
  currentUser?: User | null;
}>();

const userPicture = computed(
  () => props.interlocutor?.smallPictureUrl || defaultPicture,
);

const isLastMessageFromMe = computed(() => {
  const msg = props.chat.lastMessage;
  if (!msg || !props.currentUser) return false;
  return msg.author?.username === props.currentUser.username;
});

const lastMessagePreview = computed(() => {
  const msg = props.chat.lastMessage;
  if (!msg) return "";
  // Strip HTML tags for preview
  const text = (msg.text || "").replace(/<[^>]*>/g, "");
  const truncated = text.length > 60 ? text.slice(0, 60) + "..." : text;
  // Add prefix to indicate who sent the message
  const prefix = isLastMessageFromMe.value ? "Вы: " : "";
  return prefix + truncated;
});

const lastMessageDate = computed(() => {
  const msg = props.chat.lastMessage;
  if (!msg?.createdUtc) return "";
  return dayjs(msg.createdUtc).format("DD.MM.YYYY HH:mm");
});

const hasUnread = computed(
  () => (props.chat.unreadMessagesCount ?? 0) > 0,
);
</script>

<template>
  <router-link
    :to="{ name: 'chat', params: { id: chat.id } }"
    class="chat-preview"
    :class="{ 'has-unread': hasUnread }"
  >
    <img :src="userPicture" :alt="interlocutor?.username" class="avatar" />

    <div class="content">
      <div class="header">
        <span class="username">{{
          interlocutor?.username || "Пользователь"
        }}</span>
        <span class="date">{{ lastMessageDate }}</span>
      </div>
      <div class="message-preview">
        {{ lastMessagePreview || "Нет сообщений" }}
      </div>
    </div>

    <div v-if="hasUnread" class="unread-count">
      {{ chat.unreadMessagesCount }}
    </div>
  </router-link>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.chat-preview
  display: flex
  align-items: center
  gap: $medium
  padding: $medium
  border-radius: $border-radius
  text-decoration: none
  background-color: $bg-element
  color: $text
  cursor: pointer
  transition: background-color $animation-time

  &:hover
    background-color: $bg-highlight-blue

  &.has-unread
    background-color: $bg-highlight-blue

.avatar
  width: $grid-step * 12
  height: $grid-step * 12
  border-radius: 50%
  object-fit: cover
  flex-shrink: 0

.content
  flex: 1
  min-width: 0
  overflow: hidden

.header
  display: flex
  justify-content: space-between
  align-items: baseline
  margin-bottom: $tiny

.username
  font-weight: bold
  color: $link

.date
  font-size: $secondary-font-size
  color: $text-muted

.message-preview
  font-size: $secondary-font-size
  color: $text-muted
  white-space: nowrap
  overflow: hidden
  text-overflow: ellipsis

.unread-count
  padding: $tiny $small
  border-radius: $border-radius
  font-size: $secondary-font-size
  font-weight: bold
  background-color: $button-bg
  color: $button-text
</style>

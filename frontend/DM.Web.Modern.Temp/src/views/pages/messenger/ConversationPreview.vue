<script setup lang="ts">
import { computed } from "vue";
import type { Conversation } from "@/api/models/messaging";
import type { User } from "@/api/models/community";
import defaultPicture from "@/assets/images/userpic.png";
import dayjs from "dayjs";

const props = defineProps<{
  conversation: Conversation;
  interlocutor?: User;
  currentUser?: User | null;
}>();

const userPicture = computed(() => props.interlocutor?.smallPictureUrl || defaultPicture);

const isLastMessageFromMe = computed(() => {
  const msg = props.conversation.lastMessage;
  if (!msg || !props.currentUser) return false;
  return msg.author?.login === props.currentUser.login;
});

const lastMessagePreview = computed(() => {
  const msg = props.conversation.lastMessage;
  if (!msg) return "";
  // Strip HTML tags for preview
  const text = (msg.text || "").replace(/<[^>]*>/g, "");
  const truncated = text.length > 60 ? text.slice(0, 60) + "..." : text;
  // Add prefix to indicate who sent the message
  const prefix = isLastMessageFromMe.value ? "Вы: " : "";
  return prefix + truncated;
});

const lastMessageDate = computed(() => {
  const msg = props.conversation.lastMessage;
  if (!msg?.createdUtc) return "";
  return dayjs(msg.createdUtc).format("DD.MM.YYYY HH:mm");
});

const hasUnread = computed(() => (props.conversation.unreadMessagesCount ?? 0) > 0);
</script>

<template>
  <router-link
    :to="{ name: 'conversation', params: { id: conversation.id } }"
    class="conversation-preview"
    :class="{ 'has-unread': hasUnread }"
  >
    <img :src="userPicture" :alt="interlocutor?.login" class="avatar" />

    <div class="content">
      <div class="header">
        <span class="username">{{ interlocutor?.login || "Пользователь" }}</span>
        <span class="date">{{ lastMessageDate }}</span>
      </div>
      <div class="message-preview">
        {{ lastMessagePreview || "Нет сообщений" }}
      </div>
    </div>

    <div v-if="hasUnread" class="unread-count">
      {{ conversation.unreadMessagesCount }}
    </div>
  </router-link>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.conversation-preview
  display: flex
  align-items: center
  gap: $medium
  padding: $medium
  border-radius: $border-radius
  text-decoration: none
  +theme(background-color, $panel-background)
  +theme(color, $text)
  cursor: pointer
  transition: background-color $animation-time

  &:hover
    +theme(background-color, $panel-background-highlight)

  &.has-unread
    +theme(background-color, $panel-background-highlight)

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
  +theme(color, $active-text)

.date
  font-size: $secondary-font-size
  +theme(color, $secondary-text)

.message-preview
  font-size: $secondary-font-size
  +theme(color, $secondary-text)
  white-space: nowrap
  overflow: hidden
  text-overflow: ellipsis

.unread-count
  padding: $tiny $small
  border-radius: $border-radius
  font-size: $secondary-font-size
  font-weight: bold
  +theme(background-color, $button-background)
  +theme(color, $button-text)
</style>

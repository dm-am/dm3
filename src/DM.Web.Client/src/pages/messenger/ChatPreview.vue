<script setup lang="ts">
/**
 * A row of the list of conversations: the picture, the name with its unread
 * count and the time on the first line, the last message under them. It opens
 * its conversation with the list kept on its page (listPage.ts).
 */
import { computed } from "vue";
import { useRoute } from "vue-router";
import type { Chat } from "@/entities/message";
import type { User } from "@/shared/api/models/community";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { listPageQuery } from "./listPage";
import {
  DATE_FORMAT,
  formatSiteTime,
  siteTime,
} from "@/shared/lib/utils/datetime";

const props = withDefaults(
  defineProps<{
    chat: Chat;
    interlocutor?: User;
    currentUser?: User | null;
  }>(),
  { interlocutor: undefined, currentUser: null },
);

const route = useRoute();

const isGroup = computed(() => props.chat.type === "Group");

/** The conversation, the list beside it on the page it stands at. */
const target = computed(() => ({
  name: "chat",
  params: { id: props.chat.id },
  query: listPageQuery(route.query),
}));

const isLastMessageFromMe = computed(() => {
  const msg = props.chat.lastMessage;
  if (!msg || !props.currentUser) return false;
  return msg.author?.username === props.currentUser.username;
});

const lastMessagePreview = computed(() => {
  const msg = props.chat.lastMessage;
  if (!msg) return "";
  // A drawn line break becomes a space first, or two lines run their words together.
  const text = (msg.text || "")
    .replace(/<(?:br|hr)\s*\/?>/gi, " ")
    .replace(/<[^>]*>/g, "");
  const truncated = text.length > 60 ? `${text.slice(0, 60)}...` : text;
  const prefix = isLastMessageFromMe.value ? "Вы: " : "";
  return prefix + truncated;
});

/** The time for a message of today, the date for an older one, on the site's clock. */
const lastMessageDate = computed(() => {
  const created = props.chat.lastMessage?.createdUtc;
  if (!created) return "";
  const today = siteTime().format("YYYY-MM-DD");
  return formatSiteTime(created, "YYYY-MM-DD") === today
    ? formatSiteTime(created, "HH:mm")
    : formatSiteTime(created, DATE_FORMAT);
});

const name = computed(() =>
  isGroup.value
    ? props.chat.title || "Групповая переписка"
    : props.interlocutor?.username || "Пользователь",
);

/** Written after the name as the header writes it: "SolohinLex (2)"; the row itself is not tinted. */
const unread = computed(() => props.chat.unreadMessagesCount ?? 0);
</script>

<template>
  <router-link :to="target" class="chat-preview">
    <AvatarImg
      :picture="isGroup ? null : interlocutor?.picture"
      :alt="name"
      :size="48"
      img-class="avatar"
    />

    <!-- The name and the time on one line, the last message under them. -->
    <div class="content">
      <span class="name-line"
        ><span class="name">{{ name }}</span
        ><span v-if="unread > 0" class="unread"
          >&nbsp;<span class="bracket">(</span
          ><span class="unread-number">{{ unread }}</span
          ><span class="bracket">)</span></span
        ></span
      >
      <span class="date">{{ lastMessageDate }}</span>
      <span class="message-preview">{{
        lastMessagePreview || "Нет сообщений"
      }}</span>
    </div>
  </router-link>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Tables" as *

// One fill for every row, read or not; the open conversation in the selection's shade.
.chat-preview
  display: flex
  align-items: center
  gap: $medium
  padding: $medium
  text-decoration: none
  background-color: $bg-element
  color: $text
  cursor: pointer
  +table-row-hover
  &.router-link-active
    background-image: linear-gradient($selected-overlay, $selected-overlay)
  // Rows stand flush, so the ring goes inward.
  &:focus-visible
    +focus-ring($inward: true)

// Square: a picture is never rounded.
.avatar
  display: block
  width: $grid-step * 12
  height: $grid-step * 12
  object-fit: cover
  flex-shrink: 0

// A name longer than the line wraps, and the time goes down whole under it.
.content
  flex: 1
  min-width: 0
  display: flex
  flex-wrap: wrap
  justify-content: space-between
  column-gap: $medium
  row-gap: $tiny
  align-items: baseline

// Text, not a flex row, so "SolohinLex (2)" copies as one line; min-width: 0
// lets a long name break as any word does.
.name-line
  min-width: 0

.name
  font-weight: bold
  color: $link

.unread
  white-space: nowrap

.bracket
  color: $text-muted

.unread-number
  font-weight: bold
  color: $accent-green

.date
  white-space: nowrap
  font-size: $secondary-font-size
  color: $text-muted

// The excerpt (its first 60 letters) on a line of its own, wrapping where narrower.
.message-preview
  flex-basis: 100%
  min-width: 0
  font-size: $secondary-font-size
  color: $text-muted
</style>

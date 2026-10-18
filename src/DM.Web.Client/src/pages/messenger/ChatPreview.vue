<script setup lang="ts">
/**
 * ChatPreview: a row of the list of conversations, laid out as a list of
 * chats of a messenger: the picture, the name with the count of unread and
 * the time of the last message on the first line, the last message under them.
 *
 * In the narrow list (`narrow`) the row keeps the picture and the count of
 * unread under it; the name is the link's own and the picture's tooltip.
 *
 * The row of the open conversation stands selected. The row opens its
 * conversation with the list kept on its page (listPage.ts).
 */
import { computed } from "vue";
import { useRoute } from "vue-router";
import type { Chat } from "@/entities/message";
import type { User } from "@/shared/api/models/community";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { Tooltip } from "@/shared/ui/Tooltip";
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
    /** The narrow list: the picture and the count of unread alone. */
    narrow?: boolean;
  }>(),
  { interlocutor: undefined, currentUser: null, narrow: false },
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
  // Strip HTML tags for preview. A drawn line break becomes a space first:
  // the preview is one run of text, and a message typed on two lines would
  // otherwise run its last word into the next line's first.
  const text = (msg.text || "")
    .replace(/<(?:br|hr)\s*\/?>/gi, " ")
    .replace(/<[^>]*>/g, "");
  const truncated = text.length > 60 ? text.slice(0, 60) + "..." : text;
  // Add prefix to indicate who sent the message
  const prefix = isLastMessageFromMe.value ? "Вы: " : "";
  return prefix + truncated;
});

/**
 * When the last message came, on the site's clock: the time for a message of
 * today, the date for an older one, so the time keeps to the first line
 * beside the name.
 */
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

/**
 * The unread messages of the conversation, written after the name as the
 * header writes its own: "SolohinLex (2)", brackets grey, the number bold
 * green. Nothing while there are none; the row itself is not tinted, so a
 * read and an unread conversation differ by the number alone.
 */
const unread = computed(() => props.chat.unreadMessagesCount ?? 0);

/** The narrow row's name for a reader of the screen. */
const narrowLabel = computed(() =>
  unread.value > 0 ? `${name.value} (${unread.value})` : name.value,
);
</script>

<template>
  <router-link
    v-if="narrow"
    :to="target"
    class="chat-preview chat-preview--narrow"
    :aria-label="narrowLabel"
  >
    <Tooltip :text="name" placement="left">
      <AvatarImg
        :picture="isGroup ? null : interlocutor?.picture"
        alt=""
        :size="48"
        img-class="avatar"
      />
    </Tooltip>
    <span v-if="unread > 0" class="unread" aria-hidden="true"
      ><span class="bracket">(</span
      ><span class="unread-number">{{ unread }}</span
      ><span class="bracket">)</span></span
    >
  </router-link>

  <router-link v-else :to="target" class="chat-preview">
    <AvatarImg
      :picture="isGroup ? null : interlocutor?.picture"
      :alt="name"
      :size="48"
      img-class="avatar"
    />

    <!-- In the order they stand: the name and the time of the last message
         on one line, the last message under them. -->
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

// One fill for every row, read or not; under the pointer the shade every row
// of a list or a table of the site takes, and the open conversation in the
// selection's shade.
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
    outline: 2px solid $link
    outline-offset: -2px

// The narrow row: the picture with 8 on either side, the count under it.
.chat-preview--narrow
  flex-direction: column
  align-items: flex-start
  gap: $tiny
  padding: $small

// Square: a picture is never rounded.
.avatar
  display: block
  width: $grid-step * 12
  height: $grid-step * 12
  object-fit: cover
  flex-shrink: 0

// The name with its number, and the time of the last message at the far end
// of its line; the last message under them across the whole width. A name
// longer than the line wraps, and the time goes down whole under it.
.content
  flex: 1
  min-width: 0
  display: flex
  flex-wrap: wrap
  justify-content: space-between
  column-gap: $medium
  row-gap: $tiny
  align-items: baseline

// A line of text and not a flex row, so the name, the space and the number
// copy as "SolohinLex (2)" in one line. The name is said whole: a name longer
// than the line breaks as any word of the site does (a flex item is never
// narrower than its longest word without min-width: 0).
.name-line
  min-width: 0

.name
  font-weight: bold
  color: $link

.unread
  white-space: nowrap

.chat-preview--narrow .unread
  font-size: $secondary-font-size

.bracket
  color: $text-muted

.unread-number
  font-weight: bold
  color: $accent-green

.date
  white-space: nowrap
  font-size: $secondary-font-size
  color: $text-muted

// The excerpt of the last message (its first 60 letters) is said whole, on a
// line of its own, and wraps where the row is narrower than it.
.message-preview
  flex-basis: 100%
  min-width: 0
  font-size: $secondary-font-size
  color: $text-muted
</style>

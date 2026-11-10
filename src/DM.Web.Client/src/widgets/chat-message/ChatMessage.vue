<script setup lang="ts">
/** One message renderer for both chats and the chat room of a game; the toolbar is the feed's. */
import { ref, computed, watch } from "vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { TruncatedContent } from "@/shared/ui/TruncatedContent";
import { ContentText } from "@/shared/ui/Content";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import type { BBCodeContext } from "@/shared/lib/bbcode/bbcodeTagSet";
import { vBbcodeInteractive } from "@/shared/directives";
import { trimHtmlWhitespace } from "@/shared/lib/utils/trimHtmlWhitespace";
import { formatChatTime, openImageGates } from "@/shared/lib/utils/chat";
import type { MessageWithContinuation } from "@/shared/lib/utils/chat";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { SvgIcon } from "@/shared/ui/Icon";
import { LikeBadge } from "@/shared/ui/LikeBadge";
import { DATE_TIME_FORMAT, formatSiteTime } from "@/shared/lib/utils/datetime";
import { REMOVED_ACCOUNT_NAME, removedAccountRoute } from "@/entities/user";

const props = withDefaults(
  defineProps<{
    message: MessageWithContinuation;
    compact?: boolean;
    /** The row is hovered: the time gutter shows. */
    hovered?: boolean;
    isOnline?: boolean;
    isLikedByMe?: boolean;
    canLike?: boolean;
    isModerator?: boolean;
    isEditing?: boolean;
    /** Seeds the edit box; the typed text goes back only with save-edit. */
    editText?: string;
    isDeletedExpanded?: boolean;
    maxHeight: number;
    /** The tag set of the edit box: "direct" in private messages, which render no [mod]. */
    context?: BBCodeContext;
  }>(),
  {
    compact: false,
    hovered: false,
    isOnline: false,
    isLikedByMe: false,
    canLike: false,
    isModerator: false,
    isEditing: false,
    editText: "",
    isDeletedExpanded: false,
    context: "message",
  },
);

const emit = defineEmits<{
  like: [];
  "toggle-deleted": [];
  "save-edit": [text: string];
  "cancel-edit": [];
}>();

const localEditText = ref(props.editText);

watch(
  () => props.editText,
  (v) => {
    localEditText.value = v;
  },
);

// Decided per layout, so a change of layout redraws the gated images already shown.
const shownText = computed(() =>
  props.compact ? props.message.text : openImageGates(props.message.text),
);

const messageHtml = computed(() => trimHtmlWhitespace(shownText.value));

// A removed account arrives as null and is signed as everywhere else, "Аккаунт удален".
const authorName = computed(
  () => props.message.author?.username ?? REMOVED_ACCOUNT_NAME,
);
const authorTo = computed(() =>
  props.message.author
    ? { name: "profile", params: { username: props.message.author.username } }
    : removedAccountRoute,
);

const continuationRow = computed(
  () => props.message.isContinuation && !props.compact,
);

const formattedTime = computed(() => formatChatTime(props.message.createdUtc));

const hasEdits = computed(() => (props.message.edits?.length ?? 0) > 0);

const fullDateTooltip = computed(() =>
  [
    `Отправлено: ${formatSiteTime(props.message.createdUtc, DATE_TIME_FORMAT)}`,
    ...(props.message.edits ?? []).map(
      (edit) =>
        `Редактирование: ${formatSiteTime(edit.modifiedUtc, DATE_TIME_FORMAT)}`,
    ),
  ].join("\n"),
);

const deletedDateTooltip = computed(() => {
  const msg = props.message;
  const deleter = msg.deletedBy?.username || "неизвестно";
  const removed = msg.deletedUtc
    ? `Удалено: ${formatSiteTime(msg.deletedUtc, DATE_TIME_FORMAT)} (${deleter})`
    : `Удалено (${deleter})`;
  return `${fullDateTooltip.value}\n${removed}`;
});

const likesCount = computed(() => props.message.likes?.length ?? 0);

function handleEditSubmit() {
  emit("save-edit", localEditText.value);
}

function handleEditKeydown(e: KeyboardEvent) {
  if (e.key === "Escape") emit("cancel-edit");
}
</script>

<template>
  <div class="chat-message" :class="{ compact, hovered }">
    <template v-if="message.isRemoved && !isDeletedExpanded">
      <div v-if="compact" class="msg-layout msg-layout-compact">
        <div class="msg-body">
          <div class="msg-header msg-header-compact">
            <Tooltip :text="deletedDateTooltip">
              <span class="msg-time-group">
                <span class="msg-time">{{ formattedTime }}</span>
                <span class="msg-icon-placeholder" />
              </span>
            </Tooltip>
            <span class="msg-gap">{{ " " }}</span>
            <button
              v-if="isModerator"
              type="button"
              class="msg-deleted-inline clickable"
              @click="emit('toggle-deleted')"
            >
              Сообщение удалено
            </button>
            <span v-else class="msg-deleted-inline">Сообщение удалено</span>
          </div>
        </div>
      </div>
      <div v-else class="msg-layout">
        <div class="msg-avatar-placeholder">
          <SvgIcon name="deletedAvatar" class="deleted-avatar" />
        </div>
        <button
          v-if="isModerator"
          type="button"
          class="msg-deleted clickable"
          @click="emit('toggle-deleted')"
        >
          <span class="msg-deleted-label">Сообщение удалено</span>
        </button>
        <div v-else class="msg-deleted">
          <span class="msg-deleted-label">Сообщение удалено</span>
        </div>
      </div>
    </template>

    <template v-else-if="message.isRemoved && isDeletedExpanded">
      <div class="msg-layout" :class="{ 'msg-layout-compact': compact }">
        <router-link v-if="!compact" :to="authorTo" class="msg-avatar-link">
          <AvatarImg
            :picture="message.author?.picture"
            :alt="authorName"
            :size="72"
            img-class="msg-avatar"
          />
        </router-link>
        <div class="msg-body">
          <!-- Wraps between its parts; "(скрыть)" stays one of them -->
          <div class="msg-header" :class="{ 'msg-header-compact': compact }">
            <template v-if="compact">
              <Tooltip :text="deletedDateTooltip">
                <span class="msg-time-group">
                  <span class="msg-time">{{ formattedTime }}</span>
                  <SvgIcon name="trash" class="msg-deleted-icon" />
                </span> </Tooltip
              ><span class="msg-gap">{{ " " }}</span
              ><router-link :to="authorTo" class="msg-author">{{
                authorName
              }}</router-link
              ><span class="msg-gap">{{ " " }}</span
              ><button
                type="button"
                class="msg-hide-link"
                @click="emit('toggle-deleted')"
              >
                (скрыть)
              </button>
            </template>
            <template v-else>
              <router-link :to="authorTo" class="msg-author">{{
                authorName
              }}</router-link
              ><span class="msg-gap">{{ " " }}</span
              ><Tooltip :text="deletedDateTooltip">
                <span class="msg-time-group">
                  <span class="msg-time">{{ formattedTime }}</span>
                  <SvgIcon name="trash" class="msg-deleted-icon" />
                </span> </Tooltip
              ><span class="msg-gap">{{ " " }}</span
              ><button
                type="button"
                class="msg-hide-link"
                @click="emit('toggle-deleted')"
              >
                (скрыть)
              </button>
            </template>
          </div>
          <div class="msg-content">
            <!-- ContentText binds its spoilers: this body appears long after the feed's init -->
            <ContentText class="msg-text" :html="shownText" />
          </div>
        </div>
      </div>
    </template>

    <div
      v-else
      class="msg-layout"
      :class="{
        'msg-continuation': continuationRow,
        'msg-layout-compact': compact,
      }"
    >
      <div v-if="continuationRow" class="msg-time-gutter">
        <Tooltip :text="fullDateTooltip">
          <span class="msg-time-group msg-time-hover">
            <span class="msg-time">{{ formattedTime }}</span>
            <SvgIcon v-if="hasEdits" name="pencil" class="msg-edited-icon" />
            <span v-else class="msg-icon-placeholder" />
          </span>
        </Tooltip>
      </div>
      <router-link v-else-if="!compact" :to="authorTo" class="msg-avatar-link">
        <AvatarImg
          :picture="message.author?.picture"
          :alt="authorName"
          :size="72"
          img-class="msg-avatar"
        />
      </router-link>
      <div class="msg-body">
        <!-- Wraps between the name and the time; nothing of it is cut or put away -->
        <div
          v-if="!continuationRow"
          class="msg-header"
          :class="{ 'msg-header-compact': compact }"
        >
          <template v-if="compact">
            <Tooltip :text="fullDateTooltip">
              <span class="msg-time-group">
                <span class="msg-time">{{ formattedTime }}</span>
                <SvgIcon
                  v-if="hasEdits"
                  name="pencil"
                  class="msg-edited-icon"
                />
                <span v-else class="msg-icon-placeholder" />
              </span> </Tooltip
            ><span class="msg-gap">{{ " " }}</span
            ><router-link
              :to="authorTo"
              class="msg-author"
              :class="{ online: isOnline }"
              >{{ authorName }}</router-link
            ><span v-if="likesCount > 0" class="msg-likes"
              ><LikeBadge
                :likes="message.likes ?? []"
                :liked="isLikedByMe"
                :interactive="canLike"
                @toggle="emit('like')"
            /></span>
          </template>
          <template v-else>
            <router-link
              :to="authorTo"
              class="msg-author"
              :class="{ online: isOnline }"
              >{{ authorName }}</router-link
            ><span class="msg-gap">{{ " " }}</span
            ><Tooltip :text="fullDateTooltip">
              <span class="msg-time-group">
                <span class="msg-time">{{ formattedTime }}</span>
                <SvgIcon
                  v-if="hasEdits"
                  name="pencil"
                  class="msg-edited-icon"
                />
              </span>
            </Tooltip>
          </template>
        </div>

        <template v-if="!isEditing">
          <TruncatedContent
            class="msg-content"
            :truncatable="true"
            :max-height="maxHeight"
            :watch-key="messageHtml"
          >
            <!-- Bound again by an edit, let go by removal, the edit box and the row leaving the feed. -->
            <div
              v-bbcode-interactive="messageHtml"
              class="msg-text bbcode-content"
              v-html="messageHtml"
            />
          </TruncatedContent>
          <div v-if="!compact && likesCount > 0" class="msg-reactions">
            <LikeBadge
              :likes="message.likes ?? []"
              :liked="isLikedByMe"
              :interactive="canLike"
              @toggle="emit('like')"
            />
          </div>
        </template>
        <div v-else class="msg-edit">
          <BBCodeEditor
            v-model="localEditText"
            :context="context"
            placeholder="Редактирование сообщения..."
            :min-height="60"
            :max-height="200"
            @submit="handleEditSubmit"
            @keydown="handleEditKeydown"
          />
          <div class="msg-edit-actions">
            <button
              type="button"
              class="msg-edit-btn msg-edit-save"
              @click="handleEditSubmit"
            >
              Сохранить
            </button>
            <button
              type="button"
              class="msg-edit-btn"
              @click="emit('cancel-edit')"
            >
              Отмена
            </button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Buttons" as *
@use "@/assets/styles/Links" as *
@use "@/widgets/chat-message/compactLayout" as *

// 72px: the chat is more spacious than comments.
$avatar-size: 72px
$icon-size: 16px

.msg-layout
  display: flex
  gap: $medium

.msg-layout-compact
  gap: 0

.msg-body
  flex: 1
  min-width: 0
  position: relative

// Inline flow, not flex: flex drops the space between name and time from a copy.
.msg-header
  margin-bottom: $tiny
  line-height: 1

// A real space, so a copy reads "Name 09:09"; the width keeps gutter + gap = content margin.
.msg-gap
  display: inline-block
  width: $small
  white-space: pre

.msg-avatar-link
  flex-shrink: 0
  align-self: flex-start

.msg-avatar
  width: $avatar-size
  height: $avatar-size
  object-fit: cover
  display: block

.msg-avatar-placeholder
  flex-shrink: 0
  width: $avatar-size
  height: $avatar-size

.deleted-avatar
  width: $avatar-size
  height: $avatar-size
  color: $border

.msg-author
  +muted-link
  &.online
    +link-tone($accent-green, $accent-green-hover)

.msg-time
  font-size: $secondary-font-size

// inline-block, not inline-flex: Chrome copies a flex box as a line of its own.
.msg-time-group
  display: inline-block
  color: $text-muted
  cursor: help

.msg-time-group > :not(:first-child)
  margin-left: $minor

.msg-time-group .msg-edited-icon,
.msg-time-group .msg-deleted-icon,
.msg-time-group .msg-icon-placeholder
  vertical-align: middle

.msg-edited-icon,
.msg-deleted-icon
  width: $icon-size
  height: $icon-size
  flex-shrink: 0
  color: $text-muted

.msg-icon-placeholder
  display: inline-block
  width: $icon-size
  height: $icon-size
  flex-shrink: 0

.msg-content
  color: $text
  line-height: 1.5

.msg-text
  :deep(img), :deep(.image)
    vertical-align: top

.msg-deleted
  display: flex
  align-items: center
  gap: $small
  padding: 0
  border: none
  background: none
  font: inherit
  color: inherit
  &.clickable
    cursor: pointer

.msg-deleted-label
  color: $text-muted
  font-style: italic

.msg-deleted-inline
  display: inline-flex
  align-items: center
  min-height: $icon-size
  padding: 0
  border: none
  background: none
  color: $text-muted
  font-family: inherit
  font-size: inherit
  font-style: italic
  &.clickable
    cursor: pointer
    &:hover
      text-decoration: underline

// Spaced from the time by the .msg-gap before it, which is copied with it.
.msg-hide-link
  +inline-link-button
  &
    font-size: $secondary-font-size

.msg-edit-actions
  display: flex
  justify-content: flex-start
  gap: $small
  margin-top: $small

.msg-edit-btn
  +button

// Saving the edit is the main action of the little form: the filled button.
.msg-edit-save
  +button-primary

// The site's "Нравится", in the size and muted colour of the message's time.
.msg-reactions
  display: flex
  align-items: center
  margin-top: $small
  color: $text-muted
  font-size: $secondary-font-size

// Compact: "Нравится" follows the author's name on the header line.
.msg-likes
  margin-left: $small
  color: $text-muted
  font-size: $secondary-font-size

.msg-continuation
  align-items: baseline

.msg-time-gutter
  width: $avatar-size
  flex-shrink: 0
  display: flex
  justify-content: center
  line-height: 1

.msg-time-hover
  opacity: 0
  transition: opacity $transition-fast

.chat-message.hovered
  .msg-time-gutter .msg-time-hover
    opacity: 1

+compact-layout
</style>

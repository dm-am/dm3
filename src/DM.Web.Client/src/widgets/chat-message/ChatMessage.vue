<script setup lang="ts">
/**
 * ChatMessage — unified message rendering for global chat and private messenger.
 *
 * Handles: full/compact layout, continuation, deleted, truncation, reactions, edit mode.
 * Does NOT handle: hover toolbar (managed by parent page), infinite scroll.
 */
import { ref, computed, watch } from "vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { TruncatedContent } from "@/shared/ui/TruncatedContent";
import { BBCodeEditor } from "@/features/editor";
import {
  initBbcodeInteractive,
  trimHtmlWhitespace,
} from "@/shared/lib/utils/bbcodeInteractive";
import {
  formatChatTime,
  getLikesTooltip as getLikesTooltipUtil,
} from "@/shared/lib/utils/chat";
import type { MessageWithContinuation } from "@/shared/lib/utils/chat";
import { AvatarImg } from "@/entities/user";
import { SvgIcon } from "@/shared/ui/Icon";
import dayjs from "dayjs";

const props = withDefaults(
  defineProps<{
    message: MessageWithContinuation;
    compact?: boolean;
    /** Parent wrapper is hovered — affects time gutter opacity */
    hovered?: boolean;
    isOnline?: boolean;
    isLikedByMe?: boolean;
    canEdit?: boolean;
    canDelete?: boolean;
    canLike?: boolean;
    isModerator?: boolean;
    isEditing?: boolean;
    editText?: string;
    isDeletedExpanded?: boolean;
    maxHeight?: number;
  }>(),
  {
    compact: false,
    hovered: false,
    isOnline: false,
    isLikedByMe: false,
    canEdit: false,
    canDelete: false,
    canLike: false,
    isModerator: false,
    isEditing: false,
    editText: "",
    isDeletedExpanded: false,
    maxHeight: 500,
  },
);

const emit = defineEmits<{
  like: [];
  "toggle-deleted": [];
  "start-edit": [];
  "save-edit": [text: string];
  "cancel-edit": [];
  "update:editText": [text: string];
}>();

// Local state
const localEditText = ref(props.editText);

watch(
  () => props.editText,
  (v) => {
    localEditText.value = v;
  },
);

// Pre-trimmed BBCode HTML. Pure computed, no DOM mutation.
// TruncatedContent handles overflow, collapsed media shrinkage, link.
const messageHtml = computed(() => trimHtmlWhitespace(props.message.text));

const formattedTime = computed(() => formatChatTime(props.message.createdUtc));

const hasEdits = computed(() => (props.message.edits?.length ?? 0) > 0);

const fullDateTooltip = computed(() => {
  let result = `Отправлено: ${dayjs(props.message.createdUtc).format("DD.MM.YYYY [в] HH:mm")}`;
  if (props.message.edits?.length) {
    for (const edit of props.message.edits) {
      result += `\nРедактирование: ${dayjs(edit.editedUtc).format("DD.MM.YYYY [в] HH:mm")}`;
    }
  }
  return result;
});

const deletedDateTooltip = computed(() => {
  let result = fullDateTooltip.value;
  const msg = props.message;
  const deleterName = msg.deletedBy?.username || "неизвестно";
  const deletedUtcStr = msg.deletedUtc
    ? dayjs(msg.deletedUtc).format("DD.MM.YYYY [в] HH:mm")
    : "";
  result += deletedUtcStr
    ? `\nУдалено: ${deletedUtcStr} (${deleterName})`
    : `\nУдалено (${deleterName})`;
  return result;
});

const likesTooltip = computed(() =>
  getLikesTooltipUtil(props.message.likes ?? []),
);
const likesCount = computed(() => props.message.likes?.length ?? 0);

const reactionAriaLabel = computed(() =>
  props.isLikedByMe
    ? `Убрать лайк, нравится ${likesCount.value}`
    : `Нравится ${likesCount.value}`,
);

function handleEditSubmit() {
  emit("save-edit", localEditText.value);
}

function handleEditKeydown(e: KeyboardEvent) {
  if (e.key === "Escape") emit("cancel-edit");
}

// Reinitialize interactive BBCode elements (spoilers, NSFW toggles) every
// time TruncatedContent remounts / replaces the content element.
function initMessageBbcode(el: HTMLElement) {
  initBbcodeInteractive(el);
}
</script>

<template>
  <div class="chat-message" :class="{ compact, hovered }">
    <!-- Deleted message (collapsed) -->
    <template v-if="message.isRemoved && !isDeletedExpanded">
      <div v-if="compact" class="msg-layout msg-layout-compact">
        <div class="msg-body">
          <div class="msg-header msg-header-compact">
            <Tooltip :text="deletedDateTooltip">
              <span class="msg-time-group">
                <span class="msg-icon-placeholder" /><span class="msg-time">{{
                  formattedTime
                }}</span>
              </span>
            </Tooltip>
            <span
              class="msg-deleted-inline"
              :class="{ clickable: isModerator }"
              @click="isModerator && $emit('toggle-deleted')"
              >Сообщение удалено</span
            >
          </div>
        </div>
      </div>
      <div v-else class="msg-layout">
        <div class="msg-avatar-placeholder">
          <SvgIcon name="deletedAvatar" class="deleted-avatar" />
        </div>
        <div
          class="msg-deleted"
          :class="{ clickable: isModerator }"
          @click="$emit('toggle-deleted')"
        >
          <span class="msg-deleted-label">Сообщение удалено</span>
        </div>
      </div>
    </template>

    <!-- Deleted message (expanded — show content) -->
    <template v-else-if="message.isRemoved && isDeletedExpanded">
      <div class="msg-layout" :class="{ 'msg-layout-compact': compact }">
        <router-link
          v-if="!compact"
          :to="{
            name: 'profile',
            params: { username: message.author.username },
          }"
          class="msg-avatar-link"
        >
          <AvatarImg
            :picture="message.author.picture"
            :alt="message.author.username"
            :size="72"
            img-class="msg-avatar"
          />
        </router-link>
        <div class="msg-body">
          <div class="msg-header" :class="{ 'msg-header-compact': compact }">
            <template v-if="compact">
              <Tooltip :text="deletedDateTooltip">
                <span class="msg-time-group">
                  <span class="msg-time">{{ formattedTime }}</span>
                  <SvgIcon name="trash" class="msg-deleted-icon" />
                </span>
              </Tooltip>
              <router-link
                :to="{
                  name: 'profile',
                  params: { username: message.author.username },
                }"
                class="msg-author"
                >{{ message.author.username }}</router-link
              >
              <a
                class="msg-hide-link"
                href="#"
                @click.prevent="$emit('toggle-deleted')"
                >(скрыть)</a
              >
            </template>
            <template v-else>
              <router-link
                :to="{
                  name: 'profile',
                  params: { username: message.author.username },
                }"
                class="msg-author"
                >{{ message.author.username }}</router-link
              >
              <Tooltip :text="deletedDateTooltip">
                <span class="msg-time-group">
                  <span class="msg-time">{{ formattedTime }}</span>
                  <SvgIcon name="trash" class="msg-deleted-icon" />
                </span>
              </Tooltip>
              <a
                class="msg-hide-link"
                href="#"
                @click.prevent="$emit('toggle-deleted')"
                >(скрыть)</a
              >
            </template>
          </div>
          <div class="msg-content">
            <div class="msg-text bbcode-content" v-html="message.text" />
          </div>
        </div>
      </div>
    </template>

    <!-- Normal message -->
    <template v-else>
      <!-- Continuation (same author, no avatar/name) -->
      <div
        v-if="message.isContinuation && !compact"
        class="msg-layout msg-continuation"
      >
        <div class="msg-time-gutter">
          <Tooltip :text="fullDateTooltip">
            <span class="msg-time-group msg-time-hover">
              <span class="msg-time">{{ formattedTime }}</span>
              <SvgIcon v-if="hasEdits" name="pencil" class="msg-edited-icon" />
              <span v-else class="msg-icon-placeholder" />
            </span>
          </Tooltip>
        </div>
        <div class="msg-body">
          <template v-if="!isEditing">
            <TruncatedContent
              class="msg-content"
              :truncatable="true"
              :max-height="maxHeight"
              :watch-key="messageHtml"
              :on-content-mounted="initMessageBbcode"
            >
              <div class="msg-text bbcode-content" v-html="messageHtml" />
            </TruncatedContent>
            <div v-if="!compact && likesCount > 0" class="msg-reactions">
              <Tooltip :text="likesTooltip">
                <button
                  v-if="canLike"
                  class="reaction-badge"
                  :class="{ 'my-reaction': isLikedByMe }"
                  :aria-label="reactionAriaLabel"
                  @click="$emit('like')"
                >
                  <SvgIcon name="heartEmpty" class="reaction-heart" />
                  <span class="reaction-count">{{ likesCount }}</span>
                </button>
                <span
                  v-else
                  class="reaction-badge reaction-badge-static"
                  :class="{ 'my-reaction': isLikedByMe }"
                  :aria-label="reactionAriaLabel"
                >
                  <SvgIcon name="heartEmpty" class="reaction-heart" />
                  <span class="reaction-count">{{ likesCount }}</span>
                </span>
              </Tooltip>
            </div>
          </template>
          <div v-else class="msg-edit">
            <BBCodeEditor
              v-model="localEditText"
              context="message"
              placeholder="Редактирование сообщения..."
              :min-height="60"
              :max-height="200"
              @submit="handleEditSubmit"
              @keydown="handleEditKeydown"
            />
            <div class="msg-edit-actions">
              <button
                type="button"
                class="msg-edit-btn"
                @click="$emit('cancel-edit')"
              >
                Отмена
              </button>
              <button
                type="button"
                class="msg-edit-btn"
                @click="handleEditSubmit"
              >
                Сохранить
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Full message (with avatar and header) -->
      <div v-else class="msg-layout" :class="{ 'msg-layout-compact': compact }">
        <router-link
          v-if="!compact"
          :to="{
            name: 'profile',
            params: { username: message.author.username },
          }"
          class="msg-avatar-link"
        >
          <AvatarImg
            :picture="message.author.picture"
            :alt="message.author.username"
            :size="72"
            img-class="msg-avatar"
          />
        </router-link>
        <div class="msg-body">
          <div class="msg-header" :class="{ 'msg-header-compact': compact }">
            <!-- Compact: time first, then name -->
            <template v-if="compact">
              <Tooltip :text="fullDateTooltip">
                <span class="msg-time-group">
                  <SvgIcon
                    v-if="hasEdits"
                    name="pencil"
                    class="msg-edited-icon"
                  />
                  <span v-else class="msg-icon-placeholder" />
                  <span class="msg-time">{{ formattedTime }}</span>
                </span>
              </Tooltip>
              <router-link
                :to="{
                  name: 'profile',
                  params: { username: message.author.username },
                }"
                class="msg-author"
                :class="{ online: isOnline }"
                >{{ message.author.username }}</router-link
              >
              <Tooltip v-if="likesCount > 0" :text="likesTooltip">
                <button
                  v-if="canLike"
                  class="msg-likes-inline"
                  :class="{ 'my-like': isLikedByMe }"
                  :aria-label="reactionAriaLabel"
                  @click="$emit('like')"
                >
                  <SvgIcon name="heartEmpty" />{{ likesCount }}
                </button>
                <span
                  v-else
                  class="msg-likes-inline msg-likes-inline-static"
                  :class="{ 'my-like': isLikedByMe }"
                  :aria-label="reactionAriaLabel"
                >
                  <SvgIcon name="heartEmpty" />{{ likesCount }}
                </span>
              </Tooltip>
            </template>
            <!-- Normal: name first, then time -->
            <template v-else>
              <router-link
                :to="{
                  name: 'profile',
                  params: { username: message.author.username },
                }"
                class="msg-author"
                :class="{ online: isOnline }"
                >{{ message.author.username }}</router-link
              >
              <Tooltip :text="fullDateTooltip">
                <span class="msg-time-group">
                  <span class="msg-time">{{ formattedTime }}</span>
                  <SvgIcon
                    v-if="hasEdits"
                    name="pencil"
                    class="msg-edited-icon"
                  />
                  <span v-else class="msg-icon-placeholder" />
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
              :on-content-mounted="initMessageBbcode"
            >
              <div class="msg-text bbcode-content" v-html="messageHtml" />
            </TruncatedContent>
            <div v-if="!compact && likesCount > 0" class="msg-reactions">
              <Tooltip :text="likesTooltip">
                <button
                  v-if="canLike"
                  class="reaction-badge"
                  :class="{ 'my-reaction': isLikedByMe }"
                  :aria-label="reactionAriaLabel"
                  @click="$emit('like')"
                >
                  <SvgIcon name="heartEmpty" class="reaction-heart" />
                  <span class="reaction-count">{{ likesCount }}</span>
                </button>
                <span
                  v-else
                  class="reaction-badge reaction-badge-static"
                  :class="{ 'my-reaction': isLikedByMe }"
                  :aria-label="reactionAriaLabel"
                >
                  <SvgIcon name="heartEmpty" class="reaction-heart" />
                  <span class="reaction-count">{{ likesCount }}</span>
                </span>
              </Tooltip>
            </div>
          </template>
          <div v-else class="msg-edit">
            <BBCodeEditor
              v-model="localEditText"
              context="message"
              placeholder="Редактирование сообщения..."
              :min-height="60"
              :max-height="200"
              @submit="handleEditSubmit"
              @keydown="handleEditKeydown"
            />
            <div class="msg-edit-actions">
              <button
                type="button"
                class="msg-edit-btn"
                @click="$emit('cancel-edit')"
              >
                Отмена
              </button>
              <button
                type="button"
                class="msg-edit-btn"
                @click="handleEditSubmit"
              >
                Сохранить
              </button>
            </div>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"

// ============================================================================
// ChatMessage — shared message styles for global chat and messenger
// ============================================================================

.msg-layout
  display: flex
  gap: $medium

.msg-layout-compact
  gap: 0

.msg-body
  flex: 1
  min-width: 0
  position: relative

.msg-header
  display: flex
  align-items: center
  gap: $small
  margin-bottom: $tiny
  line-height: 1

.msg-header-compact
  display: inline-flex
  align-items: center
  gap: $small
  margin-bottom: 0
  line-height: 1

.msg-avatar-link
  flex-shrink: 0
  align-self: flex-start

// Full-layout avatar: 72px — chat is more spacious than comments (which use 56).
.msg-avatar
  width: 72px
  height: 72px
  object-fit: cover
  display: block

.msg-avatar-placeholder
  flex-shrink: 0
  width: 72px
  height: 72px

.deleted-avatar
  width: 72px
  height: 72px
  color: $border

.msg-author
  text-decoration: none
  font-weight: 500
  color: $text-muted
  &:hover
    color: $link-nav-hover
    text-decoration: underline
  &.online
    color: $accent-green
    &:hover
      color: $accent-green-hover
      text-decoration: underline

.msg-time
  font-size: $secondary-font-size

.msg-time-group
  display: inline-flex
  align-items: center
  gap: 6px
  color: $text-muted
  cursor: help

.msg-edited-icon,
.msg-deleted-icon
  width: 16px
  height: 16px
  flex-shrink: 0
  color: $text-muted

.msg-icon-placeholder
  display: inline-block
  width: 16px
  height: 16px
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
  &.clickable
    cursor: pointer

.msg-deleted-label
  color: $text-muted
  font-style: italic

.msg-deleted-inline
  display: inline-flex
  align-items: center
  min-height: 16px
  color: $text-muted
  font-style: italic
  &.clickable
    cursor: pointer
    &:hover
      text-decoration: underline

.msg-hide-link
  color: $link
  font-size: $secondary-font-size
  margin-left: $small
  &:hover
    color: $link-hover

.msg-edit
  margin-top: 0

.msg-edit-actions
  display: flex
  justify-content: flex-start
  gap: $small
  margin-top: $small

.msg-edit-btn
  +button

.msg-reactions
  display: flex
  align-items: center
  gap: $tiny
  margin-top: $small

.reaction-badge
  display: inline-flex
  align-items: center
  gap: 6px
  padding: 6px 10px
  border: none
  background-color: $hover-overlay
  color: $text-muted
  cursor: pointer
  transition: transform 0.1s ease
  border-radius: $border-radius
  svg
    width: 22px
    height: 22px
    fill: none
    transition: transform 0.15s ease
  &:hover
    background-color: $active-overlay
    svg
      filter: brightness($hover-brightness)
      transform: scale(1.15)
  &:active
    background-color: $hover-overlay
    transform: scale(0.95)
  &.my-reaction
    svg
      fill: currentColor

// Non-interactive variant (guests / message author): count only, no actions
.reaction-badge-static
  cursor: default
  &:hover
    background-color: $hover-overlay
    svg
      filter: none
      transform: none
  &:active
    transform: none

.reaction-count
  font-size: $secondary-font-size

.msg-likes-inline
  display: inline-flex
  align-items: center
  gap: 4px
  margin-left: $small
  padding: 0
  border: none
  background: transparent
  color: $text-muted
  cursor: pointer
  font-size: $secondary-font-size
  svg
    width: 16px
    height: 16px
  &.my-like
    svg
      fill: currentColor
  &:hover
    filter: brightness($hover-brightness)

// Non-interactive variant (guests / message author): count only, no actions
.msg-likes-inline-static
  cursor: default
  &:hover
    filter: none

// Continuation — time gutter
.msg-continuation
  align-items: baseline

.msg-time-gutter
  width: 72px
  flex-shrink: 0
  display: flex
  justify-content: center
  line-height: 1

.msg-time-hover
  opacity: 0
  transition: opacity 0.1s ease

// ============================================================================
// Hover state — controlled by parent via `hovered` prop
// ============================================================================
.chat-message.hovered
  .msg-time-gutter .msg-time-hover
    opacity: 1

// ============================================================================
// Compact layout — controlled by `compact` prop
// ============================================================================
// Compact: fixed time gutter so author names and content align across rows.
// Gutter holds the (optional) edit/trash icon + the time, right-aligned.
$compact-time-gutter: 62px

.chat-message.compact
  .msg-layout-compact
    display: block
  .msg-body
    display: block
  // Single-row header: time gutter + author + likes share one baseline
  .msg-header-compact
    display: flex
    align-items: baseline
    gap: $small
    margin-bottom: 0
    line-height: 1.4
  // Baseline (not center) so the time text sits on the same line as the
  // username — both anchor to the header's shared baseline. The optional
  // edit/trash icon stays vertically centered against the time text.
  .msg-time-group
    flex-shrink: 0
    display: inline-flex
    align-items: baseline
    justify-content: flex-end
    gap: 6px
    width: $compact-time-gutter
  .msg-time-group .msg-edited-icon,
  .msg-time-group .msg-icon-placeholder
    align-self: center
  .msg-author
    display: inline-flex
    align-items: baseline
  .msg-content
    display: block
    margin-left: calc(#{$compact-time-gutter} + #{$small})
    margin-top: 0
  .msg-edit
    margin-left: calc(#{$compact-time-gutter} + #{$small})
  .msg-text
    display: block
  .msg-text :deep(.bb-quote),
  .msg-text :deep(.bb-mod),
  .msg-text :deep(.bb-warning),
  .msg-text :deep(.bb-private)
    display: block !important
    margin: 0 !important
    padding: 0
    line-height: 1.4
  .msg-text :deep(ul),
  .msg-text :deep(ol)
    display: block !important
    margin: 0 !important
    padding-left: $big
    line-height: 1.4
  .msg-reactions
    display: block
    margin-top: $tiny
  .msg-deleted-inline
    display: inline-flex
    align-items: center
    min-height: 16px
    color: $text-muted
    font-style: italic
    &.clickable
      cursor: pointer
      &:hover
        text-decoration: underline
</style>

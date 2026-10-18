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
import { ContentText } from "@/shared/ui/Content";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import type { BBCodeContext } from "@/shared/lib/utils/bbcode";
import {
  initBbcodeInteractive,
  trimHtmlWhitespace,
} from "@/shared/lib/utils/bbcodeInteractive";
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
    /** Parent wrapper is hovered — affects time gutter opacity and the
     * surface the like badge's ring is drawn in */
    hovered?: boolean;
    isOnline?: boolean;
    isLikedByMe?: boolean;
    canEdit?: boolean;
    canDelete?: boolean;
    canLike?: boolean;
    isModerator?: boolean;
    isEditing?: boolean;
    /** Initial BBCode to seed the editor with when entering edit mode. Only
     * consumed once (via the watch below) — further edits live in
     * `localEditText` and travel back to the parent solely through the
     * `save-edit` payload, never synced back into this prop. */
    editText?: string;
    isDeletedExpanded?: boolean;
    maxHeight?: number;
    /** The tag set of the edit box: "direct" in private messages, which do
     * not render [mod], "message" in the global chat. */
    context?: BBCodeContext;
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
    context: "message",
  },
);

// Deleted-message reveal (moderator): a deliberate moderation action, NOT
// collapsible content — a plain instant swap outside the reveal contract
// (owner rule: bulk "Развернуть все" must not uncover removed content).
const emit = defineEmits<{
  like: [];
  "toggle-deleted": [];
  "start-edit": [];
  "save-edit": [text: string];
  "cancel-edit": [];
}>();

// Local state
const localEditText = ref(props.editText);

watch(
  () => props.editText,
  (v) => {
    localEditText.value = v;
  },
);

// An image of the global chat stands behind a spoiler of the server's only in
// the compact layout; the full one shows it in place. Decided here, by the
// layout this message is drawn in, so a change of layout redraws the messages
// already shown (see openImageGates).
const shownText = computed(() =>
  props.compact ? props.message.text : openImageGates(props.message.text),
);

// Pre-trimmed BBCode HTML. Pure computed, no DOM mutation.
// TruncatedContent handles overflow, collapsed media shrinkage, link.
const messageHtml = computed(() => trimHtmlWhitespace(shownText.value));

// The author of a message whose account was removed since arrives as null:
// the server hides the account and keeps what it wrote. Such a message is
// signed the way every other surface signs it, "Аккаунт удален", and the name
// leads to the profile of that name, which gathers what removed accounts left.
const author = computed(() => props.message.author ?? null);
const authorName = computed(
  () => author.value?.username ?? REMOVED_ACCOUNT_NAME,
);
const authorTo = computed(() =>
  author.value
    ? { name: "profile", params: { username: author.value.username } }
    : removedAccountRoute,
);

const formattedTime = computed(() => formatChatTime(props.message.createdUtc));

const hasEdits = computed(() => (props.message.edits?.length ?? 0) > 0);

const fullDateTooltip = computed(() => {
  let result = `Отправлено: ${formatSiteTime(props.message.createdUtc, DATE_TIME_FORMAT)}`;
  if (props.message.edits?.length) {
    for (const edit of props.message.edits) {
      result += `\nРедактирование: ${formatSiteTime(edit.modifiedUtc, DATE_TIME_FORMAT)}`;
    }
  }
  return result;
});

const deletedDateTooltip = computed(() => {
  let result = fullDateTooltip.value;
  const msg = props.message;
  const deleterName = msg.deletedBy?.username || "неизвестно";
  const deletedUtcStr = msg.deletedUtc
    ? formatSiteTime(msg.deletedUtc, DATE_TIME_FORMAT)
    : "";
  result += deletedUtcStr
    ? `\nУдалено: ${deletedUtcStr} (${deleterName})`
    : `\nУдалено (${deleterName})`;
  return result;
});

const likesCount = computed(() => props.message.likes?.length ?? 0);

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
                <span class="msg-time">{{ formattedTime }}</span>
                <span class="msg-icon-placeholder" />
              </span>
            </Tooltip>
            <span class="msg-gap">{{ " " }}</span>
            <button
              v-if="isModerator"
              type="button"
              class="msg-deleted-inline clickable"
              @click="$emit('toggle-deleted')"
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
          @click="$emit('toggle-deleted')"
        >
          <span class="msg-deleted-label">Сообщение удалено</span>
        </button>
        <div v-else class="msg-deleted">
          <span class="msg-deleted-label">Сообщение удалено</span>
        </div>
      </div>
    </template>

    <!-- Deleted message (expanded — show content) -->
    <template v-else-if="message.isRemoved && isDeletedExpanded">
      <div class="msg-layout" :class="{ 'msg-layout-compact': compact }">
        <router-link v-if="!compact" :to="authorTo" class="msg-avatar-link">
          <AvatarImg
            :picture="author?.picture"
            :alt="authorName"
            :size="72"
            img-class="msg-avatar"
          />
        </router-link>
        <div class="msg-body">
          <!-- A line of text: where it has no room, it wraps between its
               parts, and "(скрыть)" stays a part of it. -->
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
                @click="$emit('toggle-deleted')"
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
                @click="$emit('toggle-deleted')"
              >
                (скрыть)
              </button>
            </template>
          </div>
          <div class="msg-content">
            <!-- The SSOT component, not a hand-rolled div: this body appears on
                 "показать удаленное", long after the container-wide interactive
                 init ran, so spoilers inside it would never bind. -->
            <ContentText class="msg-text" :html="shownText" />
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
              <LikeBadge
                :likes="message.likes ?? []"
                :liked="isLikedByMe"
                :interactive="canLike"
                @toggle="$emit('like')"
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
                @click="$emit('cancel-edit')"
              >
                Отмена
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Full message (with avatar and header) -->
      <div v-else class="msg-layout" :class="{ 'msg-layout-compact': compact }">
        <router-link v-if="!compact" :to="authorTo" class="msg-avatar-link">
          <AvatarImg
            :picture="author?.picture"
            :alt="authorName"
            :size="72"
            img-class="msg-avatar"
          />
        </router-link>
        <div class="msg-body">
          <!-- A line of text: where it has no room, it wraps between the name
               and the time, and a name longer than the line breaks as any
               word of the site does. Nothing of it is cut or put away. -->
          <div class="msg-header" :class="{ 'msg-header-compact': compact }">
            <!-- Compact: time first, then name -->
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
                  @toggle="$emit('like')"
              /></span>
            </template>
            <!-- Normal: name first, then time. Nothing stands after the time
                 unless the message was edited. -->
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
              :on-content-mounted="initMessageBbcode"
            >
              <div class="msg-text bbcode-content" v-html="messageHtml" />
            </TruncatedContent>
            <div v-if="!compact && likesCount > 0" class="msg-reactions">
              <LikeBadge
                :likes="message.likes ?? []"
                :liked="isLikedByMe"
                :interactive="canLike"
                @toggle="$emit('like')"
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
                @click="$emit('cancel-edit')"
              >
                Отмена
              </button>
            </div>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

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

// Inline flow (deliberately NOT flex): the author name and the time then
// share the line's text baseline naturally, and the .msg-gap space between
// them survives selection copy — flex containers drop bare whitespace text
// nodes from the clipboard, which used to glue "Name09:09" together.
.msg-header
  margin-bottom: $tiny
  line-height: 1

.msg-header-compact
  margin-bottom: 0

// A real rendered space between header items ({{ " " }} inside, kept visible
// by white-space: pre) so copied text reads "Name 09:09 ..." like prose; the
// fixed width preserves the $small rhythm the old flex gap provided and keeps
// the compact gutter math exact (gutter + gap = content margin).
.msg-gap
  display: inline-block
  width: $small
  white-space: pre

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

// A grey link, and a green one while its author is online: each answers the
// pointer the way every link of its colour on the site does.
.msg-author
  +muted-link
  &.online
    +link-tone($accent-green, $accent-green-hover)

.msg-time
  font-size: $secondary-font-size

// inline-block, NOT inline-flex: Chrome serializes selection text through
// the layout tree and treats (inline-)flex containers as block boundaries,
// which put the time on its own copied line. An inline-block box keeps the
// time text in the header's inline flow (copy stays "Name 09:09"), gives it
// the line's shared text baseline, and still allows the fixed-width
// right-aligned gutter in compact mode. The atomic box also keeps the
// time and its icon from ever wrapping apart.
.msg-time-group
  display: inline-block
  color: $text-muted
  cursor: help

// Inline-flow replacement for the old flex gap between the time text and
// the optional edit/trash icon (works for either order — icon-first in
// compact, time-first in full).
.msg-time-group > :not(:first-child)
  margin-left: 6px

.msg-time-group .msg-edited-icon,
.msg-time-group .msg-deleted-icon,
.msg-time-group .msg-icon-placeholder
  vertical-align: middle

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
  min-height: 16px
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

.msg-edit
  margin-top: 0

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

// The like of a message is the site's badge (shared/ui/LikeBadge), drawn in
// the size of the message's own meta text, the time. It takes the colour of
// the muted text around it in every state: no red, nothing under the pointer.
.msg-reactions
  display: flex
  align-items: center
  margin-top: $small
  color: $text-muted
  font-size: $secondary-font-size

// Compact: the badge follows the author's name on the header line.
.msg-likes
  margin-left: $small
  color: $text-muted
  font-size: $secondary-font-size

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

// The ring round the like badge's plate is the colour of the surface under it.
// The chats lay a message on $bg-page and lighten its row to $bg-element while
// it is hovered or focused, which is what `hovered` reports.
.chat-message
  --like-badge-surface: #{$bg-page}

  &.hovered
    --like-badge-surface: #{$bg-element}

// ============================================================================
// Compact layout — controlled by `compact` prop
// ============================================================================
// Compact: fixed time gutter so author names and content align across rows.
// Its content is left-aligned and leads with the time, the icon following it
// as in the full layout, so the row's first visible character sits exactly
// where the full layout starts its avatar instead of behind an empty run.
// 80px so that gutter + .msg-gap ($small) = 88px — exactly the full
// layout's avatar column (72px avatar + $medium gap): with matching page
// paddings the compact content starts on the same x as the full content.
$compact-time-gutter: 80px

.chat-message.compact
  .msg-layout-compact
    display: block
  .msg-body
    display: block
  // Single-row header (inline flow): time gutter + author + likes share the
  // line's text baseline; baseline mechanics live in the base .msg-header /
  // .msg-time-group rules.
  .msg-header-compact
    margin-bottom: 0
    line-height: 1.4
  // Fixed-width time gutter so author names and content align across rows:
  // gutter + .msg-gap = the content margin below.
  .msg-time-group
    width: $compact-time-gutter
    text-align: left
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

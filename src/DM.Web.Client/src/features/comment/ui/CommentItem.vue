<script setup lang="ts">
import { ref, computed, watch } from "vue";
import { htmlToBbcode } from "@/shared/lib/utils/bbcode";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import type {
  ApiResult,
  Envelope,
  GeneralError,
  QuoteSource,
} from "@/shared/api/models/common";
import type { Comment } from "@/shared/api/models/common/comment";
import { unwrapResource } from "@/shared/api";
import { useAuthStore, userIsModerator } from "@/entities/user";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { getRoleBadge } from "@/shared/config/roles";
import { commentPermalink } from "../model/permalink";
import { Tooltip } from "@/shared/ui/Tooltip";
import { TruncatedContent } from "@/shared/ui/TruncatedContent";
import dayjs from "dayjs";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import {
  initBbcodeInteractive,
  trimHtmlWhitespace,
} from "@/shared/lib/utils/bbcodeInteractive";
import { highlightDom, clearDomHighlight } from "@/shared/lib/utils/highlight";
import { SvgIcon } from "@/shared/ui/Icon";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { ONLINE_THRESHOLD_MINUTES } from "@/shared/lib/constants/user";
import { useToast } from "@/shared/lib/composables/useToast";
import { useQuoteAction } from "@/shared/lib/composables/useQuoteComposer";
import { notifyFailure } from "@/shared/lib/errors";
import { getLikesTooltip } from "@/shared/lib/utils/chat";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";

/** What a mutation answers with: null when the server took the change. */
type SubmitResult = { error: GeneralError | null };

const props = withDefaults(
  defineProps<{
    comment: Comment;
    compact?: boolean;
    number?: number;
    /** Search query for highlighting matches in comment text */
    searchQuery?: string;
    /**
     * Fetches the comment's raw BBCode source for the editor (the displayed
     * text is server-rendered HTML and must never be edited directly).
     * Domain-specific — each page passes its API's ...ForEdit method
     * (forum/blog/game). Required: without a source fetch there is no safe
     * way to edit.
     */
    fetchEditSource: (id: string) => Promise<ApiResult<Envelope<Comment>>>;
    /**
     * Fetches the markup of a quotation of this comment. Domain-specific for
     * the same reason the source fetch is — four surfaces, four endpoints —
     * and optional, because a discussion whose page has no composer to answer
     * in shows no Quote button either.
     */
    fetchQuoteSource?: (
      id: string,
    ) => Promise<ApiResult<Envelope<QuoteSource>>>;
    /**
     * Saves the edited BBCode and answers whether the server took it. A
     * function rather than an event, because only the answer may close the
     * editor: an event has no result to wait for, and closing on the emit is
     * what threw a rejected edit away and left the reader in front of the old
     * text with nothing said.
     */
    submitEdit: (id: string, text: string) => Promise<SubmitResult>;
    /**
     * Deletes the comment. A function for the same reason: only the answer
     * tells this item whether the deleted-comment placeholder is true.
     */
    submitDelete: (id: string) => Promise<SubmitResult>;
  }>(),
  {
    compact: true,
  },
);

const emit = defineEmits<{
  like: [id: string];
  unlike: [id: string];
  warn: [id: string];
}>();

const EDIT_TIME_LIMIT_MINUTES = 15;

const route = useRoute();
const { success: toastSuccess, error: toastError } = useToast();
const { user: currentUser } = storeToRefs(useAuthStore());

// State
const isEditing = ref(false);
const editText = ref("");

// Max collapsed height before TruncatedContent shows "показать полностью".
// 300px ≈ 15-20 lines of BBCode text, matches DM2 comment visual rhythm.
const COMMENT_MAX_HEIGHT = 300;

const formattedDate = computed(() =>
  props.comment.createdUtc ? formatDateFull(props.comment.createdUtc) : "",
);

const formattedEditDate = computed(() =>
  props.comment.modifiedUtc ? formatDateFull(props.comment.modifiedUtc) : "",
);

const isEdited = computed(() => !!props.comment.modifiedUtc);

const isModerator = computed(() => userIsModerator(currentUser.value));

const isAuthor = computed(
  () => currentUser.value?.username === props.comment.author?.username,
);

const canEdit = computed(() => {
  if (!currentUser.value) return false;
  if (props.comment.isRemoved) return false;
  if (isModerator.value) return true;
  if (!isAuthor.value) return false;

  const timeSinceCreation = dayjs().diff(
    dayjs(props.comment.createdUtc),
    "minute",
    true,
  );
  return timeSinceCreation <= EDIT_TIME_LIMIT_MINUTES;
});

const canDelete = computed(() => canEdit.value);

const canWarn = computed(() => isModerator.value);

// Quoting. Two conditions, and both have to hold: the page has to have handed
// down a composer to answer in, and it has to have handed down the fetch for
// this surface's endpoint.
const { canQuote: composerAcceptsQuotes, quote } = useQuoteAction();
const canQuote = computed(
  () => composerAcceptsQuotes.value && !!props.fetchQuoteSource,
);

function quoteComment() {
  const fetchQuoteSource = props.fetchQuoteSource;
  if (!fetchQuoteSource) return;
  return quote(() => fetchQuoteSource(props.comment.id));
}

const isLikedByMe = computed(() => {
  if (!currentUser.value) return false;
  return props.comment.likes?.some(
    (u) => u.username === currentUser.value?.username,
  );
});

const canLike = computed(() => {
  if (!currentUser.value) return false;
  return !isAuthor.value;
});

const likesCount = computed(() => props.comment.likes?.length ?? 0);

const likersTooltip = computed(() =>
  getLikesTooltip(props.comment.likes ?? []),
);

// Whether the footer has any content. In the full layout the author meta and
// permalink live in the header, so a guest viewing a like-less comment would
// otherwise get an empty footer with stray spacing. In compact layout the
// footer always carries the author-info line (rendered even for a deleted
// author, see the "[удален]" branch below), so it is never empty there.
const hasFooterContent = computed(
  () =>
    props.compact ||
    canLike.value ||
    likesCount.value > 0 ||
    canEdit.value ||
    canDelete.value ||
    canWarn.value,
);

const isAuthorOnline = computed(() => {
  const lastActivityUtc = props.comment.author?.lastActivityUtc;
  if (!lastActivityUtc) return false;
  return (
    dayjs().diff(dayjs(lastActivityUtc), "minute", true) <=
    ONLINE_THRESHOLD_MINUTES
  );
});

const roleBadge = computed(() => getRoleBadge(props.comment.author?.role));

// Comment rendered HTML, pre-trimmed of leading/trailing whitespace so
// phantom empty lines never eat the truncation budget. Pure transform,
// no DOM mutation. TruncatedContent handles overflow detection, height-
// based truncation, collapsed-state media shrinkage, and the expand link.
const commentHtml = computed(() => trimHtmlWhitespace(props.comment.text));

// Methods
const editLoading = ref(false);

async function startEdit() {
  if (editLoading.value || isEditing.value) return;
  // comment.text is Display-audience server-rendered HTML; the editor needs
  // the raw BBCode source, which only the AuthorEdit audience returns (same
  // fetch-before-edit idiom as TopicView and the chat message editors).
  editLoading.value = true;
  const { data, error } = await props.fetchEditSource(props.comment.id);
  editLoading.value = false;
  if (error) {
    toastError("Не удалось загрузить текст комментария");
    return;
  }
  editText.value = htmlToBbcode(unwrapResource<Comment>(data)?.text ?? "");
  isEditing.value = true;
}

function cancelEdit() {
  isEditing.value = false;
  editText.value = "";
}

const saving = ref(false);

async function saveEdit() {
  if (!editText.value.trim() || saving.value) return;
  saving.value = true;
  const { error } = await props.submitEdit(props.comment.id, editText.value);
  saving.value = false;
  if (error) {
    notifyFailure(error, "Не удалось сохранить комментарий");
    return;
  }
  isEditing.value = false;
  editText.value = "";
}

function handleEditKeydown(e: KeyboardEvent) {
  // Ctrl+Enter to save is handled by BBCodeEditor's own "submit" emit; here
  // we only add Escape-to-cancel to match the create editor's shortcuts.
  if (e.key === "Escape") {
    cancelEdit();
  }
}

function toggleLike() {
  if (isLikedByMe.value) {
    emit("unlike", props.comment.id);
  } else {
    emit("like", props.comment.id);
  }
}

const deleting = ref(false);
const showDeleteConfirm = ref(false);

// "Удалить" sits at a $small step from "Редактировать" and "Предупреждение", and
// it used to fire on the first click with nothing to undo it. The post of a game
// and a forum topic both ask first, through this same dialog.
function requestDelete() {
  showDeleteConfirm.value = true;
}

async function handleDelete() {
  if (deleting.value) return;
  deleting.value = true;
  const { error } = await props.submitDelete(props.comment.id);
  deleting.value = false;
  showDeleteConfirm.value = false;
  if (error) notifyFailure(error, "Не удалось удалить комментарий");
}

function handleWarn() {
  emit("warn", props.comment.id);
}

async function copyAnchorLink() {
  const url = commentPermalink(props.comment.id, route.query.number);
  try {
    await navigator.clipboard.writeText(url);
    toastSuccess("Ссылка скопирована");
  } catch {
    toastError("Не удалось скопировать ссылку");
  }
}

// Track the mounted content element for re-highlighting on searchQuery change
const contentEl = ref<HTMLElement | null>(null);

// Reinitialize interactive BBCode elements (spoilers, NSFW toggles) each
// time TruncatedContent mounts / refreshes the content element.
function initCommentBbcode(el: HTMLElement) {
  contentEl.value = el;
  initBbcodeInteractive(el);
  clearDomHighlight(el);
  if (props.searchQuery) {
    highlightDom(el, props.searchQuery);
  }
}

// Re-highlight when searchQuery changes after initial mount
watch(
  () => props.searchQuery,
  (query) => {
    if (!contentEl.value) return;
    clearDomHighlight(contentEl.value);
    if (query) {
      highlightDom(contentEl.value, query);
    }
  },
);
</script>

<template>
  <div
    :id="`comment-${comment.id}`"
    class="comment"
    :class="{
      removed: comment.isRemoved,
      compact: compact,
    }"
  >
    <!-- Deleted comment placeholder. isRemoved is set locally when this
         session deletes the comment; the server never sends a removed one,
         so this is what keeps the list from jumping under the reader. -->
    <template v-if="comment.isRemoved">
      <div class="deleted-placeholder">
        <span class="deleted-text">Комментарий удален</span>
      </div>
    </template>

    <!-- Normal comment content -->
    <template v-else>
      <!-- Avatar (full layout only), top-aligned fixed column -->
      <router-link
        v-if="!compact && comment.author"
        :to="{ name: 'profile', params: { username: comment.author.username } }"
        class="avatar-link"
      >
        <AvatarImg
          :picture="comment.author.picture"
          :alt="comment.author.username"
          :size="72"
          img-class="avatar"
        />
      </router-link>

      <div class="comment-body">
        <!-- Header (full layout only): author block left, permalink top-right -->
        <div v-if="!compact" class="comment-header">
          <span class="author-block">
            <span class="author-line">
              <router-link
                v-if="comment.author"
                :to="{
                  name: 'profile',
                  params: { username: comment.author.username },
                }"
                class="author-name"
                >{{ comment.author.username }}</router-link
              ><span v-else class="author-deleted">[удален]</span
              ><template v-if="roleBadge">
                <Tooltip :text="roleBadge.label"
                  ><b class="role-letter">[{{ roleBadge.letter }}]</b></Tooltip
                ></template
              ><template v-if="comment.author">
                [<span :class="isAuthorOnline ? 'online' : 'offline'">{{
                  isAuthorOnline ? "online" : "offline"
                }}</span
                >]</template
              >
            </span>
            <span class="comment-meta"
              >{{ formattedDate
              }}<template v-if="isEdited">
                | Отредактировано {{ formattedEditDate }}</template
              ></span
            >
          </span>

          <!-- Permalink number, pinned to the top-right corner (no '#'). -->
          <Tooltip
            v-if="number"
            :text="`Скопировать ссылку на комментарий ${number}`"
          >
            <button
              class="comment-number"
              :aria-label="`Скопировать ссылку на комментарий ${number}`"
              @click="copyAnchorLink"
            >
              {{ number }}
            </button>
          </Tooltip>
        </div>

        <!-- Edit mode -->
        <template v-if="isEditing">
          <div class="edit-container">
            <BBCodeEditor
              v-model="editText"
              context="common"
              placeholder="Редактирование комментария..."
              :min-height="100"
              :max-height="300"
              :is-moderator="isModerator"
              @submit="saveEdit"
              @keydown="handleEditKeydown"
            />
            <div class="edit-actions">
              <button class="action-btn save-btn" @click="saveEdit">
                Сохранить
              </button>
              <button class="action-btn cancel-btn" @click="cancelEdit">
                Отмена
              </button>
            </div>
          </div>
        </template>

        <!-- View mode -->
        <template v-else>
          <TruncatedContent
            :truncatable="true"
            :max-height="COMMENT_MAX_HEIGHT"
            :watch-key="commentHtml"
            :on-content-mounted="initCommentBbcode"
          >
            <div class="comment-text bbcode-content" v-html="commentHtml" />
          </TruncatedContent>
        </template>

        <!-- Footer: Author info (compact only) + Actions + Number (compact) -->
        <div v-if="hasFooterContent" class="comment-footer">
          <span v-if="compact" class="author-info"
            >Автор:
            <router-link
              v-if="comment.author"
              :to="{
                name: 'profile',
                params: { username: comment.author.username },
              }"
              class="author-link"
              >{{ comment.author.username }}</router-link
            ><span v-else class="author-deleted">[удален]</span
            ><template v-if="roleBadge">
              [<Tooltip :text="roleBadge.label"
                ><b class="role-letter">{{ roleBadge.letter }}</b></Tooltip
              >]</template
            ><template v-if="comment.author">
              [<span :class="isAuthorOnline ? 'online' : 'offline'">{{
                isAuthorOnline ? "online" : "offline"
              }}</span
              >]</template
            >, {{ formattedDate
            }}<template v-if="isEdited">
              | Отредактировано {{ formattedEditDate }}</template
            ></span
          >

          <!-- Actions -->
          <span class="comment-actions">
            <span v-if="canLike || likesCount > 0" class="likes-container">
              <button
                v-if="canLike"
                class="like-btn"
                :class="{ liked: isLikedByMe }"
                :aria-label="
                  likesCount > 0 ? `Нравится: ${likesCount}` : 'Нравится'
                "
                @click="toggleLike"
              >
                <SvgIcon
                  :name="isLikedByMe ? 'heartFilled' : 'heartEmpty'"
                  class="like-icon"
                />
                <span v-if="likesCount > 0" class="likes-count">{{
                  likesCount
                }}</span>
              </button>
              <Tooltip
                v-else
                :text="likersTooltip"
                focusable
                class="like-static"
                :aria-label="`Нравится: ${likesCount}`"
              >
                <SvgIcon
                  :name="isLikedByMe ? 'heartFilled' : 'heartEmpty'"
                  class="like-icon"
                />
                <span class="likes-count">{{ likesCount }}</span>
              </Tooltip>
            </span>
            <button v-if="canQuote" class="action-btn" @click="quoteComment">
              Цитировать
            </button>
            <button v-if="canEdit" class="action-btn" @click="startEdit">
              Редактировать
            </button>
            <button
              v-if="canDelete"
              class="action-btn delete-btn"
              @click="requestDelete"
            >
              Удалить
            </button>
            <button
              v-if="canWarn"
              class="action-btn warn-btn"
              @click="handleWarn"
            >
              Предупреждение
            </button>
          </span>

          <!-- Number (anchor link) — compact layout only (no '#'). Wrapped in a
               span that IS the flex item, so margin-left:auto pins the number
               to the footer's right edge regardless of author presence. -->
          <span v-if="compact && number" class="comment-number-slot">
            <Tooltip text="Скопировать ссылку">
              <button
                class="comment-number"
                :aria-label="`Скопировать ссылку на комментарий ${number}`"
                @click="copyAnchorLink"
              >
                {{ number }}
              </button>
            </Tooltip>
          </span>
        </div>
      </div>
    </template>

    <!-- The question "Удалить" asks before it deletes. Kept outside the
         isRemoved branches so the answer still has a dialog to close. -->
    <ConfirmDialog
      :show="showDeleteConfirm"
      title="Удалить комментарий?"
      message="Комментарий будет удален. Это действие необратимо."
      confirm-label="Удалить"
      danger
      :loading="deleting"
      @confirm="handleDelete"
      @update:show="showDeleteConfirm = $event"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/BbcodeContent" as *

.comment
  display: flex
  gap: $medium
  padding: $medium
  border: 1px dashed $border
  background-color: $bg-element

  &.removed
    background-color: $bg-element-accent

  &.compact
    display: block

.deleted-placeholder
  display: flex
  align-items: center
  gap: $small
  padding: $small 0
  color: $text-muted
  width: 100%

.deleted-text
  font-style: italic

.avatar-link
  flex-shrink: 0

.avatar
  width: 72px
  height: 72px
  object-fit: cover
  border-radius: $border-radius

.comment-body
  flex: 1
  min-width: 0

// Full-layout header: author block (name + meta) on the left, permalink
// pinned to the top-right corner.
.comment-header
  display: flex
  justify-content: space-between
  align-items: flex-start
  gap: $small
  margin-bottom: $small

.author-block
  display: flex
  flex-direction: column
  gap: 2px
  min-width: 0

.author-line
  display: inline-flex
  align-items: center
  flex-wrap: wrap
  gap: $tiny

.author-name
  color: $text
  font-weight: 700
  font-size: $font-size
  text-decoration: none

  &:hover
    color: $link-hover
    text-decoration: underline

// Online status is SELECTABLE bracketed text ("[online]" / "[offline]"),
// the original-site idiom — never a status dot (owner rule; per COMM-6
// the label stays English, not Russian).
.online
  color: $accent-green

.offline
  color: $text-muted

.comment-meta
  font-size: $tertiary-font-size
  color: $text-muted

// .comment-text uses the global .bbcode-content class for typography.
// Truncation, fade, expand link, and media shrinkage are owned by
// <TruncatedContent> — see @/shared/ui/TruncatedContent.
.comment-text
  color: $text
  line-height: 1.6

.edit-container
  margin-bottom: $small

.edit-actions
  display: flex
  gap: $small
  margin-top: $small

.comment-footer
  display: flex
  align-items: baseline
  flex-wrap: wrap
  gap: $small
  margin-top: $small
  font-size: $tertiary-font-size
  color: $text-muted

.author-info
  color: $text-muted

.author-link
  color: $link
  text-decoration: none

  &:hover
    color: $link-hover
    text-decoration: underline

.author-deleted
  font-style: italic

.role-letter
  font-weight: bold
  color: $accent-green
  cursor: help

.comment-actions
  display: inline-flex
  align-items: center
  gap: $small

// In the compact layout the actions trail the inline author line, so they need
// a small gap from it. In the full layout the footer holds only the actions.
.comment.compact .comment-actions
  margin-left: $small

.likes-container
  position: relative
  display: inline-flex

.like-btn
  display: inline-flex
  align-items: center
  gap: 2px
  padding: 0 $tiny
  border: none
  background: transparent
  cursor: pointer
  color: $text-muted
  font-size: $secondary-font-size

  &:hover:not(:disabled)
    color: $accent-red

  &.liked
    color: $accent-red

  &:disabled
    cursor: default
    opacity: 0.6

// Non-interactive like indicator (own comment / guest): shows the count
// without a clickable affordance.
.like-static
  display: inline-flex
  align-items: center
  gap: 2px
  padding: 0 $tiny
  color: $text-muted
  font-size: $secondary-font-size

.likes-count
  font-weight: bold

.action-btn
  padding: 0 $tiny
  font-size: $secondary-font-size
  border: none
  background: transparent
  cursor: pointer
  color: $text-muted

  &:hover
    color: $link

  &.save-btn
    background-color: $button-bg
    color: $button-text
    padding: $tiny $small
    border-radius: $tiny

  &.cancel-btn
    padding: $tiny $small

  &.delete-btn:hover
    color: $accent-red

  &.warn-btn:hover
    color: $accent-red

// Compact: this span is the footer flex item, so the auto margin must live
// here (not on the inner button) to pin the number to the right edge.
.comment-number-slot
  margin-left: auto
  display: inline-flex

.comment-number
  flex-shrink: 0
  padding: 0
  border: none
  background: transparent
  cursor: pointer
  color: $text-muted
  font-size: $tertiary-font-size
  font-family: inherit

  &:hover
    color: $link
</style>

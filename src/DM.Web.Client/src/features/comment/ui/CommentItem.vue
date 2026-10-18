<script setup lang="ts">
import { ref, computed, watch } from "vue";
import { htmlToBbcode } from "@/shared/lib/utils/bbcode";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import type {
  ApiResult,
  Envelope,
  GeneralError,
} from "@/shared/api/models/common";
import type { Comment } from "@/shared/api/models/common/comment";
import { unwrapResource } from "@/shared/api";
import {
  REMOVED_ACCOUNT_NAME,
  RemovedAccountLink,
  removedAccountRoute,
  useAuthStore,
  userIsModerator,
} from "@/entities/user";
import { useUiStore } from "@/shared/stores/ui";
import { AvatarImg } from "@/shared/ui/AvatarImg";
import { getRoleBadge } from "@/shared/config/roles";
import { Tooltip } from "@/shared/ui/Tooltip";
import { TruncatedContent } from "@/shared/ui/TruncatedContent";
import dayjs from "dayjs";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import {
  initBbcodeInteractive,
  trimHtmlWhitespace,
} from "@/shared/lib/utils/bbcodeInteractive";
import { highlightDom, clearDomHighlight } from "@/shared/lib/utils/highlight";
import {
  CaptionLine,
  CaptionPart,
  CaptionAction,
} from "@/shared/ui/CaptionLine";
import { LikeBadge } from "@/shared/ui/LikeBadge";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { ONLINE_THRESHOLD_MINUTES } from "@/shared/lib/constants/user";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import Button from "@/shared/ui/Button/Button.vue";
import { reachesByRank } from "@/entities/moderation";

/** What a mutation answers with: null when the server took the change. */
type SubmitResult = { error: GeneralError | null };

/** Who shows a refused request: the two flags the api client reads by name. */
type RefusalFlags = { ownsRefusal?: boolean; ownsRateLimit?: boolean };

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
     * (forum/blog/game). A discussion has to pass it and the four below:
     * without a source fetch there is no safe way to edit. Only a read-only
     * comment comes without them, and offers nothing that would call them.
     */
    fetchEditSource?: (id: string) => Promise<ApiResult<Envelope<Comment>>>;
    /**
     * Saves the edited BBCode and answers whether the server took it. A
     * function rather than an event, because only the answer may close the
     * editor: an event has no result to wait for, and closing on the emit is
     * what threw a rejected edit away and left the reader in front of the old
     * text with nothing said.
     */
    submitEdit?: (id: string, text: string) => Promise<SubmitResult>;
    /**
     * Deletes the comment. A function for the same reason: only the answer
     * tells this item whether the deleted-comment placeholder is true. The
     * flags are the confirmation's, which prints a refusal in itself and wants
     * the interceptor quiet about it; a caller that drops them leaves the
     * interceptor speaking as well.
     */
    submitDelete?: (
      id: string,
      options?: RefusalFlags,
    ) => Promise<SubmitResult>;
    /**
     * Likes the comment. A function for the same reason again: a refused like
     * leaves the heart exactly as it was, so only the answer tells this item
     * that the press went nowhere and there is something to say about it.
     */
    submitLike?: (id: string) => Promise<SubmitResult>;
    /** Takes the reader's like back; answers as submitLike does. */
    submitUnlike?: (id: string) => Promise<SubmitResult>;
    /**
     * The reader is on the blacklist of the game or blog this discussion
     * belongs to. Of what this item offers, the list closes only the edit of
     * one's own comment; the heart and "Удалить" stay as they are for anybody.
     */
    viewerBlacklisted?: boolean;
    /**
     * The comment stands to be read and nothing more: no edit, no removal, no
     * warning, no heart, for any reader. The conversation of a ticket is such
     * a list (TicketConversation): it is the record of how the ticket was
     * settled, and a word rewritten or taken back after it was read breaks
     * the record. The number stays, a link to the comment as everywhere.
     */
    readonly?: boolean;
    /**
     * The comment of the guest who filed a ticket, written without an account
     * and arriving, as a removed account's does, with no author. It is signed
     * "Гость" in the name's place, with no link, for there is no profile to
     * lead to, and with no mark of presence.
     */
    guest?: boolean;
    /**
     * What one item of the list is called, in the name its number is read
     * out with: an answer of a ticket is not a comment.
     */
    noun?: string;
  }>(),
  {
    compact: true,
    viewerBlacklisted: false,
    readonly: false,
    guest: false,
    noun: "Комментарий",
  },
);

const emit = defineEmits<{
  /** The comment pressed under, and the number it shows, where it has one. */
  warn: [id: string, number?: number];
}>();

const EDIT_TIME_LIMIT_MINUTES = 15;

/** The signature of a guest's comment, where a name would stand. */
const GUEST_NAME = "Гость";

const route = useRoute();
const { user: currentUser } = storeToRefs(useAuthStore());

// The typographic switch of the settings panel. The old site kept the text of
// a comment in the same div.dmtxt as the text of a post, so its switches
// reached it there, and "Перенос слов" reaches it here. The text is flush on
// both sides at every width (+body-text).
const { hyphenateWords } = storeToRefs(useUiStore());
const typesetting = computed(() => ({
  hyphenated: hyphenateWords.value,
}));

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

// Who may touch the comment at all: a moderator always, its author for the
// first quarter of an hour; nobody where it stands to be read.
const canModify = computed(() => {
  if (props.readonly) return false;
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

// A blacklisted author keeps "Удалить" and loses "Редактировать": taking a
// comment down is still theirs, rewriting it is writing in a discussion the
// blacklist closed to them.
const canEdit = computed(
  () => canModify.value && !(props.viewerBlacklisted && isAuthor.value),
);

const canDelete = computed(() => canModify.value);

// A warning reaches only a rank below the reader's and never an administrator
// (reachesByRank, the server's rank rule): a moderator is not offered one under
// the text of a colleague, a senior moderator or an administrator. A removed
// account has no author to rank, and nobody to warn.
const canWarn = computed(
  () =>
    !props.readonly && reachesByRank(currentUser.value, props.comment.author),
);

const likes = computed(() => props.comment.likes ?? []);

const likesCount = computed(() => likes.value.length);

const isLikedByMe = computed(() => {
  if (!currentUser.value) return false;
  return likes.value.some((u) => u.username === currentUser.value?.username);
});

const canLike = computed(() => {
  if (props.readonly) return false;
  if (!currentUser.value) return false;
  return !isAuthor.value;
});

// The heart: pressed by whoever may like the comment, and drawn for everybody
// once somebody did. A comment that stands to be read has none.
const showLike = computed(
  () => !props.readonly && (canLike.value || likesCount.value > 0),
);

// Whether the caption line of the compact layout has any part: it always
// carries the author-info part (rendered even for a deleted author, see the
// "Аккаунт удален" branch below), so it is never empty.
const hasCaptionContent = computed(() => props.compact);

// The line under the text of the full layout, the edit line of a post: when
// the comment was edited and what the viewer may do with it. A comment with
// neither has no line at all.
const hasEditLine = computed(
  () =>
    !props.compact &&
    !isEditing.value &&
    (isEdited.value || canEdit.value || canDelete.value || canWarn.value),
);

// The footer holds the caption line of the compact layout and the number, each
// of which may be alone in it; the full layout's holds the number alone, in
// the card's corner, as a post's does.
const hasFooterContent = computed(
  () => hasCaptionContent.value || !!props.number,
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

// The author of a comment whose account was removed arrives as null. The
// avatar stands in its column all the same and leads where the name does: to
// the profile of "Аккаунт удален", which gathers what removed accounts left.
// A guest has no profile, and his avatar leads nowhere.
const authorTo = computed(() => {
  if (props.comment.author)
    return {
      name: "profile",
      params: { username: props.comment.author.username },
    };
  return props.guest ? null : removedAccountRoute;
});

/** What the avatar is called: the name, or the word standing in for one. */
const authorAlt = computed(
  () =>
    props.comment.author?.username ??
    (props.guest ? GUEST_NAME : REMOVED_ACCOUNT_NAME),
);

// Comment rendered HTML, pre-trimmed of leading/trailing whitespace so
// phantom empty lines never eat the truncation budget. Pure transform,
// no DOM mutation. TruncatedContent handles overflow detection, height-
// based truncation, collapsed-state media shrinkage, and the expand link.
const commentHtml = computed(() => trimHtmlWhitespace(props.comment.text));

// Methods
const editLoading = ref(false);

const EDIT_SOURCE_FAILED = "Не удалось загрузить текст комментария";

/**
 * Why a press in the footer did nothing - "Редактировать" that could not read
 * the text, a like the server refused - printed under the footer it sits in.
 *
 * The reader has just pressed a control in this comment's footer and is looking
 * at it; a plate in the corner of the page is the one place they are not
 * looking. The line stands where the press was, needs no click to dismiss and
 * clears itself on the next press, which is also the retry. The plate the
 * interceptor held for the same refusal is taken down (UI_STANDARDS,
 * "Всплывающие сообщения"); an expired session and a dead connection stay
 * with the plate.
 */
const footerResult = useActionResult();

async function startEdit() {
  const fetchEditSource = props.fetchEditSource;
  if (!fetchEditSource || editLoading.value || isEditing.value) return;
  footerResult.clear();
  // comment.text is Display-audience server-rendered HTML; the editor needs
  // the raw BBCode source, which only the AuthorEdit audience returns (same
  // fetch-before-edit idiom as TopicView and the chat message editors).
  editLoading.value = true;
  const { data, error } = await fetchEditSource(props.comment.id);
  editLoading.value = false;
  if (error) {
    footerResult.refuse(error, EDIT_SOURCE_FAILED);
    return;
  }
  editText.value = htmlToBbcode(unwrapResource<Comment>(data)?.text ?? "");
  isEditing.value = true;
}

function cancelEdit() {
  isEditing.value = false;
  editText.value = "";
  editResult.clear();
}

const saving = ref(false);

// A refused save stands under the buttons of the editor, which stays open
// with the text in it.
const editResult = useActionResult();

async function saveEdit() {
  const submitEdit = props.submitEdit;
  if (!submitEdit || !editText.value.trim() || saving.value) return;
  saving.value = true;
  editResult.clear();
  const { error } = await submitEdit(props.comment.id, editText.value);
  saving.value = false;
  if (error) {
    // The server refuses the text by the command's own name for it, and says
    // "too long" or "nested too deep" without saying of what: the line names it.
    editResult.refuse(error, "Не удалось сохранить комментарий", {
      text: "Текст комментария",
    });
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

// One request at a time: the heart stays on screen while it is in flight, and a
// second press would race the first into a like the server already has, which
// it refuses. The publication's heart keeps the same rule.
const liking = ref(false);

async function toggleLike() {
  const unliking = isLikedByMe.value;
  const submit = unliking ? props.submitUnlike : props.submitLike;
  if (!submit || liking.value) return;
  liking.value = true;
  footerResult.clear();
  const { error } = await submit(props.comment.id);
  liking.value = false;
  // A refused like changes nothing on the heart, so the line under the footer
  // says it. The words are the publication heart's.
  if (error) {
    footerResult.refuse(
      error,
      unliking ? "Не удалось убрать лайк" : "Не удалось поставить лайк",
    );
  }
}

const showDeleteConfirm = ref(false);

// "Удалить" sits at a $small step from "Редактировать" and "Предупреждение", and
// it used to fire on the first click with nothing to undo it. The post of a game
// and a forum topic both ask first, through this same dialog.
function requestDelete() {
  showDeleteConfirm.value = true;
}

// The confirmation's action: it waits shut for the answer, prints a refusal in
// itself and closes on success alone. The question is asked only where
// "Удалить" is offered, which a discussion handing no removal never is.
const deleteComment = (): Promise<SubmitResult> =>
  props.submitDelete
    ? props.submitDelete(props.comment.id, {
        ownsRefusal: true,
        ownsRateLimit: true,
      })
    : Promise.resolve({ error: null });

// The warning window is the list's (useCommentWarnDialog): the list hands it
// on as it comes, so the number the item shows travels with the press, and the
// window links that number to the comment. A removed account has nobody to
// warn, so the word is not offered there (canWarn).
function handleWarn() {
  emit("warn", props.comment.id, props.number);
}

// The number is a link to the comment and nothing more (owner, 12 October
// 2026): a press goes to it as a press on any link does, and its address is
// copied from the browser's own menu. No tooltip, no clipboard, no plate.
// It keeps the whole query, the view of the list the reader is on: the page
// number of a filtered or sorted list is a page of that view, and the same
// page of the whole list holds other comments. So a press moves to the anchor
// and leaves the list as it was, and the copied address opens the view where
// the comment is.
const numberTo = computed(() => ({
  query: route.query,
  hash: `#comment-${props.comment.id}`,
}));

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
      <!-- The columns of the full layout, a post's (GamePost.vue): the
           author's column at the left, the text beside it. In the compact
           layout a plain block around the text. -->
      <div class="comment-columns">
        <!-- The head of the full layout: the author's column, as the left
             column of a post stands, the name, the avatar the width of the
             column, the date and the like under it. A removed account keeps
             the column, with the silhouette of a user without a picture. The
             avatar leads where the name does, and a guest's leads nowhere. -->
        <div v-if="!compact" class="comment-header">
          <div class="author-inner">
            <!-- The name, the letter of the role and the mark of presence, all
                 of them at every width, no name cut and nothing put away.
                 Where the column has no room the line breaks between them, at
                 the spaces of .author-gap, and each goes down whole. -->
            <span class="author-line"
              ><router-link
                v-if="comment.author"
                :to="{
                  name: 'profile',
                  params: { username: comment.author.username },
                }"
                class="author-name"
                >{{ comment.author.username }}</router-link
              ><span v-else-if="guest" class="author-name">{{
                GUEST_NAME
              }}</span
              ><RemovedAccountLink v-else class="author-name" /><template
                v-if="roleBadge"
                ><span class="author-gap">{{ " " }}</span
                ><span class="author-role"
                  ><Tooltip :text="roleBadge.label"
                    ><b class="role-letter"
                      >[{{ roleBadge.letter }}]</b
                    ></Tooltip
                  ></span
                ></template
              ><template v-if="comment.author"
                ><span class="author-gap">{{ " " }}</span
                ><span class="author-presence"
                  >[<span :class="isAuthorOnline ? 'online' : 'offline'">{{
                    isAuthorOnline ? "online" : "offline"
                  }}</span
                  >]</span
                ></template
              ></span
            >
            <router-link v-if="authorTo" :to="authorTo" class="avatar-link">
              <AvatarImg
                :picture="comment.author?.picture"
                :alt="authorAlt"
                :size="128"
                img-class="avatar"
              />
            </router-link>
            <span v-else class="avatar-link">
              <AvatarImg :alt="authorAlt" :size="128" img-class="avatar" />
            </span>
            <span v-if="comment.createdUtc" class="comment-date">{{
              formattedDate
            }}</span>
            <!-- The like stands where a post has the sum of its reviews. -->
            <span v-if="showLike" class="comment-like"
              ><LikeBadge
                :likes="likes"
                :liked="isLikedByMe"
                :interactive="canLike"
                @toggle="toggleLike"
            /></span>
          </div>
        </div>

        <div class="comment-body">
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
                <Button
                  type="button"
                  variant="primary"
                  class="save-btn"
                  :loading="saving"
                  @click="saveEdit"
                >
                  Сохранить
                </Button>
                <Button type="button" class="cancel-btn" @click="cancelEdit">
                  Отмена
                </Button>
              </div>
              <ActionResult :result="editResult" />
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
              <div
                class="comment-text bbcode-content"
                :class="typesetting"
                v-html="commentHtml"
              />
            </TruncatedContent>
          </template>

          <!-- The edit line of the full layout, under the text as a post has
               it: "Отредактировано ... | Редактировать | Удалить |
               Предупреждение". It wraps between parts where the text is
               narrower, and nothing of it is put away for want of room. -->
          <div v-if="hasEditLine" class="comment-edit-line">
            <CaptionLine>
              <span v-if="isEdited"
                >Отредактировано {{ formattedEditDate }}</span
              >
              <CaptionAction v-if="canEdit" @click="startEdit"
                >Редактировать</CaptionAction
              >
              <CaptionAction v-if="canDelete" @click="requestDelete"
                >Удалить</CaptionAction
              >
              <CaptionAction v-if="canWarn" @click="handleWarn"
                >Предупреждение</CaptionAction
              >
            </CaptionLine>
          </div>
        </div>
      </div>

      <!-- Footer: in the compact layout one caption line (the author and the
           dates, then the actions, the like last) and the number in its own
           corner; in the full layout the number alone, in the corner of the
           card, as a post has it. -->
      <div v-if="hasFooterContent" class="comment-footer">
        <!-- Wraps between parts at every width, as the caption of a topic, and
             a narrow column breaks after the comma and keeps the date whole.
             Nothing of it is put away for want of room. -->
        <div v-if="hasCaptionContent" class="comment-caption">
          <CaptionLine>
            <CaptionPart
              :joint="comment.createdUtc ? ',' : undefined"
              class="author-info"
              >Автор:
              <router-link
                v-if="comment.author"
                :to="{
                  name: 'profile',
                  params: { username: comment.author.username },
                }"
                class="author-link"
                >{{ comment.author.username }}</router-link
              ><template v-else-if="guest">{{ GUEST_NAME }}</template
              ><RemovedAccountLink v-else /><template v-if="roleBadge">
                [<Tooltip :text="roleBadge.label"
                  ><b class="role-letter">{{ roleBadge.letter }}</b></Tooltip
                >]</template
              ><template v-if="comment.author">
                [<span :class="isAuthorOnline ? 'online' : 'offline'">{{
                  isAuthorOnline ? "online" : "offline"
                }}</span
                >]</template
              ></CaptionPart
            >
            <span v-if="comment.createdUtc">{{ formattedDate }}</span>
            <span v-if="isEdited">Отредактировано {{ formattedEditDate }}</span>
            <CaptionAction v-if="canEdit" @click="startEdit"
              >Редактировать</CaptionAction
            >
            <CaptionAction v-if="canDelete" @click="requestDelete"
              >Удалить</CaptionAction
            >
            <CaptionAction v-if="canWarn" @click="handleWarn"
              >Предупреждение</CaptionAction
            >
            <LikeBadge
              v-if="showLike"
              :likes="likes"
              :liked="isLikedByMe"
              :interactive="canLike"
              @toggle="toggleLike"
            />
          </CaptionLine>
        </div>

        <!-- The number, a link to this comment (no '#'), in the right corner
             of the footer in both layouts. A number alone does not say what
             it is to a screen reader, hence the name; the router marks every
             link to this page as the current one, and none of these is. -->
        <router-link
          v-if="number"
          :to="numberTo"
          class="comment-number"
          :aria-label="`${noun} ${number}`"
          :aria-current="undefined"
          >{{ number }}</router-link
        >
      </div>

      <!-- Why a press did nothing, under the comment it was pressed in. A
           plate in the corner used to say this. -->
      <ActionResult :result="footerResult" />
    </template>

    <!-- The question "Удалить" asks before it deletes. Kept outside the
         isRemoved branches so the answer still has a dialog to close. -->
    <ConfirmDialog
      v-model:show="showDeleteConfirm"
      title="Удалить комментарий?"
      message="Комментарий будет удален без возможности восстановления"
      confirm-label="Удалить"
      failure="Не удалось удалить комментарий"
      :action="deleteComment"
    />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/BbcodeContent" as *
@use "@/assets/styles/Inputs" as *

.comment
  padding: $medium
  border: 1px dashed $border
  background-color: $bg-element

  &.removed
    background-color: $bg-element-accent

// The full layout is the card of a post (GamePost.vue, .post-card): 5px of
// padding around the columns and the row of the number.
.comment:not(.compact):not(.removed)
  padding: 5px

.deleted-placeholder
  display: flex
  align-items: center
  gap: $small
  padding: $small 0
  color: $text-muted
  width: 100%

.deleted-text
  font-style: italic

// The columns of a post (GamePost.vue, .post-columns): DM2's td.firstrow of
// 140, a spacer of 10 and the text beside them, at every width.
.comment:not(.compact) .comment-columns
  display: flex
  gap: 10px
  align-items: flex-start

// The author's column, the left column of a post (.post-meta, .meta-inner):
// 140 wide, its content 11px in from the top and 5px from the sides with 2px
// more on the right, which leaves 128; its lines on DM2's step of 20.
.comment-header
  flex-shrink: 0
  width: 140px
  text-align: left
  font-size: $font-size
  line-height: 1.25

.author-inner
  margin: 11px 5px
  padding-right: 2px

// One line each, as in the column of a post: the name a step over the avatar.
.author-line
  display: block
  padding-bottom: 4px

.avatar-link
  display: block

// The avatar fills the column as the portrait of a post does, with its frame,
// square as every avatar is cut. Never rounded.
.avatar
  display: block
  box-sizing: border-box
  width: 100%
  height: auto
  margin: 2px 0
  border: 1px solid $border

// The date in the colour of the text, on one line, as under the portrait of a
// post (GamePost.vue, .post-date): of a fixed width, a few pixels wider than
// the 128 of the content, it ends inside the column, short of the spacer.
.comment-date
  display: block
  color: $text
  white-space: nowrap

// The like where a post has the sum of its reviews, the badge of the caption
// line in the caption's size and grey it was approved in.
.comment-like
  display: block
  color: $text-muted
  font-size: $tertiary-font-size
  line-height: 15px

.comment-body
  flex: 1
  min-width: 0

// The text column of the full layout, a post's (.post-body): the 11px above
// and below of DM2's div.dmtxt, the text 22px under the top of the column.
.comment:not(.compact) .comment-body
  padding: 11px 0

// A bold name in the colour of the text.
.author-name
  font-weight: 700
  font-size: $font-size
  color: $text

// A link, answering the pointer as every link of the site does. The word that
// signs a guest's comment is no link, and does not.
a.author-name
  +link-tone($text)

// Online status is SELECTABLE bracketed text ("[online]" / "[offline]"),
// the original-site idiom — never a status dot (owner rule; per COMM-6
// the label stays English, not Russian).
.online
  color: $accent-green

.offline
  color: $text-muted

// The name, the letter of the role and the mark of presence: each one box, as
// wide as its text, that goes to the next line whole where it does not fit
// what is left of this one. One wider than the whole line, a login of twenty
// wide letters on a phone, takes the line's width and wraps inside, the word
// broken by the site's rule (Reset.sass, word-wrap on the body); nowrap would
// turn that break off as well (CaptionLine.vue).
.author-name,
.author-role,
.author-presence
  display: inline-block
  max-width: 100%

// The space between two of them: the line's own, the one place it breaks, so
// it goes away where the line breaks and the next line starts flush, and the
// line copies "TestMentor [M] [offline]". Narrowed to $tiny, the hair the flex
// row with a gap of $tiny kept between them before 5 October: 0.2676em is the
// advance of a space of PT Sans.
.author-gap
  word-spacing: calc(#{$tiny} - 0.2676em)

// The word a hair inside its brackets, "[ offline ]", as that row drew it.
.author-presence > .online,
.author-presence > .offline
  padding: 0 $tiny

// .comment-text uses the global .bbcode-content class for typography.
// Truncation, fade, expand link, and media shrinkage are owned by
// <TruncatedContent> — see @/shared/ui/TruncatedContent.
// DM2 div.dmtxt, the block the text of every comment stood in there: 16px on a
// step of 20, the line of a post (GamePost.vue, .game-text), flush on both
// sides at every width, and hyphenated while its switch is on.
.comment-text
  color: $text
  +body-text

.edit-container
  margin-bottom: $small

// In the full layout the text and its editor stand as the text of a post:
// DM2's div.p, 11px under the column's own 11px and 10px in from its edges.
.comment:not(.compact) .comment-text,
.comment:not(.compact) .edit-container
  margin: 11px 10px

// "... показать полностью" at the left edge of the text it opens.
.comment:not(.compact) .comment-body :deep(.truncated-expand-button)
  margin-left: 10px

// The edit line of the full layout, a post's (.post-edit-line): 12px grey at
// the left edge of the text, 22px under it, 11px over the row of the number.
// The caption's size on the block as well, so the line it stands on is the
// caption's 15px and not the text's.
.comment-edit-line
  margin: 11px 5px 0 10px
  font-size: $tertiary-font-size
  line-height: 15px
  text-align: left

.edit-actions
  display: flex
  gap: $small
  margin-top: $small

// The caption line takes the row and wraps inside itself; the number keeps
// its corner and sits on the baseline of the line's last row.
.comment-footer
  display: flex
  flex-wrap: nowrap
  align-items: last baseline
  gap: $small
  margin-top: $small

// The full layout's footer is the row of the number of a post, DM2's
// td[colspan=3]: right under the columns, the caption's 15px high, the number
// in the very corner of the card's padding.
.comment:not(.compact) .comment-footer
  margin-top: 0
  font-size: $tertiary-font-size
  line-height: 15px

// The caption's size on the block as well, so the line it stands on is the
// caption's height and not the text's.
.comment-caption
  flex: 1 1 0%
  min-width: 0
  font-size: $tertiary-font-size

.author-link
  +link-tone($link)

.role-letter
  font-weight: bold
  color: $accent-green
  cursor: help

// The number is a grey link of the caption's size to the comment, pinned to
// the right edge of the footer by its auto margin whatever stands left of it,
// or nothing does.
.comment-number
  flex: none
  margin-left: auto
  font-size: $tertiary-font-size
  +muted-link
</style>

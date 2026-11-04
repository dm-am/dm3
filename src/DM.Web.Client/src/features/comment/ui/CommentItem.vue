<script setup lang="ts">
import { ref, computed } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import type {
  ApiResult,
  Envelope,
  SubmitResult,
} from "@/shared/api/models/common";
import type { Comment } from "@/shared/api/models/common/comment";
import { unwrapResource, type RequestOptions } from "@/shared/api";
import { useAuthStore, userIsModerator } from "@/entities/user";
import dayjs from "dayjs";
import { CaptionAction } from "@/shared/ui/CaptionLine";
import { LikeBadge } from "@/shared/ui/LikeBadge";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { reachesByRank } from "@/entities/moderation";
import CommentActions from "./CommentActions.vue";
import CommentAuthor from "./CommentAuthor.vue";
import CommentCaption from "./CommentCaption.vue";
import CommentEdit from "./CommentEdit.vue";
import CommentText from "./CommentText.vue";
import { EDIT_TIME_LIMIT_MINUTES } from "@/shared/lib/utils/chat";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const props = withDefaults(
  defineProps<{
    comment: Comment;
    compact?: boolean;
    number?: number;
    searchQuery?: string;
    /** Fetches the raw BBCode for the editor: the shown text is server-rendered HTML. */
    fetchEditSource?: (id: string) => Promise<ApiResult<Envelope<Comment>>>;
    /** A function, not an event: only the answer may close the editor. */
    submitEdit?: (id: string, text: string) => Promise<SubmitResult>;
    /** The flags are the confirmation's, which prints the refusal itself. */
    submitDelete?: (
      id: string,
      options?: RequestOptions,
    ) => Promise<SubmitResult>;
    submitLike?: (id: string) => Promise<SubmitResult>;
    submitUnlike?: (id: string) => Promise<SubmitResult>;
    /** The blacklist closes only the edit of one's own comment. */
    viewerBlacklisted?: boolean;
    /** The record of a ticket: no edit, removal, warning or like for anyone. */
    readonly?: boolean;
    /** A guest's ticket comment: signed "Гость", with no link and no presence mark. */
    guest?: boolean;
    /** What the number is read out with: an answer of a ticket is not a comment. */
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
  warn: [id: string, number?: number];
}>();

const route = useRoute();
const { user: currentUser } = storeToRefs(useAuthStore());

const isEditing = ref(false);
const editSource = ref("");

const isEdited = computed(() => !!props.comment.modifiedUtc);

const isModerator = computed(() => userIsModerator(currentUser.value));

const isAuthor = computed(
  () => currentUser.value?.username === props.comment.author?.username,
);

// A moderator always, the author for the first quarter of an hour.
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

// A blacklisted author may still take the comment down, but not rewrite it.
const canEdit = computed(
  () => canModify.value && !(props.viewerBlacklisted && isAuthor.value),
);

// Only a rank below the reader's, never an administrator (reachesByRank).
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

const showLike = computed(
  () => !props.readonly && (canLike.value || likesCount.value > 0),
);

const hasEditLine = computed(
  () =>
    !props.compact &&
    !isEditing.value &&
    (isEdited.value || canEdit.value || canModify.value || canWarn.value),
);

const hasFooterContent = computed(() => props.compact || !!props.number);

const editLoading = ref(false);

const EDIT_SOURCE_FAILED = "Не удалось загрузить текст комментария";

// A refusal of a footer press is printed under the footer, where the reader looks.
const footerResult = useActionResult();

async function startEdit() {
  const fetchEditSource = props.fetchEditSource;
  if (!fetchEditSource || editLoading.value || isEditing.value) return;
  footerResult.clear();
  // comment.text is rendered HTML; the author_edit answer is the text as typed.
  editLoading.value = true;
  const { data, error } = await fetchEditSource(props.comment.id);
  editLoading.value = false;
  if (error) {
    footerResult.refuse(error, EDIT_SOURCE_FAILED);
    return;
  }
  editSource.value = unwrapResource<Comment>(data)?.text ?? "";
  isEditing.value = true;
}

function closeEdit() {
  isEditing.value = false;
  editSource.value = "";
}

// One request at a time: a second press would race the first into a refused like.
const liking = ref(false);

async function toggleLike() {
  const unliking = isLikedByMe.value;
  const submit = unliking ? props.submitUnlike : props.submitLike;
  if (!submit || liking.value) return;
  liking.value = true;
  footerResult.clear();
  const { error } = await submit(props.comment.id);
  liking.value = false;
  if (error) {
    footerResult.refuse(
      error,
      unliking ? "Не удалось убрать отметку" : "Не удалось отметить",
    );
  }
}

const showDeleteConfirm = ref(false);

const deleteComment = (): Promise<SubmitResult> =>
  props.submitDelete
    ? props.submitDelete(props.comment.id, OWNS_REFUSAL)
    : Promise.resolve({ error: null });

function handleWarn() {
  emit("warn", props.comment.id, props.number);
}

// The whole query is kept: the page number belongs to the reader's view of the list.
const numberTo = computed(() => ({
  query: route.query,
  hash: `#comment-${props.comment.id}`,
}));
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
    <!-- Set locally on delete; the server never sends a removed comment. -->
    <template v-if="comment.isRemoved">
      <div class="deleted-placeholder">
        <span class="deleted-text">Комментарий удален</span>
      </div>
    </template>

    <template v-else>
      <div class="comment-columns">
        <div v-if="!compact" class="comment-header">
          <CommentAuthor :comment="comment" :guest="guest">
            <span v-if="showLike" class="comment-like"
              ><LikeBadge
                :likes="likes"
                :liked="isLikedByMe"
                :interactive="canLike"
                @toggle="toggleLike"
            /></span>
          </CommentAuthor>
        </div>

        <div class="comment-body">
          <CommentEdit
            v-if="isEditing"
            :comment-id="comment.id"
            :source="editSource"
            :is-moderator="isModerator"
            :submit-edit="submitEdit"
            @saved="closeEdit"
            @cancel="closeEdit"
          />
          <CommentText
            v-else
            :html="comment.text"
            :search-query="searchQuery"
          />

          <CommentActions
            v-if="hasEditLine"
            :edited-at="comment.modifiedUtc ?? null"
            :can-edit="canEdit"
            :can-delete="canModify"
            :can-warn="canWarn"
            @edit="startEdit"
            @delete="showDeleteConfirm = true"
            @warn="handleWarn"
          />
        </div>
      </div>

      <div v-if="hasFooterContent" class="comment-footer">
        <CommentCaption v-if="compact" :comment="comment" :guest="guest">
          <CaptionAction v-if="canEdit" @click="startEdit"
            >Редактировать</CaptionAction
          >
          <CaptionAction v-if="canModify" @click="showDeleteConfirm = true"
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
        </CommentCaption>

        <!-- The router marks every link to this page current, and none of these is. -->
        <router-link
          v-if="number"
          :to="numberTo"
          class="comment-number"
          :aria-label="`${noun} ${number}`"
          :aria-current="undefined"
          >{{ number }}</router-link
        >
      </div>

      <ActionResult :result="footerResult" />
    </template>

    <!-- Outside the isRemoved branches, so the answer still has a dialog to close. -->
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
@use "@/assets/styles/CommentParts" as *
// The full layout is a post card (GamePost.vue) on its step.
@use "@/assets/styles/PostCard" as *

.comment
  +comment-frame

  &.removed
    background-color: $bg-element-accent

// The post card's 5px around the columns and the row of the number.
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

.comment:not(.compact) .comment-columns
  display: flex
  gap: $text-inset
  align-items: flex-start

// DM2's td.firstrow of 140, its lines on DM2's step of 20, as a post's.
.comment-header
  flex-shrink: 0
  width: 140px
  text-align: left
  font-size: $font-size
  line-height: 1.25

// 15px: the line of DM2's 12px caption, here and in the edit line and the footer.
.comment-like
  display: block
  color: $text-muted
  font-size: $tertiary-font-size
  line-height: 15px

.comment-body
  flex: 1
  min-width: 0

// The text column of a post: DM2's div.dmtxt, 11px above and below.
.comment:not(.compact) .comment-body
  padding: $block-margin 0

  // The text and its editor stand as a post's text, DM2's div.p.
  :deep(.comment-text),
  :deep(.edit-container)
    margin: $block-margin $text-inset

  // The edit line at the text's left edge, 22px under it, 5px in on the right as the author's column.
  :deep(.comment-edit-line)
    margin: $block-margin 5px 0 $text-inset

  :deep(.truncated-expand-button)
    margin-left: $text-inset

.comment-footer
  display: flex
  flex-wrap: nowrap
  align-items: last baseline
  gap: $small
  margin-top: $small

.comment:not(.compact) .comment-footer
  margin-top: 0
  font-size: $tertiary-font-size
  line-height: 15px

.comment-number
  +comment-number
</style>

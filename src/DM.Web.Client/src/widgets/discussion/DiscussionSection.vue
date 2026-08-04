<script setup lang="ts">
/**
 * The discussion, one section for the whole site.
 *
 * Four records carry comments — topic, game, blog, publication — and the four
 * endpoints behind them read the same query: text search, authors, period,
 * sort. What differs between the pages is only where the comments come from
 * and who may write, so that is what arrives as props, and everything a reader
 * sees is here: the filter bar, paging above and below the list, the comments,
 * the empty and failed states, the permalink and the composer.
 *
 * It exists because the pages had drifted into three shapes. The game and the
 * blog rendered a bare list with a single paging block and no search at all —
 * not because the server could not filter, it always could, but because those
 * pages never sent the params — and answered a failed load with a red line and
 * no way to retry. The comment numbering was spelled out three times, the
 * empty state in two wordings, and a copied permalink resolved on one page of
 * the three.
 */
import { computed, nextTick, ref, watch } from "vue";
import { useRoute } from "vue-router";
import type { RouteLocationRaw } from "vue-router";
import { storeToRefs } from "pinia";
import type {
  ApiResult,
  Comment,
  Envelope,
  GeneralError,
  PagingInfo,
} from "@/shared/api/models/common";
import type { CommentsQuery } from "@/shared/api";
import { useUiStore } from "@/shared/stores/ui";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import Button from "@/shared/ui/Button/Button.vue";
import { CommentSkeleton } from "@/shared/ui/Skeleton";
import { ErrorState } from "@/shared/ui/ErrorState";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { useAuthStore, userIsModerator } from "@/entities/user";
import { CommentItem, useCommentWarnDialog } from "@/features/comment";
import { CommentsFilter, useCommentsFilter } from "@/features/comment-filter";
import { LoginPrompt } from "@/features/auth";

/** What a mutation answers with: null when the server took the change. */
type SubmitResult = { error: GeneralError | null };

const props = defineProps<{
  /** Comments of the current page, as the caller's store holds them. */
  comments: Comment[];
  /** Server paging of the current page, null while unknown. */
  paging: PagingInfo | null;
  /** A load is in flight. */
  loading: boolean;
  /**
   * The last load failed. A flag and not a sentence: the sentence is the same
   * on every discussion, so it is spelled here once.
   */
  failed: boolean;
  /**
   * The record being discussed. A change reloads: this section stays mounted
   * when the router swaps one game (or blog) for another.
   */
  recordId?: string;
  /** Loads a page of the discussion with the filter, sort and paging query. */
  load: (query: CommentsQuery) => unknown;
  /** Posts a new comment; the answer decides whether the draft is dropped. */
  create: (text: string) => Promise<SubmitResult>;
  /** Saves an edited comment; the item keeps its editor open on a refusal. */
  submitEdit: (id: string, text: string) => Promise<SubmitResult>;
  /** Deletes a comment. */
  submitDelete: (id: string) => Promise<SubmitResult>;
  /** Likes a comment. */
  like: (id: string) => unknown;
  /** Takes the like back. */
  unlike: (id: string) => unknown;
  /** Fetches a comment's raw BBCode source for the edit form. */
  fetchEditSource: (id: string) => Promise<ApiResult<Envelope<Comment>>>;
  /** Where the paging links point: the discussion's own route. */
  pagingTo: RouteLocationRaw;
  /** Composer draft key, built by composerDraftKey. */
  draftKey: string;
  /** Whether the viewer may post right now. */
  canComment: boolean;
  /**
   * Why there is no composer for a viewer who is signed in and still may not
   * write (comments closed, read-only). A guest gets the sign-in prompt.
   */
  closedHint?: string;
}>();

const emit = defineEmits<{
  /** The first loaded page has rendered — the caller marks it read. */
  loaded: [];
}>();

/** One sentence for a failed discussion load, wherever it fails. */
const LOAD_FAILURE = "Не удалось загрузить комментарии";

const route = useRoute();
const { isCompactLayout } = storeToRefs(useUiStore());
const { user } = storeToRefs(useAuthStore());
const { filterState, searchParams, hasActiveFilters } = useCommentsFilter();

// The URL is the filter's single source of truth, so one watcher covers the
// filter bar, the sort button and both paging blocks at once. The record is in
// the key because this section stays mounted across a route hop between two
// games, and the comments of the previous one would otherwise stay on screen.
const loadKey = computed(() =>
  JSON.stringify([props.recordId, searchParams.value]),
);

watch(
  loadKey,
  () => {
    if (!props.recordId) return;
    props.load(searchParams.value);
  },
  { immediate: true },
);

function retryLoad() {
  return props.load(searchParams.value);
}

// The caller marks the discussion read once, on the first page that arrives.
const listReady = computed(() => props.comments.length > 0 && !props.loading);
watch(
  listReady,
  (ready) => {
    if (ready) emit("loaded");
  },
  { once: true },
);

/** Position of a comment in the whole discussion, not on this page. */
function commentNumber(index: number): number {
  if (!props.paging) return index + 1;
  return (props.paging.current - 1) * props.paging.size + index + 1;
}

const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Комментариев по заданным фильтрам не найдено"
    : "Комментариев пока нет",
);

// Moderator warning (doc 4.2.4.1) — shared dialog wiring.
const { warnComment: handleWarn } = useCommentWarnDialog((id) =>
  props.comments.find((c) => c.id === id),
);

// Paging scrolls the discussion back into view instead of the page top: the
// record above it (topic bubble, game header) is not worth re-showing.
const sectionRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return sectionRef.value;
}

// Scroll to the comment named by the URL hash (#comment-{id}) once the page
// holding it has rendered. Backs the permalink the item copies: on the game
// and the blog that link used to open the page and go nowhere, because the
// handler lived on the topic alone.
async function scrollToHashComment() {
  const hash = route.hash;
  if (!hash.startsWith("#comment-")) return;
  if (!props.comments.length) return;

  await nextTick();
  // Wait for content (avatars, BBCode media) to settle before measuring.
  await new Promise((resolve) => setTimeout(resolve, 100));

  const element = document.getElementById(hash.slice(1));
  if (!element) return;
  element.scrollIntoView({ behavior: "smooth", block: "center" });
  element.classList.add("highlight-unread");
  setTimeout(() => element.classList.remove("highlight-unread"), 2000);
}

watch(
  () => [props.comments, route.hash] as const,
  () => scrollToHashComment(),
  { immediate: true, flush: "post" },
);

// --- Composer ---
const newComment = ref("");
const sending = ref(false);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);
const isModerator = computed(() => userIsModerator(user.value));

async function handleSend() {
  if (!newComment.value.trim() || sending.value) return;

  const text = newComment.value;
  newComment.value = "";
  sending.value = true;
  const { error } = await props.create(text);
  const failed = Boolean(error);
  if (!failed) await props.load(searchParams.value);
  sending.value = false;

  // Give the text back on failure. Emptying the field before the request is
  // what makes sending feel instant; losing what was written when it fails is
  // not part of that bargain. The editor's own clear() waits for the send to
  // land — it also drops the saved draft, and that copy is the one that
  // outlives the tab.
  if (failed) {
    newComment.value = text;
  } else {
    editorRef.value?.clear();
  }
}
</script>

<template>
  <div class="discussion">
    <CommentsFilter />

    <div ref="sectionRef" class="discussion-body">
      <PagingWithSeparators
        v-if="paging"
        :paging="paging"
        :to="pagingTo"
        :use-query="true"
        query-key="number"
        :scroll-anchor="pagingAnchor"
      />

      <!-- Loading: the skeleton stands in before the first page only, so a
           refetch keeps the comments already on screen. -->
      <CommentSkeleton v-if="loading && !comments.length" />

      <!-- A failed load must not be presented as fake-empty. Shown only when
           there are no stale comments to keep on screen. -->
      <ErrorState
        v-else-if="failed && !comments.length"
        :message="LOAD_FAILURE"
        :retry="retryLoad"
      />

      <SecondaryText v-else-if="!comments.length" class="discussion-none">
        {{ emptyText }}
      </SecondaryText>

      <div v-else class="discussion-list">
        <CommentItem
          v-for="(comment, index) in comments"
          :key="comment.id"
          v-memo="[
            comment.id,
            comment.text,
            comment.likes?.length,
            comment.isRemoved,
            comment.modifiedUtc,
            isCompactLayout,
            filterState.search,
          ]"
          :comment="comment"
          :compact="isCompactLayout"
          :number="commentNumber(index)"
          :search-query="filterState.search"
          :fetch-edit-source="fetchEditSource"
          :submit-edit="submitEdit"
          :submit-delete="submitDelete"
          @like="like"
          @unlike="unlike"
          @warn="handleWarn"
        />
      </div>

      <PagingWithSeparators
        v-if="paging"
        :paging="paging"
        :to="pagingTo"
        :use-query="true"
        query-key="number"
        :scroll-anchor="pagingAnchor"
      />
    </div>

    <div class="discussion-composer">
      <template v-if="canComment">
        <BBCodeEditor
          ref="editorRef"
          v-model="newComment"
          context="common"
          placeholder="Написать комментарий..."
          :draft-key="draftKey"
          :disabled="sending"
          :min-height="100"
          :max-height="300"
          :resizable="true"
          :is-moderator="isModerator"
          @submit="handleSend"
        />
        <Button
          :loading="sending"
          :disabled="!newComment.trim()"
          @click="handleSend"
        >
          Отправить
        </Button>
      </template>

      <SecondaryText v-else-if="closedHint" class="discussion-hint">
        {{ closedHint }}
      </SecondaryText>
      <LoginPrompt v-else-if="!user" action="оставить комментарий" />
    </div>
  </div>
</template>

<style scoped lang="sass">
// Three blocks: filter bar, discussion body, composer. $medium between them
// and between the paging blocks and the list inside the body; the tighter
// $small rhythm belongs between the comments themselves.
.discussion
  display: flex
  flex-direction: column
  gap: $medium

.discussion-body
  display: flex
  flex-direction: column
  gap: $medium

.discussion-list
  display: flex
  flex-direction: column
  gap: $small

.discussion-none
  padding: $medium 0

.discussion-composer
  display: flex
  flex-direction: column
  gap: $small
  width: 100%

  :deep(.bbcode-editor-wrapper)
    width: 100%

.discussion-hint
  padding: $small
</style>

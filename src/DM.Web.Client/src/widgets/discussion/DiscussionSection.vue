<script setup lang="ts">
/**
 * The discussion, one section for the whole site.
 *
 * Four records carry comments — topic, game, blog, publication — and the four
 * endpoints behind them read the same query: text search, authors, period,
 * sort. What differs between the pages is only where the comments come from
 * and who may write, so that is what arrives as props, and everything a reader
 * sees is here: the filter bar, paging above and below the list, the comments,
 * the empty and failed states, the permalink and the composer. The composer is
 * the form of comments of the site (CommentComposer): the conversation of a
 * ticket writes in the same one.
 *
 * It exists because the pages had drifted into three shapes. The game and the
 * blog rendered a bare list with a single paging block and no search at all —
 * not because the server could not filter, it always could, but because those
 * pages never sent the params — and answered a failed load with a red line and
 * no way to retry. The comment numbering was spelled out three times, the
 * empty state in two wordings, and a copied permalink resolved on one page of
 * the three.
 */
import { computed, ref, watch } from "vue";
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
import RefusalNote from "@/shared/ui/Layout/RefusalNote.vue";
import { CommentSkeleton } from "@/shared/ui/Skeleton";
import { ErrorState } from "@/shared/ui/ErrorState";
import { useCommentHashScroll } from "@/shared/lib/composables/useScrollToElement";
import { useAuthStore } from "@/entities/user";
import {
  CommentComposer,
  CommentItem,
  useCommentWarnDialog,
} from "@/features/comment";
import { CommentsFilter, useCommentsFilter } from "@/features/comment-filter";
import { LoginPrompt } from "@/features/auth";

/** What a mutation answers with: null when the server took the change. */
type SubmitResult = { error: GeneralError | null };

/** Who shows a refused request: the two flags the api client reads by name. */
type RefusalFlags = { ownsRefusal?: boolean; ownsRateLimit?: boolean };

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
  /**
   * Deletes a comment. The flags come from the item's confirmation, which
   * prints a refusal in itself: passed on to the request, they keep the
   * interceptor from saying it a second time.
   */
  submitDelete: (id: string, options?: RefusalFlags) => Promise<SubmitResult>;
  /**
   * Likes a comment. The answer goes on to the item: a refused like leaves
   * the heart as it was, and the item is what says so.
   */
  like: (id: string) => Promise<SubmitResult>;
  /** Takes the like back; answers as like does. */
  unlike: (id: string) => Promise<SubmitResult>;
  /** Fetches a comment's raw BBCode source for the edit form. */
  fetchEditSource: (id: string) => Promise<ApiResult<Envelope<Comment>>>;
  /** Where the paging links point: the discussion's own route. */
  pagingTo: RouteLocationRaw;
  /** Composer draft key, built by composerDraftKey. */
  draftKey: string;
  /**
   * Whether the viewer may post right now: the record's `viewer.canComment`,
   * asked by the code that refuses the comment, so a form shown here is one
   * the server takes. False offers no form; what stands in its place is
   * `closedHint`, else the way in for a guest.
   */
  canComment: boolean;
  /**
   * Why the viewer may read this discussion and not write in it: a game
   * read-only to those outside it, the blacklist of the game or the blog. A
   * plaque where the form would be (RefusalNote), under the discussion, which
   * stays whole: the filter bar, the comments, and "Комментариев пока нет"
   * when there are none, that one grey, so a refusal never reads like the
   * empty line. It goes before the sign-in prompt, so a guest at a discussion
   * read-only to everybody outside the game is told so rather than invited to
   * a form that signing in would not open.
   */
  closedHint?: string;
  /**
   * The viewer is on the blacklist of the game or blog discussed here. The
   * composer is the caller's business (canComment); this reaches the
   * comments, where it takes "Редактировать" off the viewer's own.
   */
  viewerBlacklisted?: boolean;
}>();

const emit = defineEmits<{
  /** The first loaded page has rendered — the caller marks it read. */
  loaded: [];
}>();

/** One sentence for a failed discussion load, wherever it fails. */
const LOAD_FAILURE = "Не удалось загрузить комментарии";

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
  return props.paging.skip + index + 1;
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

useCommentHashScroll(() => props.comments);

// --- Composer ---
// The comment goes, then the page is read again so it stands in the list; only
// then does the form drop the draft. A refused one stays in the form, which
// says why under its button.
async function send(text: string): Promise<SubmitResult> {
  const result = await props.create(text);
  if (!result.error) await props.load(searchParams.value);
  return result;
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
            viewerBlacklisted,
          ]"
          :comment="comment"
          :compact="isCompactLayout"
          :number="commentNumber(index)"
          :search-query="filterState.search"
          :fetch-edit-source="fetchEditSource"
          :submit-edit="submitEdit"
          :submit-delete="submitDelete"
          :submit-like="like"
          :submit-unlike="unlike"
          :viewer-blacklisted="viewerBlacklisted"
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

    <!-- Nothing to offer and nothing to say (a ban): no block at all, so the
         list ends the section without an empty step under it. -->
    <div v-if="canComment || closedHint || !user" class="discussion-composer">
      <CommentComposer v-if="canComment" :send="send" :draft-key="draftKey" />
      <RefusalNote v-else-if="closedHint" class="discussion-hint">
        {{ closedHint }}
      </RefusalNote>
      <LoginPrompt v-else action="оставить комментарий" />
    </div>
  </div>
</template>

<style scoped lang="sass">
// Three blocks: filter bar, discussion body, composer. $medium between them
// and between the paging blocks and the list inside the body; the tighter
// $small rhythm belongs between the comments themselves. The plaque of a
// discussion closed to writing takes no margin of its own: it stands where
// the form would, spaced by the gap, its frame flush with the left edge.
// "Комментариев пока нет" keeps its $medium above, off the filter bar, and
// none below.
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
  padding-top: $medium

// The place of the form under the discussion: the form itself
// (CommentComposer, the editor across the column and "Отправить" under it at
// the left), or the plaque that stands where it would (a refusal, or the way
// in for a guest), across the column as the form would be.
.discussion-composer
  display: flex
  flex-direction: column
  align-items: flex-start
  width: 100%
</style>

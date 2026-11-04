<script setup lang="ts">
/** One discussion section for the site: what differs between the pages arrives as props. */
import { computed, ref, watch } from "vue";
import type { RouteLocationNamedRaw } from "vue-router";
import { storeToRefs } from "pinia";
import type {
  ApiResult,
  Comment,
  Envelope,
  SubmitResult,
  PagingInfo,
} from "@/shared/api/models/common";
import type { CommentsQuery, RequestOptions } from "@/shared/api";
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

const props = defineProps<{
  /** Comments of the current page, as the caller's store holds them. */
  comments: Comment[];
  /** Server paging of the current page, null while unknown. */
  paging: PagingInfo | null;
  /** A load is in flight. */
  loading: boolean;
  /** The last load failed; the sentence is the same everywhere and spelled here once. */
  failed: boolean;
  /** The record discussed: a change reloads, since the section stays mounted across records. */
  recordId?: string;
  /** Loads a page of the discussion with the filter, sort and paging query. */
  load: (query: CommentsQuery) => unknown;
  /** Posts a new comment; the answer decides whether the draft is dropped. */
  create: (text: string) => Promise<SubmitResult>;
  /** Saves an edited comment; the item keeps its editor open on a refusal. */
  submitEdit: (id: string, text: string) => Promise<SubmitResult>;
  /** Deletes a comment; the flags keep the interceptor from repeating the item's refusal. */
  submitDelete: (id: string, options?: RequestOptions) => Promise<SubmitResult>;
  /** Likes a comment; a refused like is the item's to say. */
  like: (id: string) => Promise<SubmitResult>;
  /** Takes the like back; answers as like does. */
  unlike: (id: string) => Promise<SubmitResult>;
  /** Fetches a comment's raw BBCode source for the edit form. */
  fetchEditSource: (id: string) => Promise<ApiResult<Envelope<Comment>>>;
  /** Where the paging links point: the discussion's own route. */
  pagingTo: RouteLocationNamedRaw;
  /** Composer draft key, built by composerDraftKey. */
  draftKey: string;
  /** The record's viewer.canComment, so a form shown here is one the server takes. */
  canComment: boolean;
  /** Why the viewer may read and not write: a plaque in the form's place, before the sign-in prompt. */
  closedHint?: string;
  /** The viewer is on the record's blacklist: his comments lose "Редактировать". */
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

// The record is in the key: the section stays mounted across a hop between two records.
const loadKey = computed(() =>
  JSON.stringify([props.recordId, searchParams.value]),
);

function loadPage() {
  return props.load(searchParams.value);
}

watch(
  loadKey,
  () => {
    if (props.recordId) loadPage();
  },
  { immediate: true },
);

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

const { warnComment } = useCommentWarnDialog((id) =>
  props.comments.find((c) => c.id === id),
);

// Paging scrolls the discussion back into view, not the record above it.
const sectionRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return sectionRef.value;
}

useCommentHashScroll(() => props.comments);

// The page is read again before the form drops the draft; a refused one stays in the form.
async function send(text: string): Promise<SubmitResult> {
  const result = await props.create(text);
  if (!result.error) await loadPage();
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

      <!-- Before the first page only: a refetch keeps the comments on screen -->
      <CommentSkeleton v-if="loading && !comments.length" />

      <!-- A failed load is not an empty one -->
      <ErrorState
        v-else-if="failed && !comments.length"
        :message="LOAD_FAILURE"
        :retry="loadPage"
      />

      <SecondaryText v-else-if="!comments.length" class="discussion-none">
        {{ emptyText }}
      </SecondaryText>

      <div v-else class="discussion-list">
        <CommentItem
          v-for="(comment, index) in comments"
          :key="comment.id"
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
          @warn="warnComment"
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

    <!-- Nothing to offer and nothing to say (a ban): no block at all -->
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
// $medium between the blocks and around the list, $small between the comments.
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

// The form under the discussion, or the plaque in its place, across the column.
.discussion-composer
  display: flex
  flex-direction: column
  align-items: flex-start
  width: 100%
</style>

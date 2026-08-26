<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useBoardsStore, forumApi } from "@/entities/forum";
import type { CommentId } from "@/entities/forum";
import { useUiStore } from "@/shared/stores/ui";
import { storeToRefs } from "pinia";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import { useRoute } from "vue-router";
import { CommentItem, useCommentWarnDialog } from "@/features/comment";
import { useCommentsFilter } from "@/features/comment-filter";
import { CommentSkeleton } from "@/shared/ui/Skeleton";
import { ErrorState } from "@/shared/ui/ErrorState";
import { useCommentHashScroll } from "@/shared/lib/composables/useScrollToElement";

const route = useRoute();
const boardsStore = useBoardsStore();
const { comments, commentsLoading, commentsError } = storeToRefs(boardsStore);
const { isCompactLayout } = storeToRefs(useUiStore());

// Filter setup
const { filterState, searchParams, hasActiveFilters } = useCommentsFilter();

// Generate a key for dependency tracking (triggers on any param change)
const paramsKey = computed(() => JSON.stringify(searchParams.value));

// Watch for filter changes and reload comments
watch(
  paramsKey,
  () => {
    if (boardsStore.selectedTopic) {
      boardsStore.searchComments(searchParams.value);
    }
  },
  { immediate: false },
);

useCommentHashScroll(() => comments.value?.resources);

// Calculate comment number based on paging
function getCommentNumber(index: number): number {
  if (!comments.value?.paging) return index + 1;
  return comments.value.paging.skip + index + 1;
}

// The item shows the refusal and keeps its editor open, so these hand the
// store's answer straight back to it.
function handleEdit(id: string, text: string) {
  return boardsStore.updateComment(id, text);
}

function handleDelete(id: string) {
  return boardsStore.deleteComment(id);
}

async function handleLike(id: string) {
  await boardsStore.likeComment(id);
}

async function handleUnlike(id: string) {
  await boardsStore.unlikeComment(id);
}

// Moderator warning (doc 4.2.4.1) — shared dialog wiring.
const { warnComment: handleWarn } = useCommentWarnDialog((id) =>
  comments.value?.resources.find((c) => c.id === id),
);

// Raw BBCode source fetch for the edit form (AuthorEdit audience).
const fetchEditSource = (id: string) =>
  forumApi.getCommentForUpdate(id as CommentId);

// Markup of a quotation of one comment, for the reply composer that lives on
// the page above this list.
const fetchQuoteSource = (id: string) =>
  forumApi.getCommentQuote(id as CommentId);

function retryLoad() {
  boardsStore.searchComments(searchParams.value);
}

// Paging scrolls the comments block (top paging + list) back into view
// instead of the page top — the topic header above is not re-shown.
const sectionRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return sectionRef.value;
}
</script>

<template>
  <div ref="sectionRef" class="comments-section">
    <!-- Paging at top -->
    <PagingWithSeparators
      v-if="comments && comments.paging"
      :paging="comments.paging"
      :to="{
        name: 'topic',
        params: { alias: route.params.alias, num: route.params.num },
      }"
      :use-query="true"
      query-key="number"
      :scroll-anchor="pagingAnchor"
    />

    <!-- Loading: the skeleton stands in before the first page only, so a refetch
         keeps the comments already on screen. The store holds the stale rows on
         purpose while it revalidates, and blanking them here threw that away: on
         the busiest comment surface of the site a change of page, filter or sort
         wiped the list, while the same change in a game or a blog left it in
         place. Same condition as DiscussionSection, which this list is the
         reference for. -->
    <CommentSkeleton v-if="commentsLoading && !comments?.resources.length" />

    <!-- Error state: a failed load must not be presented as fake-empty.
         Shown only when there are no stale comments to keep on screen. -->
    <ErrorState
      v-else-if="commentsError && !comments?.resources.length"
      message="Не удалось загрузить комментарии"
      :retry="retryLoad"
    />

    <!-- Empty state -->
    <secondary-text
      v-else-if="comments && !comments.resources.length"
      class="comments-none"
    >
      {{
        hasActiveFilters
          ? "Комментариев по заданным фильтрам не найдено"
          : "Комментариев пока нет"
      }}
    </secondary-text>

    <!-- Comments list -->
    <div v-else-if="comments" class="comments-list">
      <CommentItem
        v-for="(comment, index) in comments.resources"
        :key="comment.id"
        v-memo="[
          comment.id,
          comment.text,
          comment.likes.length,
          comment.isRemoved,
          comment.modifiedUtc,
          isCompactLayout,
          filterState.search,
        ]"
        :comment="comment"
        :compact="isCompactLayout"
        :number="getCommentNumber(index)"
        :search-query="filterState.search"
        :fetch-edit-source="fetchEditSource"
        :fetch-quote-source="fetchQuoteSource"
        :submit-edit="handleEdit"
        :submit-delete="handleDelete"
        @like="handleLike"
        @unlike="handleUnlike"
        @warn="handleWarn"
      />
    </div>

    <!-- Paging at bottom -->
    <PagingWithSeparators
      v-if="comments && comments.paging"
      :paging="comments.paging"
      :to="{
        name: 'topic',
        params: { alias: route.params.alias, num: route.params.num },
      }"
      :use-query="true"
      query-key="number"
      :scroll-anchor="pagingAnchor"
    />
  </div>
</template>

<style scoped lang="sass">
// Paging blocks sit $medium from the comment list; the tighter $small
// rhythm between the comments themselves lives on the inner wrapper.
.comments-section
  display: flex
  flex-direction: column
  gap: $medium

.comments-list
  display: flex
  flex-direction: column
  gap: $small

.comments-none
  padding: $medium 0
</style>

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

const { filterState, searchParams, hasActiveFilters } = useCommentsFilter();

// One key for any change of the search parameters
const paramsKey = computed(() => JSON.stringify(searchParams.value));

watch(paramsKey, () => {
  if (boardsStore.selectedTopic) {
    boardsStore.searchComments(searchParams.value);
  }
});

useCommentHashScroll(() => comments.value?.resources);

function getCommentNumber(index: number): number {
  if (!comments.value?.paging) return index + 1;
  return comments.value.paging.skip + index + 1;
}

// Moderator warning: the shared dialog wiring.
const { warnComment: handleWarn } = useCommentWarnDialog((id) =>
  comments.value?.resources.find((c) => c.id === id),
);

const fetchEditSource = (id: string) =>
  forumApi.getCommentForUpdate(id as CommentId);

function retryLoad() {
  boardsStore.searchComments(searchParams.value);
}

// Paging scrolls back to the comments, not to the topic header above.
const sectionRef = ref<HTMLElement | null>(null);
</script>

<template>
  <div ref="sectionRef" class="comments-section">
    <PagingWithSeparators
      v-if="comments && comments.paging"
      :paging="comments.paging"
      :to="{
        name: 'topic',
        params: { alias: route.params.alias, num: route.params.num },
      }"
      :use-query="true"
      query-key="number"
      :scroll-anchor="() => sectionRef"
    />

    <!-- Before the first page only: a refetch keeps the stale comments on screen,
         as DiscussionSection does. -->
    <CommentSkeleton v-if="commentsLoading && !comments?.resources.length" />

    <!-- A failed load is not an empty list; shown only without stale comments. -->
    <ErrorState
      v-else-if="commentsError && !comments?.resources.length"
      message="Не удалось загрузить комментарии"
      :retry="retryLoad"
    />

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
        :submit-edit="boardsStore.updateComment"
        :submit-delete="boardsStore.deleteComment"
        :submit-like="boardsStore.likeComment"
        :submit-unlike="boardsStore.unlikeComment"
        @warn="handleWarn"
      />
    </div>

    <PagingWithSeparators
      v-if="comments && comments.paging"
      :paging="comments.paging"
      :to="{
        name: 'topic',
        params: { alias: route.params.alias, num: route.params.num },
      }"
      :use-query="true"
      query-key="number"
      :scroll-anchor="() => sectionRef"
    />
  </div>
</template>

<style scoped lang="sass">
// The tighter rhythm between the comments lives on the inner wrapper.
.comments-section
  display: flex
  flex-direction: column
  gap: $medium

.comments-list
  display: flex
  flex-direction: column
  gap: $small

.comments-none
  padding-top: $medium
</style>

<script setup lang="ts">
import { computed, ref, watch, nextTick } from "vue";
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

// Scroll to the comment referenced by the URL hash (#comment-{id}) once the
// comments for the current page are rendered. Backs the permalink feature: a
// copied link lands the viewer on the exact comment.
async function scrollToHashComment() {
  const hash = route.hash;
  if (!hash || !hash.startsWith("#comment-")) return;
  if (!comments.value?.resources.length) return;

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
  () => [comments.value, route.hash] as const,
  () => scrollToHashComment(),
  { immediate: true, flush: "post" },
);

// Calculate comment number based on paging
function getCommentNumber(index: number): number {
  if (!comments.value?.paging) return index + 1;
  const offset =
    (comments.value.paging.current - 1) * comments.value.paging.size;
  return offset + index + 1;
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

    <!-- Loading state -->
    <CommentSkeleton v-if="commentsLoading" />

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

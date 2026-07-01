<script setup lang="ts">
import { computed, watch, nextTick } from "vue";
import { useBoardsStore } from "@/entities/forum";
import { useUiStore } from "@/shared/stores/ui";
import { storeToRefs } from "pinia";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import { useRoute } from "vue-router";
import { Comment } from "@/features/comment";
import { useCommentsFilter } from "@/features/comment-filter";
import { CommentSkeleton } from "@/shared/ui/Skeleton";

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

async function handleEdit(id: string, text: string) {
  await boardsStore.updateComment(id, text);
}

async function handleDelete(id: string) {
  await boardsStore.deleteComment(id);
}

async function handleLike(id: string) {
  await boardsStore.likeComment(id);
}

async function handleUnlike(id: string) {
  await boardsStore.unlikeComment(id);
}

function handleWarn(_id: string) {
  // TODO: Open warning modal (P5.5 - console.log removed)
}
</script>

<template>
  <div class="comments-section">
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
    />

    <!-- Loading state -->
    <CommentSkeleton v-if="commentsLoading" />

    <!-- Error state: a failed load must not be presented as fake-empty.
         Shown only when there are no stale comments to keep on screen. -->
    <secondary-text
      v-else-if="commentsError && !comments?.resources.length"
      class="comments-error"
    >
      Не удалось загрузить комментарии. Попробуйте обновить страницу.
    </secondary-text>

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
    <template v-else-if="comments">
      <Comment
        v-for="(comment, index) in comments.resources"
        :key="comment.id"
        v-memo="[
          comment.id,
          comment.text,
          comment.likes.length,
          isCompactLayout,
          filterState.search,
        ]"
        :comment="comment"
        :compact="isCompactLayout"
        :number="getCommentNumber(index)"
        :search-query="filterState.search"
        @edit="handleEdit"
        @delete="handleDelete"
        @like="handleLike"
        @unlike="handleUnlike"
        @warn="handleWarn"
      />
    </template>

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
    />
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"

.comments-section
  display: flex
  flex-direction: column
  gap: $small

.comments-loading
  text-align: center
  padding: $medium 0

.comments-none
  text-align: center
  padding: $medium 0

.comments-error
  text-align: center
  padding: $medium 0
  color: $accent-red
</style>

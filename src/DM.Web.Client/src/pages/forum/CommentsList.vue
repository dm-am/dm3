<script setup lang="ts">
import { computed, watch } from "vue";
import { useBoardsStore } from "@/entities/forum";
import { useUiStore } from "@/shared/stores/ui";
import { storeToRefs } from "pinia";
import Paging from "@/shared/ui/Paging/Paging.vue";
import { useRoute } from "vue-router";
import { Comment } from "@/features/comment";
import { useCommentsFilter } from "@/features/comment-filter";

const route = useRoute();
const boardsStore = useBoardsStore();
const { comments, commentsLoading } = storeToRefs(boardsStore);
const { isCompactLayout } = storeToRefs(useUiStore());

// Filter setup
const { searchParams, hasActiveFilters } = useCommentsFilter();

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
  { immediate: false }
);

// Calculate comment number based on paging
function getCommentNumber(index: number): number {
  if (!comments.value?.paging) return index + 1;
  const offset = (comments.value.paging.current - 1) * comments.value.paging.size;
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
    <Paging
      v-if="comments"
      :paging="comments.paging!"
      :to="{ name: 'topic', params: { alias: route.params.alias, num: route.params.num } }"
      :use-query="true"
      query-key="number"
    />

    <!-- Loading state -->
    <secondary-text v-if="commentsLoading" class="comments-loading">Загрузка...</secondary-text>

    <!-- Empty state -->
    <secondary-text
      v-else-if="comments && !comments.resources.length"
      class="comments-none"
    >
      {{ hasActiveFilters ? 'Комментариев по заданным фильтрам не найдено' : 'Комментариев пока нет' }}
    </secondary-text>

    <!-- Comments list -->
    <template v-else-if="comments">
      <Comment
        v-for="(comment, index) in comments.resources"
        :key="comment.id"
        v-memo="[comment.id, comment.text, comment.likesCount]"
        :comment="comment"
        :compact="isCompactLayout"
        :number="getCommentNumber(index)"
        @edit="handleEdit"
        @delete="handleDelete"
        @like="handleLike"
        @unlike="handleUnlike"
        @warn="handleWarn"
      />
    </template>

    <!-- Paging at bottom -->
    <Paging
      v-if="comments && comments.paging && comments.paging.pages > 1"
      :paging="comments.paging"
      :to="{ name: 'topic', params: { alias: route.params.alias, num: route.params.num } }"
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
</style>

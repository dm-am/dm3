<script setup lang="ts">
import { onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { discussionApi } from "@/entities/game";
import { usePaging } from "@/shared/lib/composables/usePaging";

const route = useRoute();
const router = useRouter();
// The page size the discussion asks the API for.
const { pageSize } = usePaging();

onMounted(async () => {
  // The guid: the first-unread endpoint does not take the public id.
  const gameId = route.params.id as string;

  function fallbackToComments() {
    router.replace({ name: "game-comments", params: { id: gameId } });
  }

  const { data } = await discussionApi.getFirstUnreadComment(gameId);
  const result = data?.resource;

  if (!result?.hasUnread) {
    fallbackToComments();
    return;
  }

  const page = Math.ceil(result.commentNumber / pageSize.value);

  router.replace({
    name: "game-comments",
    params: { id: gameId },
    query: page > 1 ? { number: String(page) } : {},
    hash: `#comment-${result.commentId}`,
  });
});
</script>

<template>
  <div class="loading-state">
    <p class="loading-text">Поиск непрочитанных комментариев</p>
  </div>
</template>

<style scoped lang="sass">
.loading-state
  display: flex
  flex-direction: column
  align-items: center
  justify-content: center
  padding: $large
  gap: $medium

.loading-text
  color: $text-muted
</style>

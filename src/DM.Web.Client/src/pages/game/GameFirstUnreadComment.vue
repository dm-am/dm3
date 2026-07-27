<script setup lang="ts">
import { onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { gameApi } from "@/entities/game";

// Comments page shows 20 comments (gameApi.getGameComments default page size)
const COMMENTS_PAGE_SIZE = 20;

const route = useRoute();
const router = useRouter();

onMounted(async () => {
  // Must be the game Guid (links pass game.id): the first-unread endpoint
  // binds the id strictly as a Guid
  const gameId = route.params.id as string;

  function fallbackToComments() {
    router.replace({ name: "game-comments", params: { id: gameId } });
  }

  try {
    const { data } = await gameApi.getFirstUnreadComment(gameId);
    const result = data?.resource;

    if (!result?.hasUnread) {
      // No comments at all (or request failed) - go to comments page
      fallbackToComments();
      return;
    }

    // Land on the page that contains the comment, then scroll to it
    // (game-comments route has no extra params - paging goes via query)
    const page = Math.ceil(result.commentNumber / COMMENTS_PAGE_SIZE);

    router.replace({
      name: "game-comments",
      params: { id: gameId },
      query: {
        ...(page > 1 ? { page: String(page) } : {}),
        scrollTo: result.commentId,
      },
    });
  } catch {
    // On unexpected error, just go to comments page
    fallbackToComments();
  }
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

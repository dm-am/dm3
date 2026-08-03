<script setup lang="ts">
import { onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { gameApi } from "@/entities/game";
import { usePaging } from "@/shared/lib/composables/usePaging";

const route = useRoute();
const router = useRouter();
// The discussion asks the API for the reader's own page size, so the page
// holding a given comment has to be computed with that same size.
const { commentsPerPage } = usePaging();

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

    // Land on the page holding the comment and let the discussion scroll to
    // it. The page is "?number=", the site-wide paging key — this used to
    // spell it "page", which the discussion never read, so every jump landed
    // on page one — and the comment is the same "#comment-{id}" permalink the
    // comment itself copies.
    const page = Math.ceil(result.commentNumber / commentsPerPage.value);

    router.replace({
      name: "game-comments",
      params: { id: gameId },
      query: page > 1 ? { number: String(page) } : {},
      hash: `#comment-${result.commentId}`,
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

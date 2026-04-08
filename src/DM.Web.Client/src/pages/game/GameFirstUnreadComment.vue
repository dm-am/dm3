<script setup lang="ts">
import { onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { gameApi } from "@/entities/game";

const route = useRoute();
const router = useRouter();

onMounted(async () => {
  const gameId = route.params.id as string;

  try {
    const { data } = await gameApi.getFirstUnreadComment(gameId);
    const result = data;

    if (!result?.hasUnread) {
      // No comments at all - go to comments page
      router.replace({ name: "game-comments", params: { id: gameId } });
      return;
    }

    // Navigate to the comments page with scroll target
    router.replace({
      name: "game-comments",
      params: {
        id: gameId,
        n: result.commentNumber,
      },
      query: { scrollTo: result.commentId },
    });
  } catch {
    // On error, just go to comments page
    router.replace({ name: "game-comments", params: { id: gameId } });
  }
});
</script>

<template>
  <div class="loading-state">
    <p class="loading-text">Поиск непрочитанных комментариев</p>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

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

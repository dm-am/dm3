<script setup lang="ts">
import { onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { gameApi } from "@/entities/game";

const route = useRoute();
const router = useRouter();

onMounted(async () => {
  const gameId = route.params.id as string;

  try {
    const { data } = await gameApi.getFirstUnreadPost(gameId);
    const result = data;

    if (!result?.hasUnread) {
      // No posts at all - go to rooms list
      router.replace({ name: "game-rooms", params: { id: gameId } });
      return;
    }

    // Navigate to the room with the post
    router.replace({
      name: "game-room",
      params: {
        id: gameId,
        roomId: result.roomId,
        n: result.postNumber,
      },
      query: { scrollTo: result.postId },
    });
  } catch {
    // On error, just go to rooms list
    router.replace({ name: "game-rooms", params: { id: gameId } });
  }
});
</script>

<template>
  <div class="loading-state">
    <p class="loading-text">Поиск непрочитанных постов...</p>
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

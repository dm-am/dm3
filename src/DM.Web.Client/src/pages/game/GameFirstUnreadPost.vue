<script setup lang="ts">
import { onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { gameApi } from "@/entities/game";

// Room pages show 20 posts (gameApi.getPosts default page size)
const POSTS_PAGE_SIZE = 20;

const route = useRoute();
const router = useRouter();

onMounted(async () => {
  // Must be the game Guid (links pass game.id): the first-unread endpoint
  // binds the id strictly as a Guid
  const gameId = route.params.id as string;

  // The game's own page: the site has no rooms-list page, so a reader with
  // nothing unread lands on the game information view.
  function fallbackToGame() {
    router.replace({ name: "game", params: { id: gameId } });
  }

  try {
    const { data } = await gameApi.getFirstUnreadPost(gameId);
    const result = data?.resource;

    if (!result?.hasUnread) {
      // No posts at all (or request failed) - go to the game page
      fallbackToGame();
      return;
    }

    // The game-room route addresses rooms by number while the API returns
    // the room id - resolve the number through the rooms list
    const { data: roomsData } = await gameApi.getRooms(gameId);
    const room = roomsData?.resources.find((r) => r.id === result.roomId);

    if (!room) {
      fallbackToGame();
      return;
    }

    // Land on the page that contains the post, then scroll to it
    const page = Math.ceil(result.postNumber / POSTS_PAGE_SIZE);

    router.replace({
      name: "game-room",
      params: { id: gameId, num: room.roomNumber },
      query: {
        ...(page > 1 ? { page: String(page) } : {}),
        scrollTo: result.postId,
      },
    });
  } catch {
    // On unexpected error, just go to the game page
    fallbackToGame();
  }
});
</script>

<template>
  <div class="loading-state">
    <p class="loading-text">Поиск непрочитанных постов</p>
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

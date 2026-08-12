<script setup lang="ts">
import { onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { gameApi } from "@/entities/game";
import { usePaging } from "@/shared/lib/composables/usePaging";

const route = useRoute();
const router = useRouter();
// The room asks the API for the reader's own page size, so the page holding a
// given post has to be counted in that same size.
const { postsPerPage } = usePaging();

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

    // Land on the page holding the post and let the room scroll to it. The
    // page is "?number=", the site-wide paging key: this used to spell it
    // "page", which the room never reads, so every jump landed on page one.
    const page = Math.ceil(result.postNumber / postsPerPage.value);

    router.replace({
      name: "game-room",
      params: { id: gameId, num: room.roomNumber },
      query: {
        ...(page > 1 ? { number: String(page) } : {}),
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

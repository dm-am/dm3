<script setup lang="ts">
import { onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { postApi, roomApi } from "@/entities/game";
import { usePaging } from "@/shared/lib/composables/usePaging";

const route = useRoute();
const router = useRouter();
// The page size the room asks the API for.
const { pageSize } = usePaging();

onMounted(async () => {
  // The guid: the first-unread endpoint does not take the public id.
  const gameId = route.params.id as string;

  // The site has no list of rooms to fall back to.
  function fallbackToGame() {
    router.replace({ name: "game", params: { id: gameId } });
  }

  const { data } = await postApi.getFirstUnreadPost(gameId);
  const result = data?.resource;

  if (!result?.hasUnread) {
    fallbackToGame();
    return;
  }

  // The route addresses a room by number, the answer by id.
  const { data: roomsData } = await roomApi.getRooms(gameId);
  const room = roomsData?.resources.find((r) => r.id === result.roomId);

  if (!room) {
    fallbackToGame();
    return;
  }

  const page = Math.ceil(result.postNumber / pageSize.value);

  router.replace({
    name: "game-room",
    params: { id: gameId, num: room.roomNumber },
    query: {
      ...(page > 1 ? { number: String(page) } : {}),
      scrollTo: result.postId,
    },
  });
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

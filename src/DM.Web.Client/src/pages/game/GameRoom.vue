<script setup lang="ts">
import { computed, ref } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import {
  useZoneMarker,
  useZoneSection,
} from "@/shared/lib/composables/useZoneSection";
import { useScrollToElement } from "@/shared/lib/composables/useScrollToElement";
import { parsePageNumber } from "@/shared/lib/filters/utils";
import { RoomPostFilter, useRoomPostFilter } from "@/features/room-post-filter";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { BackLink } from "@/shared/ui/Layout";
import { ErrorState } from "@/shared/ui/ErrorState";
import { GamePost } from "@/widgets/game-post";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import { useRoomNotFound } from "./room/useRoomNotFound";
import { useRoomOpening } from "./room/useRoomOpening";
import PostComposer from "./room/PostComposer.vue";

const route = useRoute();
const gameStore = useGameDetailsStore();
const {
  game,
  rooms,
  currentRoom,
  posts,
  postsPaging,
  postsLoading,
  postsError,
  characters,
} = storeToRefs(gameStore);

const { filterState, apiFilter, hasActiveFilters, isFilteredView } =
  useRoomPostFilter();

const gameId = computed(() => route.params.id as string);
const roomNum = computed(() => parseInt(route.params.num as string, 10));
const page = computed(() => parsePageNumber(route.query.number) ?? 1);

// By number off the list, which loadRooms replaces after a pendency change.
const room = computed(
  () =>
    rooms.value.find((r) => r.roomNumber === roomNum.value) ??
    currentRoom.value,
);

const { ensureRooms, roomRefused } = useRoomNotFound({ gameId, roomNum });

// The room's name is data, so it cannot sit in meta.section.
useZoneSection(() => room.value?.title);
useZoneMarker(() => (room.value?.isArchived ? "архив" : undefined));

// The game-wide unread link lands here with ?scrollTo= and its page named.
const postsLoaded = computed(
  () => posts.value.length > 0 && !postsLoading.value,
);
useScrollToElement(postsLoaded);

// The position in the whole room; counted locally only in the room's own order.
function postNumber(index: number): number | undefined {
  const post = posts.value[index];
  if (post?.number) return post.number;
  if (isFilteredView.value) return undefined;
  return (postsPaging.value?.skip ?? 0) + index + 1;
}

const postsListRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return postsListRef.value;
}

const pagingTarget = computed(() => ({
  name: "game-room",
  params: { id: game.value?.publicId, num: roomNum.value },
}));

const { locating, reloadView } = useRoomOpening({
  gameId,
  roomNum,
  page,
  apiFilter,
  isFilteredView,
  ensureRooms,
  roomRefused,
});

const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Постов по заданным фильтрам не найдено"
    : "В этой комнате пока нет постов",
);

// The composer, the turn controls and the filter all read the characters.
gameStore.loadCharacters(gameId.value);

// The rooms carry the pendencies a post fulfils.
async function refreshRoom() {
  await gameStore.loadRooms(gameId.value);
  await reloadView();
}
</script>

<template>
  <div class="game-room">
    <BackLink
      :to="{ name: 'game', params: { id: gameId } }"
      label="Назад к игре"
    />

    <RoomPostFilter class="filters" :characters="characters" />

    <ErrorState
      v-if="postsError"
      class="error-banner"
      :message="postsError"
      :retry="reloadView"
    />

    <GamePostSkeleton
      v-if="(postsLoading || locating) && posts.length === 0"
      :count="3"
      :show-navigation="false"
    />

    <div v-else-if="!postsError && posts.length === 0" class="posts-empty">
      <secondary-text>{{ emptyText }}</secondary-text>
    </div>

    <template v-else>
      <PagingWithSeparators
        v-if="postsPaging"
        :paging="postsPaging"
        :to="pagingTarget"
        use-query
        :scroll-anchor="pagingAnchor"
      />

      <div ref="postsListRef" class="posts-list">
        <game-post
          v-for="(post, index) in posts"
          :key="post.id"
          :post="post"
          :data-id="post.id"
          :number="postNumber(index)"
          :search-query="filterState.search"
          :room="room"
          editable
          @deleted="refreshRoom"
        />
      </div>

      <PagingWithSeparators
        v-if="postsPaging"
        :paging="postsPaging"
        :to="pagingTarget"
        use-query
        :scroll-anchor="pagingAnchor"
      />
    </template>

    <PostComposer :room="room" :game-id="gameId" @posted="refreshRoom" />
  </div>
</template>

<style scoped lang="sass">
.game-room
  min-height: $grid-step * 50

.posts-empty
  padding: $big

.filters
  margin-bottom: $medium

.error-banner
  margin-bottom: $medium

// A line of the text apart, as on DM2; GamePostSkeleton keeps the same step.
.posts-list
  display: flex
  flex-direction: column
  gap: 5 * $grid-step

// The arrival pulse fades into the card's own fill, not to transparent: a
// keyframe with no end takes the element's own value.
.posts-list :deep(.post-card.highlight-unread)
  animation-name: card-arrival

@keyframes card-arrival
  from
    background-color: $bg-highlight-yellow
    box-shadow: 0 0 0 $minor $bg-highlight-yellow
</style>

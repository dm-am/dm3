<script setup lang="ts">
/**
 * "Оцененные посты" of one game — the posts of this game that got at least one
 * review.
 *
 * The list itself is widgets/rated-posts, the same one the profile subpages
 * and the moderation worklist draw; this page only says which posts it is
 * about. It used to be a list of its own: one fixed page of a hundred posts,
 * no paging, and a room filter computed on the client over that truncated
 * hundred — an answer about the posts that happened to be fetched rather than
 * about the game. The room filter is gone with it: the posts endpoint takes no
 * room, so it cannot be asked honestly until PostsQuery has one.
 */
import { computed } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import type { RatedPostsScope } from "@/entities/game";
import { RatedPostsList } from "@/widgets/rated-posts";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import LeadText from "@/shared/ui/Layout/LeadText.vue";

const route = useRoute();
const { game } = storeToRefs(useGameDetailsStore());

// The endpoint takes the game's guid; the route carries the public id, so the
// loaded game is the source and the param only the fallback.
const gameId = computed(() => game.value?.id ?? (route.params.id as string));

const scope = computed<RatedPostsScope>(() => ({
  kind: "game",
  gameId: gameId.value,
}));

const pagingTo = computed(() => ({
  name: "game-post-reviews" as const,
  params: { id: route.params.id as string },
}));
</script>

<template>
  <div class="game-post-reviews">
    <BlockTitle>Оцененные посты</BlockTitle>
    <LeadText v-once>
      Посты этой игры, которые получили хотя бы одну оценку
    </LeadText>

    <RatedPostsList
      :scope="scope"
      :paging-to="pagingTo"
      empty-text="В этой игре пока нет оцененных постов"
      navigation-level="room"
    />
  </div>
</template>

<style scoped lang="sass">
.game-post-reviews
  min-height: $grid-step * 50
</style>

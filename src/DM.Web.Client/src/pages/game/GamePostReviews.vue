<script setup lang="ts">
// The rated posts of one game: the list is shared, this page sets its scope.
import { computed } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore } from "@/entities/game";
import type { RatedPostsScope } from "@/entities/game";
import { RatedPostsList } from "@/widgets/rated-posts";
import LeadText from "@/shared/ui/Layout/LeadText.vue";

const route = useRoute();
const { game } = storeToRefs(useGameDetailsStore());

// The endpoint takes the guid, the route carries the public id.
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

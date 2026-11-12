<template>
  <section>
    <BlockTitle>Последний оцененный пост</BlockTitle>
    <GamePost
      v-if="store.latestRated"
      :post="store.latestRated"
      show-navigation
      truncatable
    />
    <ErrorState
      v-else-if="store.latestError"
      :message="store.latestError"
      :retry="() => store.fetchLatestRated(true)"
    />
    <SecondaryText v-else-if="store.latestLoaded">
      Оцененных постов пока нет
    </SecondaryText>
    <!-- Keeps the discovery link at the bottom from hopping. -->
    <GamePostSkeleton v-else />
  </section>
</template>

<script setup lang="ts">
import { GamePost } from "@/widgets/game-post";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import { ErrorState } from "@/shared/ui/ErrorState";
import { useRatedPostsStore } from "@/entities/game";
import { onMounted } from "vue";

const store = useRatedPostsStore();

// Both requests at once: the latest one does not need the best post's id, the
// store picks the post that does not repeat it once both answers are in.
onMounted(() => {
  store.fetchBestOfWeek();
  store.fetchLatestRated();
});
</script>

<style scoped lang="sass">
section
  margin-bottom: $medium
</style>

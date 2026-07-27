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
      :retry="() => store.fetchLatestRated(store.bestOfWeek?.id, true)"
    />
    <SecondaryText v-else-if="store.latestLoaded">
      Оцененных постов пока нет
    </SecondaryText>
    <!-- Skeleton: same shape as BestWeeklyPost (shared primitive). Keeps
         the discovery link at the bottom of HomePage from hopping. -->
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
import { onMounted, watch } from "vue";

const store = useRatedPostsStore();

// Wait for bestOfWeek to resolve first so its id is known before requesting
// the latest rated post — this is how the two blocks avoid rendering the
// identical post twice in a row. BestWeeklyPost.vue also calls
// fetchBestOfWeek() on mount; if that request is already in flight (the
// store's own loadingBest guard), calling it again here is a no-op, so we
// wait for bestLoaded to flip instead of relying on this call's promise.
onMounted(async () => {
  if (!store.bestLoaded) {
    store.fetchBestOfWeek();
    await new Promise<void>((resolve) => {
      const stop = watch(
        () => store.bestLoaded,
        (loaded) => {
          if (loaded) {
            stop();
            resolve();
          }
        },
      );
    });
  }
  store.fetchLatestRated(store.bestOfWeek?.id);
});
</script>

<style scoped lang="sass">
section
  margin-bottom: $medium
</style>

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
import { onMounted, watch } from "vue";

const store = useRatedPostsStore();

// The latest rated post must not repeat the best of the week, so its id is
// awaited first. A call while the store's request is in flight returns at
// once, hence the wait on bestLoaded rather than on the call's promise.
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

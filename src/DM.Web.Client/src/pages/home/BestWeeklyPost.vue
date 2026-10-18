<template>
  <section>
    <BlockTitle>Лучший пост недели</BlockTitle>
    <GamePost
      v-if="store.bestOfWeek"
      :post="store.bestOfWeek"
      show-navigation
      truncatable
    />
    <ErrorState
      v-else-if="store.bestError"
      :message="store.bestError"
      :retry="() => store.fetchBestOfWeek(true)"
    />
    <SecondaryText v-else-if="store.bestLoaded">
      Оцененных постов за эту неделю пока нет
    </SecondaryText>
    <!-- Skeleton: reserves the place of one truncatable post card
         (navigation breadcrumb + post card whose text is clamped to the
         height of its author's column, GamePost.vue). Without it the
         "Последний оцененный пост" block below jumps up when the fetch
         resolves. -->
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

onMounted(() => store.fetchBestOfWeek());
</script>

<style scoped lang="sass">
section
  margin-bottom: $medium
</style>

<template>
  <section>
    <BlockTitle>Последний оцененный пост</BlockTitle>
    <GamePost
      v-if="store.latestRated"
      :post="store.latestRated"
      show-navigation
      truncatable
    />
    <SecondaryText v-else-if="store.latestLoaded">
      Нет оцененных постов
    </SecondaryText>
    <!-- Skeleton: same shape as BestWeeklyPost (shared primitive). Keeps
         the discovery link at the bottom of HomePage from hopping. -->
    <GamePostSkeleton v-else />
  </section>
</template>

<script setup lang="ts">
import { GamePost } from "@/pages/game";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import GamePostSkeleton from "./GamePostSkeleton.vue";
import { useRatedPostsStore } from "@/entities/game";
import { onMounted } from "vue";

const store = useRatedPostsStore();

onMounted(() => store.fetchLatestRated());
</script>

<style scoped lang="sass">
section
  margin-bottom: 1rem
</style>

<template>
  <section>
    <BlockTitle>Лучший пост недели</BlockTitle>
    <GamePost v-if="store.bestOfWeek" :post="store.bestOfWeek" show-navigation />
    <SecondaryText v-else-if="store.loaded">
      Нет оцененных постов за эту неделю
    </SecondaryText>
  </section>
</template>

<script setup lang="ts">
import { GamePost } from "@/pages/game";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import { useFeaturedPostsStore } from "@/entities/game";
import { onMounted } from "vue";

const store = useFeaturedPostsStore();

onMounted(() => store.fetchBestOfWeek());
</script>

<style scoped lang="sass">
section
  margin-bottom: 1rem
</style>

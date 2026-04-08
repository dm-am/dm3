<template>
  <section>
    <BlockTitle>Последний оцененный пост</BlockTitle>
    <GamePost v-if="store.lastWithPlus" :post="store.lastWithPlus" show-navigation />
    <SecondaryText v-else-if="store.loaded">
      Нет оцененных постов
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

onMounted(() => store.fetchLatestFeatured());
</script>

<style scoped lang="sass">
section
  margin-bottom: 1rem
</style>

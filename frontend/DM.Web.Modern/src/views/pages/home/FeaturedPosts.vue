<template>
  <section>
    <block-title>Лучший пост за неделю</block-title>
    <featured-post-card v-if="store.bestOfWeek" :post="store.bestOfWeek" />
    <secondary-text v-else-if="store.loaded">Нет оцененных постов за эту неделю</secondary-text>
  </section>

  <section>
    <block-title>Последний оцененный пост</block-title>
    <featured-post-card v-if="store.lastWithPlus" :post="store.lastWithPlus" />
    <secondary-text v-else-if="store.loaded">Нет оцененных постов</secondary-text>
  </section>
</template>

<script setup lang="ts">
import FeaturedPostCard from "@/views/layout/sidebar/FeaturedPostCard.vue";
import SecondaryText from "@/components/layout/SecondaryText.vue";
import { useFeaturedPostsStore } from "@/stores/featuredPosts";
import { onMounted } from "vue";

const store = useFeaturedPostsStore();

onMounted(() => store.fetch());
</script>

<style scoped lang="sass">
section
  margin-bottom: 1rem
</style>

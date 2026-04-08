<script setup lang="ts">
import { computed, onMounted } from "vue";
import { storeToRefs } from "pinia";
import { useBoardsStore } from "@/entities/forum";
import { Topic } from "@/features/topic";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import dayjs from "dayjs";

const NEWS_AGE_DAYS = 7;

const store = useBoardsStore();
const { news: allNews } = storeToRefs(store);

// Show news from last week, or just the latest one if none are recent
const news = computed(() => {
  if (!allNews.value?.length) return allNews.value;

  const weekAgo = dayjs().subtract(NEWS_AGE_DAYS, "day");
  const recentNews = allNews.value.filter((n) =>
    dayjs(n.createdUtc).isAfter(weekAgo),
  );

  // If no recent news, show just the latest one
  if (recentNews.length === 0) {
    return [allNews.value[0]];
  }

  return recentNews;
});

onMounted(() => {
  store.fetchNews();
});
</script>

<template>
  <block-title>Последние новости</block-title>

  <div v-if="news?.length" class="news-list">
    <Topic
      v-for="article in news"
      :key="article.id"
      :topic="article"
      :truncatable="true"
      :max-height="150"
    />
  </div>
  <secondary-text v-else-if="news !== null">Нет новостных топиков</secondary-text>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"

.news-list
  display: flex
  flex-direction: column
  gap: $medium
</style>

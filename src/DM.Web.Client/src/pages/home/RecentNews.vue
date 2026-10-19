<script setup lang="ts">
import { computed, onMounted } from "vue";
import { storeToRefs } from "pinia";
import { NEWS_WIDGET_LIMIT, useBoardsStore } from "@/entities/forum";
import { TopicView } from "@/features/topic";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ErrorState } from "@/shared/ui/ErrorState";
import dayjs from "dayjs";

const NEWS_AGE_DAYS = 7;

const store = useBoardsStore();
const { news: allNews, newsError } = storeToRefs(store);

// The last week's news, or the latest one if none is recent. The cap is
// applied here too: the block's height must not depend on the server
// honouring the page size.
const news = computed(() => {
  if (!allNews.value?.length) return allNews.value;

  const weekAgo = dayjs().subtract(NEWS_AGE_DAYS, "day");
  const recentNews = allNews.value.filter((n) =>
    dayjs(n.createdUtc).isAfter(weekAgo),
  );

  if (recentNews.length === 0) {
    return [allNews.value[0]];
  }

  return recentNews.slice(0, NEWS_WIDGET_LIMIT);
});

onMounted(() => {
  store.fetchNews();
});
</script>

<template>
  <block-title>Последние новости</block-title>

  <div v-if="news?.length" class="news-list">
    <TopicView
      v-for="article in news"
      :key="article.id"
      :topic="article"
      :truncatable="true"
      :max-height="150"
      :preview-only="true"
    />
  </div>
  <ErrorState
    v-else-if="newsError"
    message="Не удалось загрузить новости"
    :retry="() => store.fetchNews(true)"
  />
  <!-- Mirrors TopicView so the page below does not jump; null means not loaded yet. -->
  <div v-else-if="news === null" class="news-skeleton" aria-hidden="true">
    <div class="skeleton-topic">
      <div class="skeleton-title-row">
        <div class="skeleton-title" />
      </div>
      <div class="skeleton-description">
        <div class="skeleton-line wide" />
        <div class="skeleton-line" />
        <div class="skeleton-line" />
        <div class="skeleton-line" />
        <div class="skeleton-line narrow" />
      </div>
      <div class="skeleton-footer">
        <div class="skeleton-meta" />
      </div>
    </div>
  </div>
  <secondary-text v-else>Новостных топиков пока нет</secondary-text>
  <p class="all-news-link">
    С остальными новостями можно ознакомиться
    <router-link to="/forum/news"
      ><strong>в новостном разделе форума</strong></router-link
    >, а с полной статистикой сайта
    <router-link :to="{ name: 'site-statistics' }"
      ><strong>на отдельной странице</strong></router-link
    >.
  </p>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

.news-list
  display: flex
  flex-direction: column
  gap: $medium

.all-news-link
  margin: $medium 0 0
  color: $text

// One collapsed news card of TopicView, at the 150px clamp RecentNews passes.
.news-skeleton
  display: flex
  flex-direction: column

.skeleton-topic
  padding: $medium
  border: 1px dashed $border
  background-color: $bg-element

.skeleton-title-row
  padding-bottom: $small
  border-bottom: 1px dashed $border
  margin-bottom: $small

.skeleton-title
  +skeleton-shimmer
  // h3.topic-title: 16px at line-height 1.3
  height: 21px
  width: 40%

.skeleton-description
  // The budget of TruncatedContent at :max-height="150"
  display: flex
  flex-direction: column
  gap: 6px
  min-height: 150px

.skeleton-line
  +skeleton-shimmer
  height: 14px
  width: 100%

  &.wide
    width: 95%

  &.narrow
    width: 60%

.skeleton-footer
  // Stands in for the author line of .topic-footer
  margin-top: $small

.skeleton-meta
  +skeleton-shimmer
  height: 14px
  width: 70%
</style>

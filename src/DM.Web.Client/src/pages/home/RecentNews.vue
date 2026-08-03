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

// Show news from last week, or just the latest one if none are recent.
// The age window decides which news qualify, NEWS_WIDGET_LIMIT decides how many
// are shown: without the cap the block grew a card for every fresh topic and
// its height moved with the calendar. The cap is applied here as well as in the
// request because the block's shape must not depend on the server honouring a
// page size.
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
  <!-- Loading placeholder. Mirrors the real <TopicView> shape pixel-for-pixel
       so the home page below (Best post, Featured post, discovery link)
       does not jump when the news array arrives. Structure matches
       TopicView.vue: dashed card, title with underline, description
       (TruncatedContent) + footer. `news === null` means "not loaded
       yet" (fetch errors land in the branch above); empty array is the
       genuine "no news". -->
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
@import "@/assets/styles/Skeleton"

.news-list
  display: flex
  flex-direction: column
  gap: $medium

.all-news-link
  margin: $medium 0 0
  color: $text
  a
    color: $link
    &:hover
      color: $link-hover

// Skeleton for one collapsed news Topic card. Pixel-matches the real
// <TopicView> card in TopicView.vue:
//   - outer dashed-bordered box with $medium padding
//   - h3 title with $small padding-bottom + 1px dashed underline +
//     $small bottom margin
//   - description (clamped to the 150px `:max-height` prop passed by
//     RecentNews) + footer margin-top $small
// Total budgeted height: 16 padding + (~20 title + 8 + 1 + 8) + 175
// description+footer + 16 padding ≈ 244px, matching the real card so
// the "С остальными новостями..." discovery link below stays pinned.
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
  // Matches h3.topic-title: $font-size (16px) × ~1.3 line-height = 21px
  height: 21px
  width: 40%

.skeleton-description
  // Reserves the same budget as <TruncatedContent :max-height="150">
  // in RecentNews: 5 shimmer lines × 14px + 4 × 6px gaps = 94px + the
  // +56px of unused headroom the 150px clamp typically leaves visible.
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
  // Matches .topic-footer { margin-top: $small } with a single author-info
  // shimmer line to stand in for "Автор: X [role] [online], дата |
  // Комментарии: N".
  margin-top: $small

.skeleton-meta
  +skeleton-shimmer
  height: 14px
  width: 70%
</style>

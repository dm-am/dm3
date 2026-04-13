<template>
  <div
    v-if="loaded"
    class="site-stats"
    role="region"
    aria-label="Статистика сайта"
  >
    <div class="stat-row">
      Пользователей: {{ formatNumber(users.value) }}
      <span class="bracket">[</span
      ><span class="delta">+{{ users.todayDelta }}</span
      ><span class="bracket">]</span>, онлайн:
      <span class="online">{{ online }}</span>
    </div>
    <div class="stat-row">
      Персонажей: {{ formatNumber(characters.value) }}
      <span class="bracket">[</span
      ><span class="delta">+{{ characters.todayDelta }}</span
      ><span class="bracket">]</span>
    </div>
    <div class="stat-row">
      Игр: {{ formatNumber(games.value) }} <span class="bracket">[</span
      ><span class="delta">+{{ games.todayDelta }}</span
      ><span class="bracket">]</span>, постов: {{ formatNumber(posts.value) }}
      <span class="bracket">[</span
      ><span class="delta">+{{ posts.todayDelta }}</span
      ><span class="bracket">]</span>
    </div>
    <div class="stat-row">
      Блогов: {{ formatNumber(blogs.value) }} <span class="bracket">[</span
      ><span class="delta">+{{ blogs.todayDelta }}</span
      ><span class="bracket">]</span>, публикаций:
      {{ formatNumber(publications.value) }} <span class="bracket">[</span
      ><span class="delta">+{{ publications.todayDelta }}</span
      ><span class="bracket">]</span>
    </div>
  </div>
  <!-- Skeleton placeholder while loading. Matches .site-stats
       dimensions exactly: 4 rows at $secondary-font-size × line-height
       1.4, with the text line occupying ~0.75em of each row's height
       so nothing shifts vertically when the numbers arrive. -->
  <div v-else class="site-stats-skeleton" aria-hidden="true">
    <div class="skeleton-row"><span class="skeleton-text" /></div>
    <div class="skeleton-row"><span class="skeleton-text skeleton-short" /></div>
    <div class="skeleton-row"><span class="skeleton-text skeleton-medium" /></div>
    <div class="skeleton-row"><span class="skeleton-text" /></div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted } from "vue";
import { storeToRefs } from "pinia";
import { useStatisticsStore } from "@/shared/stores/statistics";

const store = useStatisticsStore();
const { loaded, online, users, characters, games, posts, blogs, publications } =
  storeToRefs(store);

function formatNumber(value: number): string {
  return value.toLocaleString("ru-RU");
}

// Handle tab visibility changes - stop polling when tab is not visible
function handleVisibilityChange() {
  if (document.hidden) {
    store.stopPolling();
  } else {
    store.startPolling();
  }
}

onMounted(() => {
  store.startPolling();
  document.addEventListener("visibilitychange", handleVisibilityChange);
});

onUnmounted(() => {
  store.stopPolling();
  document.removeEventListener("visibilitychange", handleVisibilityChange);
});
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.site-stats
  font-size: $secondary-font-size
  line-height: 1.4
  text-align: left
  color: $text
  white-space: nowrap

.bracket
  color: $text-muted

.delta
  color: $accent-green

.online
  color: $accent-green

// Skeleton — same external box model as .site-stats so no vertical or
// horizontal shift happens when the loaded numbers replace it.
// Each row is exactly 1.4em tall (matches line-height) and the shimmer
// bar takes ~65% of that height, vertically centred via flex.
.site-stats-skeleton
  font-size: $secondary-font-size
  line-height: 1.4
  text-align: left

.skeleton-row
  display: flex
  align-items: center
  height: 1.4em

.skeleton-text
  display: inline-block
  width: 210px
  height: 0.85em
  background: linear-gradient(90deg, $bg-element 25%, $bg-element-hover 50%, $bg-element 75%)
  background-size: 200% 100%
  animation: skeleton-shimmer 1.5s ease-in-out infinite
  border-radius: 2px

  &.skeleton-short
    width: 140px

  &.skeleton-medium
    width: 180px

  @media (prefers-reduced-motion: reduce)
    animation: none
    background: $bg-element-hover

@keyframes skeleton-shimmer
  0%
    background-position: 200% 0
  100%
    background-position: -200% 0
</style>

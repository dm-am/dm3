<template>
  <div
    v-if="loaded"
    class="site-stats"
    role="region"
    aria-label="Статистика сайта"
  >
    <div class="stat-row">
      Пользователей: {{ statValue(users.value) }}
      <span class="bracket" aria-hidden="true">[</span
      ><span class="delta">{{ deltaValue(users.todayDelta) }}</span
      ><span class="bracket" aria-hidden="true">]</span>, online:
      <span class="online">{{ statsUnavailable ? "n/a" : online }}</span>
    </div>
    <div class="stat-row">
      Персонажей: {{ statValue(characters.value) }}
      <span class="bracket" aria-hidden="true">[</span
      ><span class="delta">{{ deltaValue(characters.todayDelta) }}</span
      ><span class="bracket" aria-hidden="true">]</span>
    </div>
    <div class="stat-row">
      Игр: {{ statValue(games.value) }}
      <span class="bracket" aria-hidden="true">[</span
      ><span class="delta">{{ deltaValue(games.todayDelta) }}</span
      ><span class="bracket" aria-hidden="true">]</span>, постов:
      {{ statValue(posts.value) }}
      <span class="bracket" aria-hidden="true">[</span
      ><span class="delta">{{ deltaValue(posts.todayDelta) }}</span
      ><span class="bracket" aria-hidden="true">]</span>
    </div>
    <div class="stat-row">
      Блогов: {{ statValue(blogs.value) }}
      <span class="bracket" aria-hidden="true">[</span
      ><span class="delta">{{ deltaValue(blogs.todayDelta) }}</span
      ><span class="bracket" aria-hidden="true">]</span>, публикаций:
      {{ statValue(publications.value) }}
      <span class="bracket" aria-hidden="true">[</span
      ><span class="delta">{{ deltaValue(publications.todayDelta) }}</span
      ><span class="bracket" aria-hidden="true">]</span>
    </div>
  </div>
  <!-- Skeleton placeholder while loading. Matches .site-stats
       dimensions exactly: 4 rows at $secondary-font-size × line-height
       1.4, with the text line occupying ~0.75em of each row's height
       so nothing shifts vertically when the numbers arrive. -->
  <div v-else class="site-stats-skeleton" aria-hidden="true">
    <div class="skeleton-row"><span class="skeleton-text" /></div>
    <div class="skeleton-row">
      <span class="skeleton-text skeleton-short" />
    </div>
    <div class="skeleton-row">
      <span class="skeleton-text skeleton-medium" />
    </div>
    <div class="skeleton-row"><span class="skeleton-text" /></div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted } from "vue";
import { storeToRefs } from "pinia";
import { useStatisticsStore } from "@/shared/stores/statistics";

const store = useStatisticsStore();
const {
  stats,
  loaded,
  online,
  users,
  characters,
  games,
  posts,
  blogs,
  publications,
} = storeToRefs(store);

// Stats failed to load and there is no previously fetched data to show.
// The block stays visible with "n/a" per value instead of fake zeros;
// stale numbers from an earlier successful poll keep showing as is.
const statsUnavailable = computed(() => stats.value === null);

function formatNumber(value: number): string {
  return value.toLocaleString("ru-RU");
}

function statValue(value: number): string {
  return statsUnavailable.value ? "n/a" : formatNumber(value);
}

function deltaValue(value: number): string {
  return statsUnavailable.value ? "n/a" : `+${value}`;
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
@import "src/assets/styles/Skeleton"

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
  +skeleton-shimmer

  &.skeleton-short
    width: 140px

  &.skeleton-medium
    width: 180px
</style>

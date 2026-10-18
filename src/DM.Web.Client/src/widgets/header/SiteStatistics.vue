<template>
  <div
    v-if="loaded"
    class="site-stats"
    role="region"
    aria-label="Статистика сайта"
  >
    <!-- A line breaks after its comma and nowhere else: in a column narrower
         than the line (a larger text of the browser, numbers of thousands) its
         second count goes under the first whole, not a word of it. The space
         ends the first count, and <wbr> is where the line may break: a space
         between the two would be dropped as markup, and one inside an
         unbreakable count breaks nothing. -->
    <div class="stat-row">
      <span class="stat-part"
        >Пользователей: {{ statValue(users.value) }}
        <span class="bracket" aria-hidden="true">[</span
        ><span class="delta" :class="{ rising: grew(users.todayDelta) }">{{
          deltaValue(users.todayDelta)
        }}</span
        ><span class="bracket" aria-hidden="true">]</span>, </span
      ><wbr /><span class="stat-part"
        >online:
        <span class="online" :class="{ rising: grew(online) }">{{
          statsUnavailable ? VALUE_UNAVAILABLE : online
        }}</span></span
      >
    </div>
    <div class="stat-row">
      <span class="stat-part"
        >Персонажей: {{ statValue(characters.value) }}
        <span class="bracket" aria-hidden="true">[</span
        ><span class="delta" :class="{ rising: grew(characters.todayDelta) }">{{
          deltaValue(characters.todayDelta)
        }}</span
        ><span class="bracket" aria-hidden="true">]</span></span
      >
    </div>
    <div class="stat-row">
      <span class="stat-part"
        >Игр: {{ statValue(games.value) }}
        <span class="bracket" aria-hidden="true">[</span
        ><span class="delta" :class="{ rising: grew(games.todayDelta) }">{{
          deltaValue(games.todayDelta)
        }}</span
        ><span class="bracket" aria-hidden="true">]</span>, </span
      ><wbr /><span class="stat-part"
        >постов: {{ statValue(posts.value) }}
        <span class="bracket" aria-hidden="true">[</span
        ><span class="delta" :class="{ rising: grew(posts.todayDelta) }">{{
          deltaValue(posts.todayDelta)
        }}</span
        ><span class="bracket" aria-hidden="true">]</span></span
      >
    </div>
    <div class="stat-row">
      <span class="stat-part"
        >Блогов: {{ statValue(blogs.value) }}
        <span class="bracket" aria-hidden="true">[</span
        ><span class="delta" :class="{ rising: grew(blogs.todayDelta) }">{{
          deltaValue(blogs.todayDelta)
        }}</span
        ><span class="bracket" aria-hidden="true">]</span>, </span
      ><wbr /><span class="stat-part"
        >публикаций: {{ statValue(publications.value) }}
        <span class="bracket" aria-hidden="true">[</span
        ><span
          class="delta"
          :class="{ rising: grew(publications.todayDelta) }"
          >{{ deltaValue(publications.todayDelta) }}</span
        ><span class="bracket" aria-hidden="true">]</span></span
      >
    </div>
  </div>
  <div v-else class="site-stats-skeleton" aria-hidden="true">
    <!-- Skeleton placeholder while loading. Matches .site-stats
         dimensions exactly: 4 rows at $secondary-font-size × line-height
         1.4, with the text line occupying ~0.75em of each row's height
         so nothing shifts vertically when the numbers arrive. Inside the
         placeholder and not before it: a comment between the two branches
         is kept in a development build and makes the second one a fragment,
         which the class a parent gives (.drawer-lead) cannot land on. -->
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
import { useStatisticsStore } from "@/entities/statistics";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";

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

// Stats failed to load and there is no previously fetched data to show. The
// block stays visible with the missing-value token per value instead of fake
// zeros; stale numbers from an earlier successful poll keep showing as is.
const statsUnavailable = computed(() => stats.value === null);

function formatNumber(value: number): string {
  return value.toLocaleString("ru-RU");
}

function statValue(value: number): string {
  return statsUnavailable.value ? VALUE_UNAVAILABLE : formatNumber(value);
}

function deltaValue(value: number): string {
  return statsUnavailable.value ? VALUE_UNAVAILABLE : `+${value}`;
}

// Green is for what is good: a count the day added to, readers online. A
// nothing is neither good nor bad and stays grey, as a count that did not
// come does (the owner's rule of colour).
function grew(value: number): boolean {
  return !statsUnavailable.value && value > 0;
}

// This block's own claim on the polling: the header and the sliding column
// may both draw the statistics for a moment, as the window crosses the shell
// breakpoint, and the store polls while either of them is on screen.
const poller = {};

// Handle tab visibility changes - stop polling when tab is not visible
function handleVisibilityChange() {
  if (document.hidden) {
    store.stopPolling(poller);
  } else {
    store.startPolling(poller);
  }
}

onMounted(() => {
  store.startPolling(poller);
  document.addEventListener("visibilitychange", handleVisibilityChange);
});

onUnmounted(() => {
  store.stopPolling(poller);
  document.removeEventListener("visibilitychange", handleVisibilityChange);
});
</script>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

.site-stats
  font-size: $secondary-font-size
  line-height: 1.4
  text-align: left
  color: $text

.stat-part
  white-space: nowrap

.bracket
  color: $text-muted

.delta,
.online
  color: $text-muted

  &.rising
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

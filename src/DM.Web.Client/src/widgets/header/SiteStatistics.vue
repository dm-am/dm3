<template>
  <div
    v-if="loaded"
    class="site-stats"
    role="region"
    aria-label="Статистика сайта"
  >
    <!-- A line breaks after its comma and nowhere else: <wbr> is the one place.
         A label and its count are one text node: split in two, a phone draws
         the first digit of the count a fraction apart. -->
    <div v-for="(row, line) in rows" :key="line" class="stat-row">
      <template v-for="(part, at) in row" :key="part.label"
        ><wbr v-if="at > 0" /><span class="stat-part"
          ><template v-if="part.count"
            >{{ part.label }}: {{ statValue(part.count.value) }}
            <span class="bracket" aria-hidden="true">[</span
            ><span
              class="delta"
              :class="{ rising: grew(part.count.todayDelta) }"
              >{{ deltaValue(part.count.todayDelta) }}</span
            ><span class="bracket" aria-hidden="true">]</span></template
          ><template v-else
            >{{ part.label }}:
            <span class="online" :class="{ rising: grew(online) }">{{
              statsUnavailable ? VALUE_UNAVAILABLE : online
            }}</span></template
          ><template v-if="at < row.length - 1">, </template></span
        ></template
      >
    </div>
  </div>
  <div v-else class="site-stats-skeleton" aria-hidden="true">
    <!-- In here: between the branches, a comment makes this one a fragment in development -->
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

// Nothing ever loaded: the missing-value token per value, not fake zeros.
const statsUnavailable = computed(() => stats.value === null);

function statValue(value: number): string {
  return statsUnavailable.value
    ? VALUE_UNAVAILABLE
    : value.toLocaleString("ru-RU");
}

/** The lines of the block; a part without a count is the readers online. */
const rows = computed<{ label: string; count?: typeof users.value }[][]>(() => [
  [{ label: "Пользователей", count: users.value }, { label: "online" }],
  [{ label: "Персонажей", count: characters.value }],
  [
    { label: "Игр", count: games.value },
    { label: "постов", count: posts.value },
  ],
  [
    { label: "Блогов", count: blogs.value },
    { label: "публикаций", count: publications.value },
  ],
]);

function deltaValue(value: number): string {
  return statsUnavailable.value ? VALUE_UNAVAILABLE : `+${value}`;
}

// Green for what is good; nothing added stays grey.
function grew(value: number): boolean {
  return !statsUnavailable.value && value > 0;
}

// This block's own claim: two blocks may stand for a moment as the window crosses the breakpoint.
const poller = {};

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

$stats-line-height: 1.4

.site-stats
  font-size: $secondary-font-size
  line-height: $stats-line-height
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

// The box of .site-stats, so nothing shifts when the numbers arrive.
.site-stats-skeleton
  font-size: $secondary-font-size
  line-height: $stats-line-height
  text-align: left

.skeleton-row
  display: flex
  align-items: center
  height: $stats-line-height * 1em

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

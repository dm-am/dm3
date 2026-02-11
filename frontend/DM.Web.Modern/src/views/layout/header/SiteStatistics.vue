<template>
  <div
    v-if="loaded"
    class="site-stats"
    role="region"
    aria-label="Статистика сайта"
  >
    <div class="stat-row">
      Пользователей: {{ formatNumber(users.value) }} <span class="bracket">[</span><span class="delta">+{{ users.todayDelta }}</span><span class="bracket">]</span>, онлайн: <span class="online">{{ online }}</span>
    </div>
    <div class="stat-row">
      Персонажей: {{ formatNumber(characters.value) }} <span class="bracket">[</span><span class="delta">+{{ characters.todayDelta }}</span><span class="bracket">]</span>
    </div>
    <div class="stat-row">
      Игр: {{ formatNumber(games.value) }} <span class="bracket">[</span><span class="delta">+{{ games.todayDelta }}</span><span class="bracket">]</span>
    </div>
    <div class="stat-row">
      Игровых постов: {{ formatNumber(posts.value) }} <span class="bracket">[</span><span class="delta">+{{ posts.todayDelta }}</span><span class="bracket">]</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted } from "vue";
import { storeToRefs } from "pinia";
import { useStatisticsStore } from "@/stores";

const store = useStatisticsStore();
const { loaded, online, users, characters, games, posts } = storeToRefs(store);

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
</style>

<script setup lang="ts">
import type { Game } from "@/api/models/gaming";
import { computed, ref } from "vue";

const props = defineProps<{
  game: Game;
  counters: boolean;
  alwaysShowCounters?: boolean;
}>();
const params = computed(() => ({ id: props.game.id }));
const hovered = ref(false);
const showCounters = computed(
  () => props.counters && (props.alwaysShowCounters || hovered.value),
);

// Counter values with null safety
const postsCount = computed(() => props.game.unreadPostsCount ?? 0);
const commentsCount = computed(() => props.game.unreadCommentsCount ?? 0);

// ARIA labels for accessibility
const postsAriaLabel = computed(
  () => `${postsCount.value} непрочитанных постов`,
);
const commentsAriaLabel = computed(
  () => `${commentsCount.value} непрочитанных комментариев`,
);

// Tooltip: [Master] | [System] | Players: X
const playersCount = computed(() => {
  const uniqueIds = new Set(props.game.activeCharacterUserIds ?? []);
  return uniqueIds.size;
});
const gameTooltip = computed(() => {
  const parts: string[] = [];
  if (props.game.master?.login) {
    parts.push(props.game.master.login);
  }
  if (props.game.system) {
    parts.push(props.game.system);
  }
  parts.push(`Игроков: ${playersCount.value}`);
  return parts.join(" | ");
});
</script>

<template>
  <div class="link" @mouseenter="hovered = true" @mouseleave="hovered = false">
    <span class="muted" aria-hidden="true">- </span>
    <router-link :to="{ name: 'game', params }" :title="gameTooltip">{{
      game.title
    }}</router-link>{{ " "
    }}<span
      v-if="showCounters"
      class="counters"
      role="status"
      :aria-label="`Счётчики: ${postsCount} постов, ${commentsCount} комментариев`"
    ><span class="muted" aria-hidden="true">(</span>
      <router-link
        :to="{ name: 'game-first-unread-post', params }"
        :aria-label="postsAriaLabel"
        :title="`Непрочитанные посты: ${postsCount}`"
        >{{ postsCount }}</router-link
      >
      <span class="muted" aria-hidden="true">/</span>
      <router-link
        :to="{ name: 'game-comments', params }"
        :aria-label="commentsAriaLabel"
        :title="`Непрочитанные комментарии: ${commentsCount}`"
        >{{ commentsCount }}</router-link
      >
      <span class="muted" aria-hidden="true">)</span>
    </span>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.muted
  color: $text-muted

.counters
  transition: opacity 0.15s ease
</style>

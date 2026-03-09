<script setup lang="ts">
import type { Game } from "@/entities/game";
import { computed, ref } from "vue";

const props = withDefaults(
  defineProps<{
    game: Game;
    counters: boolean;
    alwaysShowCounters?: boolean;
    prefix?: string;
  }>(),
  {
    prefix: "- ",
  },
);
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

// Tooltip: Master | System | Игроков: X/Y | Читателей: Z
const playersInfo = computed(() => {
  const current = props.game.recruitment?.playerCount ?? 0;
  const limit = props.game.recruitment?.playerLimit;
  return limit ? `${current}/${limit}` : `${current}`;
});
const readersCount = computed(() => props.game.readerUserIds?.length ?? 0);
const gameTooltip = computed(() => {
  const parts: string[] = [];
  if (props.game.master?.username) {
    parts.push(props.game.master.username);
  }
  if (props.game.system) {
    parts.push(props.game.system);
  }
  parts.push(`Игроков: ${playersInfo.value}`);
  parts.push(`Читателей: ${readersCount.value}`);
  return parts.join(" | ");
});
</script>

<template>
  <div class="link" @mouseenter="hovered = true" @mouseleave="hovered = false">
    <span class="muted" aria-hidden="true">{{ prefix }}</span>
    <router-link :to="{ name: 'game', params }" :title="gameTooltip">{{
      game.title
    }}</router-link>{{ " "
    }}<span
      v-if="showCounters"
      class="counters"
      role="status"
      :aria-label="`Счетчики: ${postsCount} постов, ${commentsCount} комментариев`"
    ><span class="muted" aria-hidden="true">(</span>
      <router-link
        :to="{ name: 'game-first-unread-post', params }"
        :aria-label="postsAriaLabel"
        :title="`Непрочитанные посты: ${postsCount}`"
        >{{ postsCount }}</router-link
      >
      <span class="muted" aria-hidden="true">/</span>
      <router-link
        :to="{ name: 'game-first-unread-comment', params }"
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
  .muted
    user-select: text
</style>

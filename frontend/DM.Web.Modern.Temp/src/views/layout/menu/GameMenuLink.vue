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
const showCounters = computed(() => props.counters && (props.alwaysShowCounters || hovered.value));
</script>

<template>
  <div class="link" @mouseenter="hovered = true" @mouseleave="hovered = false">
    <span class="muted">-</span> <router-link :to="{ name: 'game', params }">{{ game.title }}</router-link><span v-if="showCounters" class="counters">&nbsp;<span class="muted">(</span><router-link :to="{ name: 'game-first-unread-post', params }">{{ game.unreadPostsCount || 0 }}</router-link><span class="muted">/</span><router-link :to="{ name: 'game-comments', params }">{{ game.unreadCommentsCount || 0 }}</router-link><span class="muted">)</span></span>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.muted
  +theme(color, $secondary-text)

.counters
  transition: opacity 0.15s ease
</style>

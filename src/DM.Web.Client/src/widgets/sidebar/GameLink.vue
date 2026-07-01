<script setup lang="ts">
// Sidebar game row: wraps the shared GameLink primitive from
// @/entities/game (which owns the title + tooltip pair) and adds the
// sidebar-specific affordances around it:
//   - "- " prefix
//   - hover-activated unread counters "(posts/comments)"
//
// The green "new game" highlight is delegated to the primitive via the
// highlight-new prop.
import {
  useGameDisplay,
  GameLink,
  type Game,
  type GameRef,
} from "@/entities/game";
import { computed, ref } from "vue";

const props = withDefaults(
  defineProps<{
    game: Game | GameRef;
    counters: boolean;
    alwaysShowCounters?: boolean;
    prefix?: string;
  }>(),
  {
    prefix: "- ",
  },
);

const {
  getUnreadPosts,
  getUnreadComments,
  formatUnreadPostsTooltip,
  formatUnreadCommentsTooltip,
} = useGameDisplay();

const params = computed(() => ({ id: props.game.id }));
const hovered = ref(false);
const showCounters = computed(
  () => props.counters && (props.alwaysShowCounters || hovered.value),
);

const postsCount = computed(() => getUnreadPosts(props.game));
const commentsCount = computed(() => getUnreadComments(props.game));
const postsTooltip = computed(() => formatUnreadPostsTooltip(postsCount.value));
const commentsTooltip = computed(() =>
  formatUnreadCommentsTooltip(commentsCount.value),
);
</script>

<template>
  <div class="link" @mouseenter="hovered = true" @mouseleave="hovered = false">
    <span class="muted" aria-hidden="true">{{ prefix }}</span>
    <GameLink :game="game" highlight-new muted-closed />{{ " "
    }}<span v-if="showCounters" class="counters"
      ><span class="bracket">(</span
      ><router-link
        :to="{ name: 'game-first-unread-post', params }"
        :aria-label="postsTooltip"
        >{{ postsCount }}</router-link
      ><span class="counter-sep">/</span
      ><router-link
        :to="{ name: 'game-first-unread-comment', params }"
        :aria-label="commentsTooltip"
        >{{ commentsCount }}</router-link
      ><span class="bracket">)</span></span
    >
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.muted
  color: $text-muted
  user-select: none

.counters
  transition: opacity 0.15s ease

.bracket,
.counter-sep
  color: $text-muted
</style>

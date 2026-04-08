<script setup lang="ts">
import { useGameDisplay, type Game, type GameRef } from "@/entities/game";
import { Tooltip } from "@/shared/ui/Tooltip";
import { computed, ref } from "vue";

const props = withDefaults(
  defineProps<{
    game: Game | GameRef;
    counters: boolean;
    alwaysShowCounters?: boolean;
    prefix?: string;
    /** Show master and assistants */
    showOwners?: boolean;
  }>(),
  {
    prefix: "- ",
    showOwners: false,
  },
);

const {
  buildTooltip,
  getUnreadPosts,
  getUnreadComments,
  formatUnreadPostsTooltip,
  formatUnreadCommentsTooltip,
  isNew,
} = useGameDisplay();

// Game is "new" if recruitment started < 7 days ago
const isNewGame = computed(() => isNew(props.game));

const params = computed(() => ({ id: props.game.id }));
const hovered = ref(false);
const showCounters = computed(
  () => props.counters && (props.alwaysShowCounters || hovered.value),
);

// Counter values using shared composable
const postsCount = computed(() => getUnreadPosts(props.game));
const commentsCount = computed(() => getUnreadComments(props.game));

// Tooltips using shared composable
const postsTooltip = computed(() => formatUnreadPostsTooltip(postsCount.value));
const commentsTooltip = computed(() => formatUnreadCommentsTooltip(commentsCount.value));
const gameTooltip = computed(() => buildTooltip(props.game));
</script>

<template>
  <div class="link" @mouseenter="hovered = true" @mouseleave="hovered = false">
    <span class="muted" aria-hidden="true">{{ prefix }}</span>
    <Tooltip :text="gameTooltip">
      <router-link :to="{ name: 'game', params }" :class="{ 'new-item': isNewGame }">{{
        game.title
      }}</router-link>
    </Tooltip>{{ " "
    }}<span v-if="showCounters" class="counters"
      ><span class="bracket">(</span
      ><Tooltip :text="postsTooltip"
        ><router-link
          :to="{ name: 'game-first-unread-post', params }"
          :aria-label="postsTooltip"
          >{{ postsCount }}</router-link
        ></Tooltip
      ><span class="separator">/</span
      ><Tooltip :text="commentsTooltip"
        ><router-link
          :to="{ name: 'game-first-unread-comment', params }"
          :aria-label="commentsTooltip"
          >{{ commentsCount }}</router-link
        ></Tooltip
      ><span class="bracket">)</span
    ></span>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.muted
  color: $text-muted
  user-select: none

.new-item
  color: $accent-green
  &:hover
    color: $accent-green-hover

.counters
  transition: opacity 0.15s ease

.bracket,
.separator
  color: $text-muted
</style>

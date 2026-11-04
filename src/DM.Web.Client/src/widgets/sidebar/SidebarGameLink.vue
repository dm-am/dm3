<script setup lang="ts">
// A sidebar game row: GameLink with the prefix, the unread counters and the wait star.
import {
  useGameDisplay,
  GameLink,
  type Game,
  type GameRef,
} from "@/entities/game";
import { CounterPair } from "@/shared/ui/CounterPair";
import { Tooltip } from "@/shared/ui/Tooltip";
import { computed, ref } from "vue";

const props = withDefaults(
  defineProps<{
    game: Game | GameRef;
    alwaysShowCounters?: boolean;
    prefix?: string;
    /** The star when the game waits for the viewer; the public lists are not his obligations. */
    waitMarker?: boolean;
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
const showCounters = computed(() => props.alwaysShowCounters || hovered.value);

const postsCount = computed(() => getUnreadPosts(props.game));
const commentsCount = computed(() => getUnreadComments(props.game));
const postsTooltip = computed(() => formatUnreadPostsTooltip(postsCount.value));
const commentsTooltip = computed(() =>
  formatUnreadCommentsTooltip(commentsCount.value),
);

const STAR = "*";

// The server's answer, from the selection the room list draws its star from.
const awaitsMe = computed(
  () => props.waitMarker && props.game.awaitsViewerTurn === true,
);
const awaitedNames = computed(() => props.game.awaitedCharacterNames ?? []);
const starTooltip = computed(() =>
  awaitedNames.value.length
    ? `Вашего хода ждут: ${awaitedNames.value.join(", ")}`
    : "Ожидается ваш ход",
);
</script>

<template>
  <li class="link" @mouseenter="hovered = true" @mouseleave="hovered = false">
    <span class="muted" aria-hidden="true">{{ prefix }}</span>
    <GameLink :game="game" highlight-new muted-closed />{{ " "
    }}<CounterPair
      v-if="showCounters"
      class="counters"
      :first-value="postsCount"
      :first-to="{ name: 'game-first-unread-post', params }"
      :first-label="postsTooltip"
      :second-value="commentsCount"
      :second-to="{ name: 'game-first-unread-comment', params }"
      :second-label="commentsTooltip"
    /><Tooltip v-if="awaitsMe" :text="starTooltip">
      <span class="star" aria-label="Ожидается ваш ход">{{ STAR }}</span>
    </Tooltip>
  </li>
</template>

<style scoped lang="sass">
.link
  display: block

.star
  color: $accent-red
  margin-left: $minor
  cursor: default
</style>

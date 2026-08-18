<script setup lang="ts">
// Sidebar game row: wraps the shared GameLink primitive from
// @/entities/game (which owns the title + tooltip pair) and adds the
// sidebar-specific affordances around it:
//   - "- " prefix
//   - hover-activated unread counters "(posts/comments)"
//   - red "★" wait marker with the "Вашего хода ждут: ..." tooltip, for the
//     lists that show the viewer his own games (wait-marker prop)
//
// The green "new game" highlight is delegated to the primitive via the
// highlight-new prop.
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
    counters: boolean;
    alwaysShowCounters?: boolean;
    prefix?: string;
    /**
     * Draw the star when the game waits for this viewer's post. Off by default:
     * the flag rides on every game the server sends, and the public lists
     * (active, recruiting, popular, finished) are lists of games, not of the
     * reader's obligations.
     */
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
const showCounters = computed(
  () => props.counters && (props.alwaysShowCounters || hovered.value),
);

const postsCount = computed(() => getUnreadPosts(props.game));
const commentsCount = computed(() => getUnreadComments(props.game));
const postsTooltip = computed(() => formatUnreadPostsTooltip(postsCount.value));
const commentsTooltip = computed(() =>
  formatUnreadCommentsTooltip(commentsCount.value),
);

// Wait marker glyph, mirrored from GameRoomLink and the mentor panel.
const STAR = "★";

// Whether a turn is awaited is the server's answer, given by the same selection
// the room list draws its star from: the row cannot promise a star the room
// behind it would not show.
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

.muted
  color: $text-muted

.counters
  transition: opacity 0.15s ease

// Same star the room rows and the mentor panel draw.
.star
  color: $accent-red
  margin-left: 4px
  cursor: default
</style>

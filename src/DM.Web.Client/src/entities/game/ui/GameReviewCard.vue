<template>
  <SpeechBubble :content-key="review.id">
    <!-- Server-rendered BBCode, unlike the recommendation's plain text: the
         bubble does not care what is inside it. Highlighting is ContentText's
         own, done over the DOM rather than by a regex over the HTML. -->
    <ContentText :html="review.text" :search-query="searchQuery" />

    <!-- "<автор> о <игра>", the recommendation's footer shape. The author
         leads as the person speaking; the second party recedes to a muted
         link. Spaces around "о" are real text nodes so the line copies as
         plain text. The game is omitted where the surface IS the game — its
         own reviews tab — the way the site testimonials gallery omits a
         recipient. -->
    <template #left>
      <user-link
        :user="review.author"
        :search-query="searchQuery"
        :hide-badge="true"
      /><template v-if="showGame && review.gameId"
        >{{ " " }}<span class="about-connector">о</span>{{ " "
        }}<router-link
          class="about-link"
          :to="{ name: 'game', params: { id: review.gameId } }"
          >{{ gameTitle }}</router-link
        ></template
      >
    </template>

    <template #right>
      <Tooltip :text="formatDateFull(review.createdUtc)" focusable
        ><secondary-text class="review-date">
          {{ formatDate(review.createdUtc) }}
        </secondary-text></Tooltip
      >
    </template>
  </SpeechBubble>
</template>

<script setup lang="ts">
/**
 * A game review drawn the way a recommendation is: the same green bubble with
 * the text visible at once, and "<автор> о <игра>" with the date under it.
 *
 * It used to be a row of a collapsed accordion, where the text was hidden
 * behind a click and the authorship was a comma-joined string with no links
 * in it. The bubble comes from shared/ui/SpeechBubble, so this card and
 * TestimonialCard cannot drift apart.
 */
import { computed } from "vue";
import type { GameReview } from "@/shared/api/models/game/reviews";
import { UserLink } from "@/entities/user/@x/game";
import { SpeechBubble } from "@/shared/ui/SpeechBubble";
import { ContentText } from "@/shared/ui/Content";
import { Tooltip } from "@/shared/ui/Tooltip";
import { formatDate, formatDateFull } from "@/shared/lib/utils/datetime";

const props = withDefaults(
  defineProps<{
    review: GameReview;
    /**
     * Name the reviewed game in the footer. False on the game's own reviews
     * tab, where every row is about the page the reader is already on.
     */
    showGame?: boolean;
    /** Search query for highlighting */
    searchQuery?: string;
  }>(),
  { showGame: true, searchQuery: undefined },
);

// A game whose title never arrived still gets a readable line rather than an
// empty link — the same fallback the accordion rows used.
const gameTitle = computed(() => props.review.gameTitle ?? "Игра без названия");
</script>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

// The bubble sets pre-wrap for the recommendation's plain text; server-
// rendered BBCode brings its own markup, and literal whitespace runs would
// render here differently from every other ContentText surface.
:deep(.bbcode-content)
  white-space: normal

// The second party of the pair recedes so the author stays the primary link,
// exactly as the recipient does in a recommendation.
.about-link
  +muted-link

.about-connector
  color: $text-muted

// secondary-text renders a block <div>; inside the float's inline copy flow
// it must be inline, or Chrome emits a newline and the footer copies as
// "author\ndate".
.review-date
  display: inline
  font-size: $font-size
  cursor: help
</style>

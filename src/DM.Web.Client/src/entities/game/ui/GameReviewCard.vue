<template>
  <SpeechBubble :content-key="review.id">
    <ContentText :html="review.text" :search-query="searchQuery" />

    <!-- The spaces around "о" are text nodes, so the line copies as plain text. -->
    <template #left>
      <user-link
        v-if="review.author"
        :user="review.author"
        :search-query="searchQuery"
        :hide-badge="true"
      /><removed-account-link v-else /><template
        v-if="showGame && review.gameId"
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

    <template v-if="$slots.actions" #actions>
      <slot name="actions" />
    </template>
  </SpeechBubble>
</template>

<script setup lang="ts">
import { computed } from "vue";
import type { GameReview } from "@/shared/api/models/game/reviews";
import { RemovedAccountLink, UserLink } from "@/entities/user/@x/game";
import { SpeechBubble } from "@/shared/ui/SpeechBubble";
import { ContentText } from "@/shared/ui/Content";
import { Tooltip } from "@/shared/ui/Tooltip";
import { formatDate, formatDateFull } from "@/shared/lib/utils/datetime";

const props = withDefaults(
  defineProps<{
    review: GameReview;
    /** False on the game's own reviews tab. */
    showGame?: boolean;
    searchQuery?: string;
  }>(),
  { showGame: true },
);

const gameTitle = computed(() => props.review.gameTitle ?? "Игра без названия");
</script>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

// The bubble's pre-wrap is for plain text; rendered BBCode brings its own markup.
:deep(.bbcode-content)
  white-space: normal

.about-link
  +muted-link

.about-connector
  color: $text-muted

// Inline, or the footer copies as "author", a newline and the date.
.review-date
  display: inline
  font-size: $font-size
  cursor: help
</style>

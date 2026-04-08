<script setup lang="ts">
import { ref, computed } from "vue";
import type { Post, PostReview } from "@/entities/game";
import { gameApi, PostReviewItem } from "@/entities/game";
import { ContentText, Tooltip, SvgIcon } from "@/shared/ui";
import { UserLink } from "@/entities/user";
import { useContentTruncation } from "@/shared/lib/composables";
import dayjs from "dayjs";
import defaultPicture from "@/assets/images/userpic.png";

const props = withDefaults(
  defineProps<{
    post: Post;
    number?: number;
    /** Show navigation breadcrumb (Game > Room) */
    showNavigation?: boolean;
    /** Enable content truncation */
    truncatable?: boolean;
    /** Max height before truncation (px) */
    maxHeight?: number;
  }>(),
  {
    showNavigation: false,
    truncatable: false,
    maxHeight: 150,
  },
);

// Reviews state
const showReviews = ref(false);
const reviews = ref<PostReview[]>([]);
const reviewsLoaded = ref(false);

// Data access
const character = computed(() => props.post?.character);
const author = computed(() => props.post?.author);
const hasCharacter = computed(() => !!character.value);
const hasAuthor = computed(() => !!author.value);
const authorGameRole = computed(() => props.post?.authorGameRole);
const characterName = computed(() => character.value?.name);
const canLinkCharacter = computed(() => !!character.value?.id && !!props.post?.room?.game?.id);
const hasDiceRolls = computed(() => props.post?.diceRolls && props.post.diceRolls.length > 0);
const hasMetagameText = computed(() => !!props.post?.metagameText);
const postTextHtml = computed(() => props.post?.gameText ?? "");

const avatarPicture = computed(() => {
  if (character.value?.pictureUrl) {
    return character.value.pictureUrl;
  }
  return defaultPicture;
});

const formattedDate = computed(() => {
  if (!props.post?.createdUtc) return "";
  return dayjs(props.post.createdUtc).format("DD.MM.YYYY HH:mm");
});

const reviewCount = computed(() => props.post?.reviewCount ?? 0);

const postId = computed(() => props.post?.id);
const postAnchor = computed(() => `#post-${postId.value}`);

// Rating text parts
const ratingPrefix = "Рейтинг (";
const ratingSuffix = ")";
const ratingValue = computed(() => {
  const r = postRating.value ?? 0;
  const sign = r > 0 ? "+" : "";
  return `${sign}${r}`;
});
const ratingColorClass = computed(() => {
  const r = postRating.value ?? 0;
  if (r > 0) return "positive";
  if (r < 0) return "negative";
  return "neutral";
});

// Has any reviews to show toggle
const hasReviews = computed(() => reviewCount.value > 0);

// Navigation
const hasNavigation = computed(() => props.showNavigation && props.post?.room?.game);
const gameId = computed(() => props.post?.room?.game?.publicId || props.post?.room?.game?.id);
const gameTitle = computed(() => props.post?.room?.game?.title);
const roomNumber = computed(() => props.post?.room?.roomNumber);
const roomTitle = computed(() => props.post?.room?.title);

// Rating
const postRating = computed(() => props.post?.rating ?? null);

// Truncation (unified composable)
const shouldTruncate = computed(() => props.truncatable || props.showNavigation);
const {
  setContentRef,
  contentStyle,
  needsTruncation,
  isExpanded,
  toggleExpand,
} = useContentTruncation({
  maxHeight: props.maxHeight,
  enabled: shouldTruncate,
  watchContent: postTextHtml,
});

function copyAnchorLink() {
  navigator.clipboard.writeText(
    window.location.origin + window.location.pathname + postAnchor.value,
  );
}

// Review functions
async function toggleReviews() {
  showReviews.value = !showReviews.value;

  if (showReviews.value && !reviewsLoaded.value && postId.value) {
    try {
      const { data } = await gameApi.getPostReviews(postId.value);
      reviews.value = data?.resources ?? [];
      reviewsLoaded.value = true;
    } catch {
      // Ignore errors
    }
  }
}
</script>

<template>
  <article :id="`post-${postId}`" class="game-post" :class="{ 'with-nav': hasNavigation }">
    <!-- Navigation breadcrumb -->
    <div v-if="hasNavigation" class="post-nav">
      <router-link :to="{ name: 'game', params: { id: gameId } }">
        {{ gameTitle }}
      </router-link>
      <span class="nav-separator"> > </span>
      <router-link :to="{ name: 'game-room', params: { id: gameId, num: roomNumber } }">
        {{ roomTitle }}
      </router-link>
    </div>

    <div class="post-layout">
      <!-- Left column: metadata -->
      <div class="post-meta">
        <!-- Character/Author info -->
        <template v-if="hasCharacter">
          <router-link
            v-if="canLinkCharacter"
            class="character-name"
            :to="{ name: 'game-characters', params: { id: post.room?.game?.publicId || post.room?.game?.id } }"
          >{{ characterName }}</router-link>
          <span v-else class="character-name">{{ characterName }}</span>
          <UserLink v-if="hasAuthor" :user="author!" hide-badge />
        </template>
        <template v-else-if="authorGameRole">
          <span class="game-role">{{ authorGameRole }}</span>
          <UserLink v-if="hasAuthor" :user="author!" hide-badge />
        </template>
        <template v-else-if="hasAuthor">
          <UserLink :user="author!" />
        </template>
        <template v-else>
          <span class="system-text">Системное</span>
        </template>
        <!-- Avatar (character picture) -->
        <img v-if="hasCharacter" :src="avatarPicture" class="avatar" />
        <!-- Date -->
        <span class="post-date">{{ formattedDate }}</span>

        <!-- Rating display -->
        <span class="rating-display">
          {{ ratingPrefix }}<a
            v-if="hasReviews"
            class="rating-value"
            :class="ratingColorClass"
            @click="toggleReviews"
          >{{ ratingValue }}</a
          ><span v-else class="rating-value" :class="ratingColorClass">{{ ratingValue }}</span
          >{{ ratingSuffix }}
        </span>

        <!-- Rating buttons -->
        <div class="rating-buttons">
          <a class="rating-btn">+</a>
          <a class="rating-btn">=</a>
          <a class="rating-btn">-</a>
        </div>
      </div>

      <!-- Right column: content -->
      <div class="post-body">
        <!-- Post content (truncatable) -->
        <div
          :ref="setContentRef"
          class="post-content"
          :class="{ truncatable: needsTruncation }"
          :style="contentStyle"
        >
          <!-- Game text -->
          <div class="game-text">
            <content-text :html="postTextHtml" />
          </div>

          <!-- Dice rolls (under game text) -->
          <div v-if="hasDiceRolls" class="dice-rolls">
            <div v-for="roll in post.diceRolls" :key="roll.id" class="dice-roll">
              <span class="dice-result">
                d{{ roll.dice }}: {{ roll.result }}
                <span v-if="roll.bonus">{{ roll.bonus > 0 ? "+" : "" }}{{ roll.bonus }}</span>
                <span class="dice-total">= {{ roll.result + (roll.bonus || 0) }}</span>
              </span>
              <span v-if="roll.comment" class="dice-comment">{{ roll.comment }}</span>
            </div>
          </div>

          <!-- Metagame text (OOC) -->
          <div v-if="hasMetagameText" class="metagame-text">
            <content-text :html="post.metagameText!" />
          </div>
        </div>
        <a v-if="needsTruncation && !isExpanded" class="expand-link" @click="toggleExpand"
          >... <strong>показать полностью</strong></a
        >
      </div>

      <!-- Post footer: post link -->
      <div class="post-footer">
        <Tooltip v-if="hasNavigation" text="Перейти к посту">
          <router-link
            class="post-link"
            :to="{ name: 'game-room', params: { id: gameId, num: roomNumber }, hash: postAnchor }"
          ><SvgIcon name="back" /></router-link>
        </Tooltip>
        <a v-else-if="number" class="post-number" :href="postAnchor" @click.prevent="copyAnchorLink">{{ number }}</a>
      </div>
    </div>

    <!-- Reviews section (below post, expandable) -->
    <ul v-if="hasReviews" class="reviews-section" :class="{ collapsed: !showReviews }">
      <PostReviewItem v-for="review in reviews" :key="review.id" :review="review" />
    </ul>
  </article>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

// Posts with navigation: nav is outside card, card is .post-layout
.game-post.with-nav
  .post-layout
    padding: 5px
    background-color: $bg-element
    border: 1px dashed $border

// Regular posts: card styling on container
.game-post:not(.with-nav)
  padding: 5px
  background-color: $bg-element
  border: 1px dashed $border

// Two-column layout
.post-layout
  display: flex
  flex-wrap: wrap
  gap: 10px
  align-items: flex-start

// Left column: metadata
.post-meta
  flex-shrink: 0
  box-sizing: content-box
  width: 140px
  min-height: 290px
  padding-top: $small
  text-align: left
  font-size: $font-size
  line-height: 1.4

  > *
    display: block

// Character name: gray when not linkable, link color when linkable
.character-name
  padding-bottom: 4px

span.character-name
  color: $text-muted

a.character-name
  color: $link
  &:hover
    color: $link-hover

.avatar
  width: 140px
  height: auto
  margin: 2px 0
  border: 1px solid $border

.post-date
  color: $text

.system-text
  font-style: italic
  color: $text-muted

.game-role
  font-weight: 600
  color: $text-muted

// Right column: content
.post-body
  flex: 1
  min-width: 0
  min-height: 290px
  word-wrap: break-word
  word-break: break-word
  overflow-wrap: break-word
  color: $text
  line-height: 1.5

.post-content
  &.truncatable
    overflow: hidden
    transition: max-height 0.4s ease

.game-text
  color: $text
  margin: 11px 5px

.expand-link
  display: inline-block
  margin-left: 5px
  color: $link
  cursor: pointer
  &:hover
    color: $link-hover

.metagame-text
  color: $text-meta
  margin: 11px 5px

  b, strong, i, em, u, s, strike, del, pre, code, li, span, div
    color: inherit

.dice-rolls
  padding: 0 2px

.dice-roll
  padding: 2px 0
  margin-top: 4px

.dice-result
  font-family: monospace
  font-size: $secondary-font-size
  color: $heading
  padding: 2px
  border: 1px solid $border

.dice-total
  font-weight: bold
  color: $accent-green

.dice-comment
  color: $text-muted
  font-style: italic

.post-footer
  width: 100%
  display: flex
  justify-content: flex-end
  align-items: center
  gap: $small
  min-height: 20px
  margin-top: $tiny

.post-number
  color: $text-muted
  font-size: $tertiary-font-size
  text-decoration: none
  &:hover
    color: $link

// Navigation (for featured posts on homepage)
.post-nav
  margin-bottom: $small
  a
    color: $link
    text-decoration: none
    &:hover
      color: $link-hover

.nav-separator
  color: $text-muted

// Rating display
.rating-display
  display: block
  margin-top: $minor
  font-size: $font-size
  color: $text-muted

.rating-value
  &.positive
    color: $accent-green
  &.negative
    color: $accent-red
  &.neutral
    color: $text-muted

a.rating-value
  cursor: pointer
  &.positive:hover
    color: $accent-green-hover
  &.negative:hover
    color: $accent-red-hover
  &.neutral:hover
    color: $link-hover

.rating-buttons
  margin-top: $tiny

.rating-btn
  font-size: $font-size
  color: $link
  cursor: pointer
  margin-right: $medium
  text-decoration: none
  &:hover
    color: $link-hover

// Reviews section (below post, bullet list)
.reviews-section
  list-style: disc
  margin: 0
  margin-top: $small
  padding-left: 25px
  overflow: hidden
  transition: max-height $animation-time ease-out
  max-height: 1000px
  &.collapsed
    max-height: 0
    margin-top: 0

.post-link
  color: $link
  font-size: $secondary-font-size
  text-decoration: none
  &:hover
    color: $link-hover
</style>

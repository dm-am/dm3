<template>
  <div class="featured-post">
    <!-- Navigation: Game > Room -->
    <div class="post-nav">
      <router-link :to="{ name: 'game', params: { id: post.gameId } }">
        {{ post.gameTitle }}
      </router-link>
      <span class="nav-separator"> > </span>
      <router-link
        :to="{ name: 'game-room', params: { id: post.gameId, roomId: post.roomId } }"
      >
        {{ post.roomTitle }}
      </router-link>
    </div>

    <!-- Post text preview -->
    <div class="post-text">{{ post.textPreview }}</div>

    <!-- Author and rating -->
    <div class="post-meta">
      <span class="meta-dash">— </span>
      <user-link :user="post.author" />
      <span v-if="post.characterName" class="character-name">
        ({{ post.characterName }})
      </span>
      <span class="meta-dot"> · </span>
      <span class="rating" :class="ratingClass">{{ ratingText }}</span>
    </div>

    <!-- Reviews section (collapsible) -->
    <div v-if="post.reviewCount > 0" class="reviews-section">
      <div class="reviews-header" @click="toggleReviews">
        <span>Отзывы ({{ post.reviewCount }})</span>
        <span class="toggle-icon" :class="{ expanded: showReviews }"></span>
      </div>
      <div class="reviews-list" :class="{ collapsed: !showReviews }">
        <template v-if="reviews.length > 0">
          <div v-for="review in reviews" :key="review.id" class="review-item">
            <user-link :user="review.author!" :hide-badge="true" />
            <span class="review-sign" :class="getSignClass(review.sign)">
              {{ getSignText(review.sign) }}
            </span>
            <span v-if="review.text" class="review-text">{{ review.text }}</span>
          </div>
        </template>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from "vue";
import type { FeaturedPost, PostReview, ReviewSign } from "@/entities/game";
import { UserLink } from "@/entities/user";
import { gameApi } from "@/entities/game";

const props = defineProps<{ post: FeaturedPost }>();

const showReviews = ref(false);
const reviews = ref<PostReview[]>([]);
const reviewsLoading = ref(false);
const reviewsLoaded = ref(false);

const ratingText = computed(() => {
  const r = props.post.rating;
  if (r > 0) return `+${r}`;
  return r.toString();
});

const ratingClass = computed(() => {
  const r = props.post.rating;
  if (r > 0) return "positive";
  if (r < 0) return "negative";
  return "neutral";
});

function getSignClass(sign?: ReviewSign): string {
  switch (sign) {
    case "Positive":
      return "positive";
    case "Negative":
      return "negative";
    default:
      return "neutral";
  }
}

function getSignText(sign?: ReviewSign): string {
  switch (sign) {
    case "Positive":
      return "+1";
    case "Negative":
      return "-1";
    default:
      return "0";
  }
}

async function toggleReviews() {
  showReviews.value = !showReviews.value;

  // Lazy load reviews on first expand
  if (showReviews.value && !reviewsLoaded.value) {
    reviewsLoading.value = true;
    try {
      const { data } = await gameApi.getPostReviews(props.post.id);
      reviews.value = data?.resources ?? [];
      reviewsLoaded.value = true;
    } finally {
      reviewsLoading.value = false;
    }
  }
}
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.featured-post
  padding: 0

.post-nav
  margin-bottom: $small
  font-size: $secondary-font-size

.nav-separator
  color: $text-muted

.post-text
  margin-bottom: $small
  color: $text
  line-height: 1.4

.post-meta
  color: $text-muted
  font-size: $secondary-font-size

.meta-dash, .meta-dot
  color: $text-muted

.character-name
  color: $text-muted

.rating
  font-weight: bold
  &.positive
    color: $accent-green
  &.negative
    color: $accent-red
  &.neutral
    color: $text-muted

.reviews-section
  margin-top: $small
  padding-top: $small
  border-top: 1px dashed $border

.reviews-header
  display: flex
  justify-content: space-between
  align-items: center
  cursor: pointer
  color: $text-muted
  user-select: none
  font-size: $secondary-font-size
  &:hover
    color: $text

.toggle-icon
  position: relative
  width: 12px
  height: 12px
  &::before
    content: '+'
    font-size: 14px
    font-weight: bold
  &.expanded::before
    content: '−'

.reviews-list
  overflow: hidden
  transition: max-height $animation-time ease-out
  max-height: 500px
  &.collapsed
    max-height: 0

.review-item
  padding: $tiny 0
  font-size: $secondary-font-size

.review-sign
  margin: 0 $tiny
  font-weight: bold
  &.positive
    color: $accent-green
  &.negative
    color: $accent-red
  &.neutral
    color: $text-muted

.review-text
  color: $text-muted
  font-style: italic
</style>

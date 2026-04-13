<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick } from "vue";
import type { Post, PostReview } from "@/entities/game";
import {
  gameApi,
  GameLink,
  PostReviewItem,
  RoomLink,
} from "@/entities/game";
import { ContentText, Tooltip, TruncatedContent } from "@/shared/ui";
import { UserLink } from "@/entities/user";
import { trimHtmlWhitespace } from "@/shared/lib/utils/bbcodeInteractive";
import { useAuthStore } from "@/shared/stores/auth";
import dayjs from "dayjs";
import { defaultAvatarUrl as defaultPicture, symbols } from "@/shared/lib/utils/icons";

const props = withDefaults(
  defineProps<{
    post: Post;
    number?: number;
    /** Featured post mode — shows navigation breadcrumb, anchor icon instead of number */
    showNavigation?: boolean;
    /** Enable content truncation */
    truncatable?: boolean;
    /** Fallback max height before truncation (px) */
    maxHeight?: number;
  }>(),
  {
    showNavigation: false,
    truncatable: false,
    maxHeight: 150,
  },
);

const authStore = useAuthStore();

// Reviews state
const showReviews = ref(false);
const reviews = ref<PostReview[]>([]);
const reviewsLoaded = ref(false);

// Review form state
const newReviewSign = ref<number>(1);
const newReviewText = ref("");
const submittingReview = ref(false);

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
// Pre-trim leading/trailing empty lines so they never inflate scrollHeight
// or eat the collapsed budget. Pure transforms, no DOM mutation.
const postTextHtml = computed(() => trimHtmlWhitespace(props.post?.gameText));
const postMetagameHtml = computed(() =>
  trimHtmlWhitespace(props.post?.metagameText),
);

const avatarPicture = computed(() => {
  if (character.value?.pictureUrl) {
    return character.value.pictureUrl;
  }
  return defaultPicture;
});

const formattedDate = computed(() => {
  if (!props.post?.createdUtc) return "";
  return dayjs(props.post.createdUtc).format("DD.MM.YYYY [в] HH:mm");
});

const reviewCount = computed(() => props.post?.reviewCount ?? 0);

const postId = computed(() => props.post?.id);
const postAnchor = computed(() => `#post-${postId.value}`);

// Rating — "Рейтинг: +N" format, bold colored link
const postRating = computed(() => props.post?.rating ?? null);
const ratingText = computed(() => {
  const r = postRating.value;
  if (r === null || r === undefined) return "+0";
  if (r > 0) return `+${r}`;
  if (r === 0) return "+0";
  return `${r}`;
});
const ratingColorClass = computed(() => {
  const r = postRating.value ?? 0;
  if (r > 0) return "positive";
  if (r < 0) return "negative";
  return "neutral";
});

const hasReviews = computed(() => reviewCount.value > 0);

// Navigation (featured post mode). `post.room.game` is already a
// full sidebar-tier GameRef (master, assistants, activeCharacters,
// recruitment, subscribersCount) hydrated by the backend's batched
// EnrichGamesAsync step in PostRepository.GetRated — no per-row
// fetch, no tooltip cache, no reactivity dance. Same data-flow as
// ActiveGames / RecruitingGames in the sidebar: receive the full
// ref, pass it straight into GameLink. See PATTERNS.md → "DTO
// Projection Pattern" — GameRef is the sidebar tier with all
// tooltip-feeding fields populated.
const hasNavigation = computed(
  () => props.showNavigation && !!props.post?.room?.game?.publicId,
);

// Raw identifiers used by the "перейти к посту" router-link in the
// post footer. Resolved from the nested GameRef; the hasNavigation
// gate above ensures both are defined whenever the link renders.
const gameId = computed(() => props.post?.room?.game?.publicId);
const roomNumber = computed(() => props.post?.room?.roomNumber);

// ──────────────────────────────────────────────────────────────────────────────
// Truncation — dynamic height matching post-meta, line-aligned.
// Opt-in via `truncatable` only. Contexts that just need the breadcrumb
// (e.g. Pulse) pass `show-navigation` without `truncatable` and get full posts.
// ──────────────────────────────────────────────────────────────────────────────
const shouldTruncate = computed(() => props.truncatable);

const metaRef = ref<HTMLElement | null>(null);
const dynamicMaxHeight = ref(props.maxHeight);

function recalcMaxHeight() {
  if (!metaRef.value) return;
  const metaH = metaRef.value.offsetHeight;
  if (metaH <= 0) return;

  // Reserve space for "... показать полностью" link (~22px)
  const expandLinkH = 22;
  const available = metaH - expandLinkH;

  // Snap to full lines (approximate line-height from font)
  const lineH = 20; // 16px font * ~1.25 default line-height
  const lines = Math.max(1, Math.floor(available / lineH));
  dynamicMaxHeight.value = lines * lineH;
}

let resizeObserver: ResizeObserver | null = null;

onMounted(() => {
  nextTick(recalcMaxHeight);
  if (metaRef.value) {
    resizeObserver = new ResizeObserver(recalcMaxHeight);
    resizeObserver.observe(metaRef.value);
  }
});

onUnmounted(() => {
  resizeObserver?.disconnect();
});

// Truncation is delegated to <TruncatedContent> in the template.

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

async function submitReview() {
  if (!postId.value || !newReviewText.value.trim() || submittingReview.value) return;
  submittingReview.value = true;
  try {
    const { data } = await gameApi.createPostReview(postId.value, {
      sign: newReviewSign.value,
      text: newReviewText.value.trim(),
    });
    if (data) {
      reviews.value.push(data);
      newReviewText.value = "";
      newReviewSign.value = 1;
    }
  } finally {
    submittingReview.value = false;
  }
}
</script>

<template>
  <article :id="`post-${postId}`" class="game-post" :class="{ featured: hasNavigation }">
    <!-- Navigation breadcrumb (featured post only). `post.room.game`
         is a full sidebar-tier GameRef so GameLink / RoomLink render
         the same tooltip UX as the sidebar without a second fetch. -->
    <div v-if="hasNavigation" class="post-nav">
      <GameLink :game="post.room!.game!" />
      <span class="nav-separator"> > </span>
      <RoomLink :room="post.room!" :game="post.room!.game!" />
    </div>

    <!-- Post card (bordered area) -->
    <div class="post-card">
      <div class="post-columns">
        <!-- Left column: metadata -->
        <div ref="metaRef" class="post-meta">
          <div class="meta-inner">
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

            <!-- Avatar -->
            <img v-if="hasCharacter" :src="avatarPicture" class="avatar" />

            <!-- Date -->
            <span class="post-date">{{ formattedDate }}</span>

            <!-- Rating: "Рейтинг: <bold value>" -->
            <span class="rating-line">Рейтинг: <!--
              --><a
                v-if="hasReviews"
                class="rating-value"
                :class="ratingColorClass"
                @click="toggleReviews"
              ><b>{{ ratingText }}</b></a
              ><span v-else class="rating-value" :class="ratingColorClass"><b>{{ ratingText }}</b></span>
            </span>
          </div>
        </div>

        <!-- Right column: content -->
        <div class="post-body">
          <TruncatedContent
            :truncatable="shouldTruncate"
            :max-height="dynamicMaxHeight"
            :watch-key="postTextHtml"
          >
            <div class="game-text">
              <content-text :html="postTextHtml" />
            </div>

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

            <div v-if="hasMetagameText" class="metagame-text">
              <content-text :html="postMetagameHtml" />
            </div>
          </TruncatedContent>
        </div>
      </div>

      <!-- Post footer: number or anchor icon (inside card, like DM2 td[colspan=3]) -->
      <div class="post-footer">
        <Tooltip v-if="hasNavigation" text="Перейти к посту">
          <router-link
            class="post-link"
            :to="{ name: 'game-room', params: { id: gameId, num: roomNumber }, hash: postAnchor }"
          >{{ symbols.returnArrow }}</router-link>
        </Tooltip>
        <a v-else-if="number" class="post-number" :href="postAnchor" @click.prevent="copyAnchorLink">{{ number }}</a>
      </div>
    </div>

    <!-- Reviews section (below card, grid-based collapse animation) -->
    <div class="reviews-collapse" :class="{ expanded: showReviews }">
      <div class="reviews-overflow">
        <ul v-if="hasReviews || (showReviews && authStore.isAuthenticated)" class="reviews-section">
          <PostReviewItem v-for="review in reviews" :key="review.id" :review="review" />
          <!-- Review form (logged-in users) -->
          <li v-if="showReviews && authStore.isAuthenticated" class="review-form">
            <div class="sign-selector">
              <button class="sign-btn" :class="{ active: newReviewSign === 1, positive: newReviewSign === 1 }" @click="newReviewSign = 1">+</button>
              <button class="sign-btn" :class="{ active: newReviewSign === 0, neutral: newReviewSign === 0 }" @click="newReviewSign = 0">=</button>
              <button class="sign-btn" :class="{ active: newReviewSign === -1, negative: newReviewSign === -1 }" @click="newReviewSign = -1">−</button>
            </div>
            <textarea v-model="newReviewText" class="review-input" placeholder="Текст отзыва..." rows="2"></textarea>
            <button class="submit-btn" :disabled="!newReviewText.trim() || submittingReview" @click="submitReview">Отправить</button>
          </li>
        </ul>
      </div>
    </div>
  </article>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

// ============================================================================
// Game Post — layout dimensions matching DM2
// ============================================================================

// Navigation (featured post only, above card)
.post-nav
  margin-bottom: $small
  a
    color: $link
    // No local text-decoration override — global a:hover rule provides
    // the underline on hover.
    &:hover
      color: $link-hover

.nav-separator
  color: $text-muted

// Card: DM2 .commentItem { padding: 5px; border: 1px dashed; background }
.post-card
  padding: 5px
  background-color: $bg-element
  border: 1px dashed $border

// Two-column layout: DM2 table (firstrow 180px + 10px spacer + thirdrow auto)
.post-columns
  display: flex
  gap: 10px
  align-items: flex-start

// ──────────────────────────────────────────────────────────────────────────────
// Left column — DM2: td.firstrow { width: 140px }
// Widened to 160px so LongestLoginPossible fits on one line
// ──────────────────────────────────────────────────────────────────────────────
.post-meta
  flex-shrink: 0
  width: 160px
  text-align: left
  font-size: $font-size

// DM2: div.p { margin: 11px 5px } inside td.firstrow
.meta-inner
  margin: 11px 5px

  // DM2: gray6 is block, br tags separate elements — block children replicate this
  > *
    display: block

// Character name: #666 gray, not bold (DM2 .gray6 { color: #666 })
.character-name
  display: block
  padding-bottom: 4px
  color: $heading-alt

a.character-name
  &:hover
    color: $link-hover

// Game role (DungeonMaster / Assistant): bold, same #666 gray
.game-role
  display: block
  padding-bottom: 4px
  font-weight: bold
  color: $heading-alt

// Avatar: constrained by .meta-inner width (same as username text area)
.avatar
  display: block
  box-sizing: border-box
  width: 100%
  max-height: 500px
  height: auto
  margin: 2px 0
  border: 1px solid $border

// Date: normal text color (not muted)
.post-date
  color: $text

.system-text
  font-style: italic
  color: $text-muted

// ──────────────────────────────────────────────────────────────────────────────
// Rating: "Рейтинг: <bold +N>"
// ──────────────────────────────────────────────────────────────────────────────
// "Рейтинг:" label in normal text color, value is colored
.rating-line
  color: $text
  font-size: $font-size

.rating-value
  font-size: $font-size

  &.positive
    color: $accent-green
  &.negative
    color: $accent-red
  &.neutral
    color: $text-muted

a.rating-value
  cursor: pointer
  text-decoration: none
  &.positive:hover
    color: $accent-green-hover
  &.negative:hover
    color: $accent-red-hover
  &.neutral:hover
    color: $link-hover

// ──────────────────────────────────────────────────────────────────────────────
// Right column — DM2: td.thirdrow { valign: top; width: 100% }
// No min-height, no explicit line-height (browser default ~1.2)
// ──────────────────────────────────────────────────────────────────────────────
.post-body
  flex: 1
  min-width: 0
  padding-top: $small
  word-wrap: break-word
  word-break: break-word
  overflow-wrap: break-word
  color: $text

// Game text: DM2 div.dmtxt { margin: 11px 5px } + #main .content { padding-right: 20px }
.game-text
  color: $text
  margin: 11px 15px 11px 5px
  text-align: justify
  hyphens: auto
  -webkit-hyphens: auto

// Metagame text: DM2 div.p.postcomment { color: #7B532B }
.metagame-text
  color: $text-meta
  margin: 11px 15px 11px 5px
  text-align: justify
  hyphens: auto
  -webkit-hyphens: auto

  b, strong, i, em, u, s, strike, del, pre, code, li, span, div
    color: inherit

// Dice rolls: DM2 .diceRollsbg { padding: 0 2px } + .diceLine { margin-top: 4px }
.dice-rolls
  padding: 0 2px

.dice-roll
  padding: 2px 0
  margin-top: 4px

// DM2: .diceLine SPAN { border: 1px solid; padding: 2px; font-size: 16px; bold }
.dice-result
  font-size: $font-size
  font-weight: bold
  color: $heading
  padding: 2px
  border: 1px solid $border

.dice-total
  font-weight: bold
  color: $accent-green

.dice-comment
  color: $text-muted
  font-style: italic

// ──────────────────────────────────────────────────────────────────────────────
// Post footer — DM2: td[colspan=3] height=0, span.comment float:right
// ──────────────────────────────────────────────────────────────────────────────
.post-footer
  display: flex
  justify-content: flex-end
  padding-right: 7px

.post-number
  color: $text-muted
  font-size: $tertiary-font-size
  text-decoration: none
  &:hover
    color: $link

.post-link
  color: $link
  font-size: $secondary-font-size
  text-decoration: none
  &:hover
    color: $link-hover

// ──────────────────────────────────────────────────────────────────────────────
// Reviews section — grid-based collapse for smooth animation
// Using grid-template-rows 0fr/1fr avoids the max-height jank
// ──────────────────────────────────────────────────────────────────────────────
.reviews-collapse
  display: grid
  grid-template-rows: 0fr
  transition: grid-template-rows 0.3s ease-out
  @media (prefers-reduced-motion: reduce)
    transition: none
  &.expanded
    grid-template-rows: 1fr

.reviews-overflow
  overflow: hidden

.reviews-section
  list-style: disc
  margin: 0
  padding-top: $small
  padding-left: 25px

// ──────────────────────────────────────────────────────────────────────────────
// Review creation form (at bottom of reviews list)
// ──────────────────────────────────────────────────────────────────────────────
.review-form
  list-style: none
  margin-top: $small
  display: flex
  gap: $small
  align-items: flex-start

.sign-selector
  display: flex
  flex-direction: column
  gap: 2px

.sign-btn
  width: 28px
  height: 24px
  border: 1px solid $border
  background: $bg-element
  color: $text-muted
  cursor: pointer
  font-size: $font-size
  font-weight: bold
  padding: 0
  &:hover
    border-color: $link
  &.active.positive
    color: $accent-green
    border-color: $accent-green
  &.active.neutral
    color: $text-muted
    border-color: $text-muted
  &.active.negative
    color: $accent-red
    border-color: $accent-red

.review-input
  flex: 1
  min-height: 40px
  padding: $minor
  border: 1px solid $border
  font-size: $font-size
  font-family: inherit
  resize: vertical
  color: $text
  background: $bg-page

.submit-btn
  padding: $button-padding
  border: 1px solid $border
  background: $bg-element
  color: $link
  cursor: pointer
  font-size: $secondary-font-size
  font-weight: $button-font-weight
  &:hover
    color: $link-hover
    border-color: $link
  &:disabled
    opacity: 0.5
    cursor: default
</style>

<script setup lang="ts">
/**
 * "Рецензии" of one game — reviews of the game as a whole, one per author.
 *
 * Drawn by the same card as the profile listings and as a recommendation: a
 * green bubble with the text visible at once, authorship and date under it.
 * The game is not named in the footer here — every row on this page is about
 * the game the reader is already on, the way the site testimonials gallery
 * omits the recipient.
 *
 * Eligibility stays the server's word (GameReviewService: a post in the game,
 * a hundred posts overall, one review per author). Only the two gates the page
 * can answer by itself are drawn here — a guest, and an author whose review is
 * already on screen.
 */
import { ref, computed, type Ref } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, gameApi, GameReviewCard } from "@/entities/game";
import { useAuthStore } from "@/shared/stores/auth";
import type { GameReview } from "@/shared/api/models/game/reviews";
import type { ListEnvelope } from "@/shared/api/models/common";
import Button from "@/shared/ui/Button/Button.vue";
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { BlockTitle } from "@/shared/ui/Layout";
import { ErrorState } from "@/shared/ui/ErrorState";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { TestimonialSkeleton } from "@/entities/testimonial";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { LoginPrompt } from "@/features/auth";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { notifyFailure } from "@/shared/lib/errors";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";

const route = useRoute();
const gameStore = useGameDetailsStore();
const authStore = useAuthStore();
const { game } = storeToRefs(gameStore);
const { user } = storeToRefs(authStore);

const reviews: Ref<ListEnvelope<GameReview> | null> = ref(null);
const newReviewText = ref("");
const submitting = ref(false);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);

// Shared guard rather than hand-rolled loading/error refs: it drops answers of
// superseded requests, so fast paging can no longer land the wrong page, and
// it keeps the previous list on screen while the next one is in flight.
const {
  loading,
  error: loadError,
  run,
} = useGuardedRequest({ message: "Не удалось загрузить рецензии" });

const gameId = computed(() => game.value?.id ?? (route.params.id as string));
const page = computed(() => {
  const p = route.query.number;
  return p ? parseInt(String(p), 10) : 1;
});

const items = computed(() => reviews.value?.resources ?? []);
const paging = computed(() => reviews.value?.paging ?? null);

// The viewer's own review, among the ones on this page. One review per author
// per game is the server's rule and its answer to a second attempt is its own
// sentence; this only keeps the form off the screen where the answer is
// already visible.
const ownReview = computed(() =>
  items.value.find((r) => r.author?.username === user.value?.username),
);

// Paging scrolls the reviews list back into view (not the page top)
const reviewsListRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return reviewsListRef.value;
}

function fetchReviews() {
  if (!gameId.value) return Promise.resolve();
  return run(
    () => gameApi.getGameReviews(gameId.value, { number: page.value }),
    (data) => {
      reviews.value = data ?? null;
    },
  );
}

useFetchData(
  () => fetchReviews(),
  [
    {
      // The URL is the single source of truth for paging. The game id is
      // deliberately NOT part of the key: it flips from the route's publicId
      // to the store's GUID a beat after mount, and both name the same game,
      // so keying on it refetched an identical list on every cold load. The
      // route param covers actual navigation to another game.
      param: () => JSON.stringify({ q: route.query, id: route.params.id }),
      callback: () => fetchReviews(),
    },
  ],
);

async function submitReview() {
  if (!gameId.value || !newReviewText.value.trim() || submitting.value) return;
  submitting.value = true;
  try {
    const { error } = await gameApi.createGameReview(gameId.value, {
      text: newReviewText.value.trim(),
    });
    if (error) {
      // Every refusal of this endpoint is a sentence of the server's own (not
      // a participant, a newbie, already reviewed); notifyFailure relays the
      // one that came back. The draft stays put so a refusal costs no text.
      notifyFailure(error, "Не удалось отправить рецензию");
      return;
    }
    newReviewText.value = "";
    // clear() also drops the saved draft, so it runs only once the send landed.
    editorRef.value?.clear();
    await fetchReviews();
  } finally {
    submitting.value = false;
  }
}
</script>

<template>
  <div class="game-reviews">
    <LeadText v-once>
      Рецензии на игру целиком, по одной от участника. Кто может ее написать,
      решает сервер.
    </LeadText>

    <!-- Error banner — independent of the list: a failed refetch never hides
         the reviews already on screen. -->
    <ErrorState
      v-if="loadError"
      class="error-banner"
      :message="loadError"
      :retry="fetchReviews"
    />

    <!-- Skeleton of the same bubble the cards use, so the height it reserves
         is the height they will take; shown only while there is no data yet
         (stale-while-revalidate on paging). -->
    <TestimonialSkeleton v-if="loading && !reviews" :count="3" />

    <template v-else-if="reviews">
      <div v-if="items.length" ref="reviewsListRef" class="list">
        <PagingWithSeparators
          v-if="paging"
          :paging="paging"
          :to="{ name: 'game-reviews', params: { id: route.params.id } }"
          :use-query="true"
          :scroll-anchor="pagingAnchor"
        />

        <div class="list-items">
          <template v-for="(review, idx) in items" :key="review.id">
            <GameReviewCard :review="review" :show-game="false" />
            <DashSeparator v-if="idx < items.length - 1" spacing="tiny" />
          </template>
        </div>

        <PagingWithSeparators
          v-if="paging"
          :paging="paging"
          :to="{ name: 'game-reviews', params: { id: route.params.id } }"
          :use-query="true"
          :scroll-anchor="pagingAnchor"
        />
      </div>

      <SecondaryText v-else>Рецензий на эту игру пока нет</SecondaryText>
    </template>

    <!-- Create review form -->
    <DashSeparator class="form-separator" />
    <div class="review-form">
      <template v-if="authStore.isAuthenticated && !ownReview">
        <BlockTitle>Оставить рецензию</BlockTitle>
        <BBCodeEditor
          ref="editorRef"
          v-model="newReviewText"
          context="common"
          placeholder="Напишите рецензию на игру (минимум 10 символов)"
          :draft-key="composerDraftKey('game', 'review', game?.id)"
          :disabled="submitting"
          :min-height="120"
          :max-height="400"
          @submit="submitReview"
        />
        <Button
          type="button"
          :loading="submitting"
          :disabled="newReviewText.trim().length < 10"
          @click="submitReview"
        >
          Отправить рецензию
        </Button>
      </template>

      <SecondaryText v-else-if="ownReview">
        Ваша рецензия на эту игру уже опубликована
      </SecondaryText>

      <LoginPrompt v-else action="написать рецензию" />
    </div>
  </div>
</template>

<style scoped lang="sass">
// min-height, not padding: every other tab of the game zone reserves the same
// height, and on a game with no reviews the zone used to collapse.
.game-reviews
  min-height: $grid-step * 50

.error-banner
  margin-bottom: $medium

// Paging blocks sit $medium from the cards; the cards carry dash rules of
// their own between them.
.list
  display: flex
  flex-direction: column
  gap: $medium
  margin-top: $medium

.list-items
  display: flex
  flex-direction: column
  gap: $tiny

// The shared dash rule instead of the hand-rolled `border-top: 1px dashed`
// this page used to draw — the only such line on the site.
.form-separator
  margin-top: $big

// Column flow with the editor stretched and the button at its natural width,
// the same composer geometry as the game discussion.
.review-form
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small
  margin-top: $medium

  :deep(.bbcode-editor-wrapper)
    width: 100%
</style>

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
 * The form is offered by the server's answer for this viewer
 * (game.viewer.canReview, GameReviewService.MayCreateAsync): past the
 * probation, a post in the game, no review of theirs there yet. Whoever may not
 * write one gets the list alone, with no sentence in place of the form; the
 * author's own review is the whole answer there. A guest is invited to sign in.
 *
 * This is the one surface where a review is edited and deleted: each card is
 * GameReviewEditable, with the pencil and the trash in the bubble corner for
 * whoever may act. The profile listings draw the same card read-only.
 */
import { ref, computed, type Ref } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, gameApi } from "@/entities/game";
import { GameReviewEditable } from "@/features/game-review-edit";
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
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
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

// The viewer's own review, among the ones on this page. The server's answer
// already says there is one; this keeps the form off in the moment between a
// review sent and the game read again.
const ownReview = computed(() =>
  items.value.find((r) => r.author?.username === user.value?.username),
);

// Whether the form is offered: the server's answer, read with the game. The
// game shell reads the game again when the viewer signs in or out, so the
// answer is always for the viewer on screen.
const canWriteReview = computed(
  () =>
    authStore.isAuthenticated &&
    !!game.value?.viewer?.canReview &&
    !ownReview.value,
);

/**
 * The answer changes with what the viewer did here: a review sent closes the
 * form, the own review deleted may open it again. The game is read again,
 * quietly; a refusal of that read keeps the game on screen.
 */
function askAgainWhetherReviewable() {
  const id = game.value?.id;
  if (id) void gameStore.loadGame(id, { keepCurrentOnError: true });
}

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

// A saved edit comes back rendered by the server and takes the old row's
// place, so the list shows it without a refetch.
function replaceReview(updated: GameReview) {
  const list = reviews.value;
  if (!list) return;
  list.resources = list.resources.map((r) =>
    r.id === updated.id ? { ...r, ...updated } : r,
  );
}

// A deleted review leaves the list at once; the refetch then brings the page
// and its paging back in line with the server, and with the author's review
// gone, the form to write one returns.
function dropReview(id: string) {
  const own = ownReview.value?.id === id;
  const list = reviews.value;
  if (list) list.resources = list.resources.filter((r) => r.id !== id);
  void fetchReviews();
  if (own) askAgainWhetherReviewable();
}

/** A refused review, under the button that sent it; the draft stays. */
const submitResult = useActionResult();

async function submitReview() {
  if (!gameId.value || !newReviewText.value.trim() || submitting.value) return;
  submitting.value = true;
  submitResult.clear();
  try {
    const { error } = await gameApi.createGameReview(gameId.value, {
      text: newReviewText.value.trim(),
    });
    if (error) {
      // Every refusal of this endpoint is a sentence of the server's own (not
      // a participant, a newbie, already reviewed); the line under the button
      // relays the one that came back. The draft stays put so a refusal costs
      // no text.
      submitResult.refuse(error, "Не удалось отправить рецензию");
      return;
    }
    newReviewText.value = "";
    // clear() also drops the saved draft, so it runs only once the send landed.
    editorRef.value?.clear();
    await fetchReviews();
    askAgainWhetherReviewable();
  } finally {
    submitting.value = false;
  }
}
</script>

<template>
  <div class="game-reviews">
    <LeadText v-once>
      Рецензии на игру целиком, по одной от участника
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
            <GameReviewEditable
              :review="review"
              :show-game="false"
              @updated="replaceReview"
              @deleted="dropReview"
            />
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

    <!-- Create review form: for whoever may write one, and the way in for a
         guest. -->
    <template v-if="!authStore.isAuthenticated || canWriteReview">
      <DashSeparator class="form-separator" />
      <div class="review-form">
        <template v-if="authStore.isAuthenticated">
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
            variant="primary"
            :loading="submitting"
            :disabled="newReviewText.trim().length < 10"
            @click="submitReview"
          >
            Отправить рецензию
          </Button>
          <ActionResult :result="submitResult" class="review-refusal" />
        </template>

        <LoginPrompt v-else action="написать рецензию" />
      </div>
    </template>
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

  // Spaced by the gap of the column.
  .review-refusal
    --action-result-margin: 0
</style>

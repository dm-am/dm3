<script setup lang="ts">
// The reviews of one game, one per author; the one place a review is edited.
import { ref, computed } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import {
  useGameDetailsStore,
  gameReviewApi,
  GameStatus,
} from "@/entities/game";
import { GameReviewEditable } from "@/features/game-review-edit";
import { useAuthStore } from "@/entities/user";
import { reachesByRank } from "@/entities/moderation";
import { useReviewWarnDialog } from "@/features/moderation-actions";
import type { GameReview } from "@/shared/api/models/game/reviews";
import type { ListEnvelope } from "@/shared/api/models/common";
import Button from "@/shared/ui/Button/Button.vue";
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
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
import { parsePageNumber } from "@/shared/lib/filters/utils";

const route = useRoute();
const gameStore = useGameDetailsStore();
const authStore = useAuthStore();
const { game } = storeToRefs(gameStore);
const { user } = storeToRefs(authStore);

// A review is a place a warning names; only a rank below the reader's, as for a comment.
const canWarn = (review: GameReview) =>
  reachesByRank(user.value, review.author);
const { warn } = useReviewWarnDialog("GameReview");

const reviews = ref<ListEnvelope<GameReview> | null>(null);
const newReviewText = ref("");
const submitting = ref(false);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);

const {
  loading,
  error: loadError,
  run,
} = useGuardedRequest({ message: "Не удалось загрузить рецензии" });

const gameId = computed(() => game.value?.id ?? (route.params.id as string));
const page = computed(() => parsePageNumber(route.query.number) ?? 1);

const items = computed(() => reviews.value?.resources ?? []);
const paging = computed(() => reviews.value?.paging ?? null);

// Keeps the form off between a review sent and the game read again.
const ownReview = computed(() =>
  items.value.find((r) => r.author?.username === user.value?.username),
);

const canWriteReview = computed(
  () =>
    authStore.isAuthenticated &&
    !!game.value?.viewer?.canReview &&
    !ownReview.value,
);

// A review is written on a closed game alone, so a guest is not asked to sign in for one elsewhere.
const guestMayBeAsked = computed(
  () => !authStore.isAuthenticated && game.value?.status === GameStatus.Closed,
);

// A review sent or deleted here changes the server's answer.
function reloadReviewability() {
  const id = game.value?.id;
  if (id) void gameStore.loadGame(id, { keepCurrentOnError: true });
}

const reviewsListRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return reviewsListRef.value;
}

const pagingTo = computed(() => ({
  name: "game-reviews",
  params: { id: route.params.id },
}));

function fetchReviews() {
  return run(
    () => gameReviewApi.getGameReviews(gameId.value, { number: page.value }),
    (data) => {
      reviews.value = data ?? null;
    },
  );
}

useFetchData(
  () => fetchReviews(),
  [
    {
      // Not gameId: it turns from the public id to the guid after mount.
      param: () => JSON.stringify({ q: route.query, id: route.params.id }),
      callback: () => fetchReviews(),
    },
  ],
);

function replaceReview(updated: GameReview) {
  const list = reviews.value;
  if (!list) return;
  list.resources = list.resources.map((r) =>
    r.id === updated.id ? { ...r, ...updated } : r,
  );
}

function dropReview(id: string) {
  const own = ownReview.value?.id === id;
  const list = reviews.value;
  if (list) list.resources = list.resources.filter((r) => r.id !== id);
  void fetchReviews();
  if (own) reloadReviewability();
}

const submitResult = useActionResult();

async function submitReview() {
  if (!newReviewText.value.trim() || submitting.value) return;
  submitting.value = true;
  submitResult.clear();
  try {
    const { error } = await gameReviewApi.createGameReview(gameId.value, {
      text: newReviewText.value,
    });
    if (error) {
      submitResult.refuse(error, "Не удалось отправить рецензию");
      return;
    }
    newReviewText.value = "";
    // clear() drops the saved draft too, so only after the send landed.
    editorRef.value?.clear();
    await fetchReviews();
    reloadReviewability();
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

    <ErrorState
      v-if="loadError"
      class="error-banner"
      :message="loadError"
      :retry="fetchReviews"
    />

    <TestimonialSkeleton v-if="loading && !reviews" :count="3" />

    <template v-else-if="reviews">
      <div v-if="items.length" ref="reviewsListRef" class="list">
        <PagingWithSeparators
          v-if="paging"
          :paging="paging"
          :to="pagingTo"
          use-query
          :scroll-anchor="pagingAnchor"
        />

        <div class="list-items">
          <template v-for="(review, idx) in items" :key="review.id">
            <GameReviewEditable
              :review="review"
              :show-game="false"
              :can-warn="canWarn(review)"
              @updated="replaceReview"
              @deleted="dropReview"
              @warn="warn"
            />
            <DashSeparator v-if="idx < items.length - 1" spacing="tiny" />
          </template>
        </div>

        <PagingWithSeparators
          v-if="paging"
          :paging="paging"
          :to="pagingTo"
          use-query
          :scroll-anchor="pagingAnchor"
        />
      </div>

      <SecondaryText v-else>Рецензий на эту игру пока нет</SecondaryText>
    </template>

    <template v-if="guestMayBeAsked || canWriteReview">
      <DashSeparator class="form-separator" />
      <div class="review-form">
        <template v-if="authStore.isAuthenticated">
          <BlockTitle>Оставить рецензию</BlockTitle>
          <BBCodeEditor
            ref="editorRef"
            v-model="newReviewText"
            context="common"
            placeholder="Напишите рецензию на игру"
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
            :disabled="!newReviewText.trim()"
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
.game-reviews
  min-height: $grid-step * 50

.error-banner
  margin-bottom: $medium

.list
  display: flex
  flex-direction: column
  gap: $medium
  margin-top: $medium

.list-items
  display: flex
  flex-direction: column
  gap: $tiny

.form-separator
  margin-top: $big

.review-form
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small
  margin-top: $medium

  :deep(.bbcode-editor-wrapper)
    width: 100%

  .review-refusal
    --action-result-margin: 0
</style>

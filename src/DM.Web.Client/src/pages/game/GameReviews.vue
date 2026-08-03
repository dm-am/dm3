<script setup lang="ts">
/**
 * "Рецензии" of one game — reviews of the game as a whole, one per author.
 *
 * The page had the data and none of a page's frame: no heading, no failure
 * state, and a form that was a bare textarea with a bare button while every
 * other composer on the site is the BBCode editor with a draft that survives a
 * closed tab. A guest saw no form and no reason for its absence.
 *
 * Eligibility stays the server's word (GameReviewService: a post in the game,
 * a hundred posts overall, one review per author). Only the two gates the page
 * can answer by itself are drawn here — a guest, and an author whose review is
 * already on screen.
 */
import { ref, computed, onMounted, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, gameApi } from "@/entities/game";
import { useAuthStore } from "@/shared/stores/auth";
import type { GameReview } from "@/shared/api/models/game/reviews";
import type { ListEnvelope } from "@/shared/api/models/common";
import { ContentText } from "@/shared/ui";
import Button from "@/shared/ui/Button/Button.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import LeadText from "@/shared/ui/Layout/LeadText.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ExpandableListSkeleton } from "@/shared/ui/Skeleton";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { LoginPrompt } from "@/features/auth";
import Paging from "@/shared/ui/Paging/Paging.vue";
import {
  ExpandableList,
  type ExpandableItem,
} from "@/shared/ui/ExpandableList";
import { composerDraftKey } from "@/shared/lib/utils/draftKey";
import { formatDateFull } from "@/shared/lib/utils/datetime";
import { notifyFailure } from "@/shared/lib/errors";

const route = useRoute();
const gameStore = useGameDetailsStore();
const authStore = useAuthStore();
const { game } = storeToRefs(gameStore);
const { user } = storeToRefs(authStore);

const reviews = ref<ListEnvelope<GameReview> | null>(null);
const loading = ref(false);
const loadError = ref<string | null>(null);
const newReviewText = ref("");
const submitting = ref(false);
const editorRef = ref<InstanceType<typeof BBCodeEditor> | null>(null);

const gameId = computed(() => game.value?.id ?? (route.params.id as string));
const page = computed(() => {
  const p = route.query.number;
  return p ? parseInt(String(p), 10) : 1;
});

// Map reviews to ExpandableItem format for ExpandableList.
// Title shows "Author, Date" as plain text (ExpandableList renders it): a
// comma, because the em dash is out of interface copy.
const reviewItems = computed<(ExpandableItem & { review: GameReview })[]>(() =>
  (reviews.value?.resources ?? []).map((r) => ({
    id: r.id,
    title: `${r.author?.username ?? "Аноним"}, ${formatDateFull(r.createdUtc)}`,
    review: r,
  })),
);

// The viewer's own review, among the ones on this page. One review per author
// per game is the server's rule and its answer to a second attempt is its own
// sentence; this only keeps the form off the screen where the answer is
// already visible.
const ownReview = computed(() =>
  (reviews.value?.resources ?? []).find(
    (r) => r.author?.username === user.value?.username,
  ),
);

// Paging scrolls the reviews list back into view (not the page top)
const reviewsListRef = ref<{ $el: HTMLElement } | null>(null);
function pagingAnchor(): HTMLElement | null {
  return reviewsListRef.value?.$el ?? null;
}

async function fetchReviews() {
  if (!gameId.value) return;
  loading.value = true;
  try {
    const { data, error } = await gameApi.getGameReviews(gameId.value, {
      number: page.value,
    });
    if (error) {
      // Keep whatever is already on screen; the banner renders above the list,
      // the way a failed refetch is answered on /pulse.
      loadError.value = "Не удалось загрузить рецензии";
      return;
    }
    loadError.value = null;
    reviews.value = data ?? null;
  } finally {
    loading.value = false;
  }
}

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

onMounted(fetchReviews);
watch(page, fetchReviews);
</script>

<template>
  <div class="game-reviews">
    <BlockTitle>Рецензии</BlockTitle>
    <LeadText v-once>
      Отзывы об игре целиком, по одной рецензии от участника. Написать ее может
      тот, у кого в этой игре есть хотя бы один пост.
    </LeadText>

    <!-- Error banner — independent of the list: a failed refetch never hides
         the reviews already on screen. -->
    <ErrorState
      v-if="loadError"
      class="error-banner"
      :message="loadError"
      :retry="fetchReviews"
    />

    <!-- Skeleton twin of the collapsed reviews ExpandableList; shown only
         while there is no data yet (stale-while-revalidate on paging). -->
    <ExpandableListSkeleton v-if="loading && !reviews" />

    <template v-else-if="reviews">
      <ExpandableList
        v-if="reviewItems.length"
        ref="reviewsListRef"
        :items="reviewItems"
        :allow-multiple="true"
      >
        <template #content="{ item }">
          <div class="review-text">
            <ContentText :html="item.review.text" />
          </div>
        </template>
      </ExpandableList>

      <SecondaryText v-else>Рецензий на эту игру пока нет</SecondaryText>

      <Paging
        v-if="reviews.paging && reviews.paging.pages > 1"
        :paging="reviews.paging"
        :to="{ name: 'game-reviews', params: { id: route.params.id } }"
        :use-query="true"
        :scroll-anchor="pagingAnchor"
        class="pagination"
      />
    </template>

    <!-- Create review form -->
    <div class="review-form">
      <template v-if="authStore.isAuthenticated && !ownReview">
        <h3>Оставить рецензию</h3>
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
.game-reviews
  padding: $small 0

.error-banner
  margin-bottom: $medium

// Line-height comes from the global .bbcode-content (SSOT) on ContentText.
.review-text
  color: $text

.pagination
  margin-top: $medium

// Column flow with the editor stretched and the button at its natural width,
// the same composer geometry as the game discussion.
.review-form
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small
  margin-top: $big
  padding-top: $medium
  border-top: 1px dashed $border

  h3
    margin: 0
    font-size: $font-size
    font-weight: 600
    color: $heading

  :deep(.bbcode-editor-wrapper)
    width: 100%
</style>

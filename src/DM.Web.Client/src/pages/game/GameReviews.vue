<script setup lang="ts">
import { ref, computed, onMounted, watch } from "vue";
import { useRoute } from "vue-router";
import { storeToRefs } from "pinia";
import { useGameDetailsStore, gameApi } from "@/entities/game";
import { useAuthStore } from "@/shared/stores/auth";
import type { GameReview } from "@/shared/api/models/game/reviews";
import type { ListEnvelope } from "@/shared/api/models/common";
import { ContentText } from "@/shared/ui";
import { UserLink } from "@/entities/user";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import Paging from "@/shared/ui/Paging/Paging.vue";
import {
  ExpandableList,
  type ExpandableItem,
} from "@/shared/ui/ExpandableList";
import dayjs from "dayjs";

const route = useRoute();
const gameStore = useGameDetailsStore();
const authStore = useAuthStore();
const { game } = storeToRefs(gameStore);

const reviews = ref<ListEnvelope<GameReview> | null>(null);
const loading = ref(false);
const newReviewText = ref("");
const submitting = ref(false);
const error = ref<string | null>(null);

const gameId = computed(() => game.value?.id ?? (route.params.id as string));
const page = computed(() => {
  const p = route.query.number;
  return p ? parseInt(String(p), 10) : 1;
});

// Map reviews to ExpandableItem format for ExpandableList.
// Title shows "Author — Date" as plain text (ExpandableList renders it).
const reviewItems = computed<(ExpandableItem & { review: GameReview })[]>(() =>
  (reviews.value?.resources ?? []).map((r) => ({
    id: r.id,
    title: `${r.author?.username ?? "Аноним"} — ${formatDate(r.createdUtc)}`,
    review: r,
  })),
);

async function fetchReviews() {
  if (!gameId.value) return;
  loading.value = true;
  try {
    const { data } = await gameApi.getGameReviews(gameId.value, {
      number: page.value,
    });
    reviews.value = data ?? null;
  } finally {
    loading.value = false;
  }
}

async function submitReview() {
  if (!gameId.value || !newReviewText.value.trim() || submitting.value) return;
  submitting.value = true;
  error.value = null;
  try {
    const { data, error: apiError } = await gameApi.createGameReview(
      gameId.value,
      { text: newReviewText.value.trim() },
    );
    if (data) {
      newReviewText.value = "";
      await fetchReviews();
    } else if (apiError) {
      error.value = (apiError as any).title || "Ошибка при отправке рецензии";
    }
  } finally {
    submitting.value = false;
  }
}

function formatDate(dateStr: string) {
  return dayjs(dateStr).format("DD.MM.YYYY [в] HH:mm");
}

onMounted(fetchReviews);
watch(page, fetchReviews);
</script>

<template>
  <div class="game-reviews">
    <SecondaryText v-if="loading">Загрузка...</SecondaryText>

    <SecondaryText
      v-else-if="
        reviews && !reviews.resources.length && !authStore.isAuthenticated
      "
    >
      Рецензий на эту игру пока нет
    </SecondaryText>

    <template v-else-if="reviews">
      <ExpandableList
        v-if="reviewItems.length"
        :items="reviewItems"
        :allow-multiple="true"
      >
        <template #content="{ item }">
          <div class="review-text">
            <ContentText :html="item.review.text" />
          </div>
        </template>
      </ExpandableList>

      <SecondaryText v-else>
        Рецензий на эту игру пока нет. Будьте первым!
      </SecondaryText>

      <Paging
        v-if="reviews.paging && reviews.paging.pages > 1"
        :paging="reviews.paging"
        :to="{ name: 'game-reviews', params: { id: route.params.id } }"
        :use-query="true"
        class="pagination"
      />
    </template>

    <!-- Create review form -->
    <div v-if="authStore.isAuthenticated" class="review-form">
      <h3>Оставить рецензию</h3>
      <div v-if="error" class="form-error">{{ error }}</div>
      <textarea
        v-model="newReviewText"
        class="review-textarea"
        rows="4"
        placeholder="Напишите вашу рецензию на игру (минимум 10 символов)..."
      />
      <button
        class="submit-btn"
        :disabled="newReviewText.trim().length < 10 || submitting"
        @click="submitReview"
      >
        {{ submitting ? "Отправка..." : "Отправить рецензию" }}
      </button>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"

.game-reviews
  padding: $small 0

.review-text
  color: $text
  line-height: 1.5

.pagination
  margin-top: $medium

.review-form
  margin-top: $big
  padding-top: $medium
  border-top: 1px dashed $border

  h3
    margin: 0 0 $small
    font-size: $font-size
    font-weight: 600
    color: $heading

.form-error
  color: $accent-red
  font-size: $secondary-font-size
  margin-bottom: $small

.review-textarea
  width: 100%
  padding: $small
  border: 1px solid $border
  font-size: $font-size
  font-family: inherit
  resize: vertical
  color: $text
  background: $bg-page
  box-sizing: border-box

.submit-btn
  margin-top: $small
  +button
</style>

<script setup lang="ts">
/** The reviews under the card and the form, here so a draft survives a close. */
import { computed, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import type { PostReview } from "@/entities/game";
import {
  postApi,
  PostReviewItem,
  reviewSignOptions,
  reviewSignToNumber,
} from "@/entities/game";
import { unwrapResource } from "@/shared/api";
import { reachesByRank } from "@/entities/moderation";
import { useAuthStore } from "@/entities/user";
import { useReviewWarnDialog } from "@/features/moderation-actions";
import { refusalHere } from "@/shared/lib/errors";
import { ActionResult } from "@/shared/ui/ActionResult";
import { Button } from "@/shared/ui/Button";
import { SecondaryText } from "@/shared/ui/Layout";
import { SegmentedControl } from "@/shared/ui/SegmentedControl";
import type { PostReviews } from "./usePostReviews";

const props = defineProps<{
  reviews: PostReviews;
  /** Lets the reviews offer their own edit and delete (PostReviewItem). */
  editable: boolean;
}>();

const { user: currentUser } = storeToRefs(useAuthStore());

// The text of a review is a place a warning names; only a rank below the reader's, as for a comment.
const canWarn = (review: PostReview) =>
  props.editable && reachesByRank(currentUser.value, review.author);
const { warn } = useReviewWarnDialog("PostReview");

const newReviewSign = ref<number>(1);
const newReviewText = ref("");
const submittingReview = ref(false);
// The refusal of the last send, under the text it refused.
const reviewRefusal = ref<string | null>(null);

/** The segments of the sign toggle: "=" alone for a newbie. */
const reviewSigns = computed(() =>
  reviewSignOptions(props.reviews.canPickSigned),
);

// Keep the selected sign valid for newbies (they may only pick "=").
watch(
  () => props.reviews.canPickSigned,
  (canSign) => {
    if (!canSign) newReviewSign.value = 0;
  },
  { immediate: true },
);

async function submitReview() {
  const postId = props.reviews.postId;
  if (!postId || !newReviewText.value.trim() || submittingReview.value) return;
  submittingReview.value = true;
  reviewRefusal.value = null;
  const sign = newReviewSign.value;
  try {
    const { data, error } = await postApi.createPostReview(postId, {
      sign,
      text: newReviewText.value,
    });
    const created = unwrapResource<PostReview>(data);
    if (created) {
      newReviewText.value = "";
      newReviewSign.value = props.reviews.canPickSigned ? 1 : 0;
      props.reviews.onCreated(created, sign);
    } else if (error) {
      // The draft stays and the refusal stands under it; a 401 and a dead connection keep the plate.
      reviewRefusal.value = refusalHere(error, "Не удалось отправить оценку");
    }
  } finally {
    submittingReview.value = false;
  }
}
</script>

<template>
  <div
    :id="reviews.collapseId"
    class="reviews-collapse expand-fold"
    :class="{ open: reviews.open }"
    :inert="!reviews.open"
  >
    <div class="expand-fold-clip">
      <SecondaryText v-if="reviews.loading" class="reviews-status">
        Загрузка...
      </SecondaryText>
      <SecondaryText v-else-if="reviews.error" class="reviews-status">
        {{ reviews.error }}
      </SecondaryText>
      <!-- The reviews and the form for whoever may still write one -->
      <ul
        v-if="reviews.hasReviews || (reviews.open && reviews.showForm)"
        class="reviews-section"
      >
        <PostReviewItem
          v-for="(review, i) in reviews.list"
          :key="review.id"
          :review="review"
          :number="i + 1"
          :to="reviews.routeOf(review.id)"
          :highlight="reviews.isHighlighted(review)"
          :editable="editable"
          :can-warn="canWarn(review)"
          @updated="reviews.onUpdated"
          @deleted="reviews.onDeleted"
          @warn="warn"
        />
        <li v-if="reviews.open && reviews.showForm" class="review-form">
          <SegmentedControl
            :model-value="String(newReviewSign)"
            :options="reviewSigns"
            ariaLabel="Знак оценки"
            @update:model-value="newReviewSign = reviewSignToNumber($event)"
          />
          <textarea
            v-model="newReviewText"
            class="review-input"
            placeholder="Текст оценки..."
            aria-label="Текст оценки"
            rows="2"
          ></textarea>
          <Button
            type="button"
            variant="primary"
            class="submit-btn"
            :disabled="!newReviewText.trim() || submittingReview"
            @click="submitReview"
          >
            Отправить
          </Button>
          <ActionResult :text="reviewRefusal" class="form-error" />
        </li>
      </ul>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/ReviewParts" as *

// The old site's 40px bullet indent.
.reviews-section
  +review-list

// The loading or error line.
.reviews-status
  padding-top: $small
  padding-left: 3 * $small

// A column: the sign, the text, the button at the left, as every composer stands.
.review-form
  list-style: none
  margin-top: $small
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $small

// A text area of the site, across the column, growing down only.
.review-input
  box-sizing: border-box
  width: 100%
  resize: vertical

// No margin of its own: the gap of the column spaces it.
.form-error
  --action-result-margin: 0
</style>

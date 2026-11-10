<script setup lang="ts">
/** The counters of a profile, each a link to the list behind it where there is one. */
import { computed } from "vue";
import type { User } from "@/shared/api/models/community";
import {
  DATE_TIME_FORMAT,
  formatDate,
  formatSiteTime,
} from "@/shared/lib/utils/datetime";
import { isUserOnline } from "@/shared/lib/utils/chat";
import { useNowTimestamp } from "@/shared/lib/composables/useNowTimestamp";
import { VALUE_UNAVAILABLE } from "@/shared/lib/constants/copy";
import { StatLine } from "@/shared/ui/StatLine";
import type { ProfileSubpageName } from "./profileSubpages";

const props = defineProps<{
  user: User;
  /** The name in the address, which the links of the counters carry. */
  username: string;
  isRemovedAccount: boolean;
}>();

// Asked again on every minute's tick, so the mark turns offline on a page left open.
const now = useNowTimestamp();
const isOnline = computed(() => {
  void now.value;
  return isUserOnline(props.user.lastActivityUtc);
});

const lastActivityFormatted = computed(() =>
  props.user.lastActivityUtc
    ? formatSiteTime(props.user.lastActivityUtc, DATE_TIME_FORMAT)
    : "",
);

const registrationDate = computed(() =>
  formatDate(props.user.registeredUtc, ""),
);

const reviewsGiven = computed(() => props.user.reviewsGiven ?? 0);
const endorsementsReceived = computed(
  () => props.user.endorsementsReceived ?? 0,
);
const endorsementsGiven = computed(() => props.user.endorsementsGiven ?? 0);
// Reviews of whole games. Not the same datum as `reviewsGiven` above, which
// counts ratings of single posts.
const gameReviewsReceived = computed(() => props.user.gameReviewsReceived ?? 0);
const gameReviewsGiven = computed(() => props.user.gameReviewsGiven ?? 0);

/** The list behind a counter, of the user the address names. */
const listLink = (name: ProfileSubpageName) => ({
  name,
  params: { username: props.username },
});

const ratingSum = computed<number | null>(() => {
  const r = props.user.rating;
  if (!r) return null;
  return r.postReviewScoreSum ?? 0;
});

const ratingSumDisplay = computed<string>(() => {
  const v = ratingSum.value;
  if (v === null) return VALUE_UNAVAILABLE;
  return v > 0 ? `+${v}` : String(v);
});

const ratingSumVariant = computed<"positive" | "negative" | "muted">(() => {
  const v = ratingSum.value;
  if (v === null || v === 0) return "muted";
  return v > 0 ? "positive" : "negative";
});

const gamePostsCount = computed<number>(
  () => props.user.rating?.totalPosts ?? 0,
);
</script>

<template>
  <div class="stats-group">
    <StatLine
      v-if="!isRemovedAccount"
      label="Дата регистрации"
      :value="registrationDate"
    />
    <!-- "Аккаунт удален" sums every removed account, and its lists are not answered: plain numbers. -->
    <StatLine
      label="Рейтинг"
      :value="ratingSumDisplay"
      :variant="ratingSumVariant"
      :to="
        ratingSum !== null && !isRemovedAccount
          ? listLink('received-reviews')
          : undefined
      "
    />
    <!-- The rating beside the posts rated, so given and received compare. -->
    <StatLine
      label="Оценено чужих постов"
      :value="reviewsGiven"
      :to="isRemovedAccount ? undefined : listLink('given-reviews')"
    />
    <StatLine label="Написано постов" :value="gamePostsCount" />
    <StatLine
      v-if="!isRemovedAccount"
      label="Последняя активность"
      :value="isOnline ? 'online' : lastActivityFormatted"
      :variant="isOnline ? 'positive' : 'default'"
      :emphasized="false"
    />
    <!-- Recommendations are a subgroup of their own, a step apart. -->
    <div v-if="!isRemovedAccount" class="endorsement-stats">
      <StatLine
        label="Получено рекомендаций"
        :value="endorsementsReceived"
        :to="
          endorsementsReceived > 0
            ? listLink('received-endorsements')
            : undefined
        "
      />
      <StatLine
        label="Написано рекомендаций"
        :value="endorsementsGiven"
        :to="endorsementsGiven > 0 ? listLink('given-endorsements') : undefined"
      />
    </div>
    <!-- Reviews of whole games, worded apart from the ratings of single posts above. -->
    <div v-if="!isRemovedAccount" class="game-review-stats">
      <StatLine
        label="Получено рецензий на игры"
        :value="gameReviewsReceived"
        :to="
          gameReviewsReceived > 0
            ? listLink('received-game-reviews')
            : undefined
        "
      />
      <StatLine
        label="Написано рецензий на игры"
        :value="gameReviewsGiven"
        :to="gameReviewsGiven > 0 ? listLink('given-game-reviews') : undefined"
      />
    </div>
  </div>
</template>

<style scoped lang="sass">
// $minor inside every group of stat lines of the profile; the groups stand $medium apart.
.stats-group,
.endorsement-stats,
.game-review-stats
  display: flex
  flex-direction: column
  align-items: flex-start
  gap: $minor

// Endorsements and game reviews are subgroups: the $medium step above each is
// larger than the $minor inside, so each reads as a unit of its own.
.endorsement-stats,
.game-review-stats
  margin-top: $medium
</style>

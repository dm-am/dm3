<script setup lang="ts">
/**
 * ProfileGameReviewsList — the shared game-reviews list with pagination. Used
 * by both pages: "Полученные рецензии на игры" and "Написанные рецензии на
 * игры".
 *
 * Differences between the modes:
 *  - endpoint: `getUserGameReviews` (received) vs `getWrittenUserGameReviews`
 *    (written);
 *  - the route name for Paging links (the `routeName` prop);
 *  - the row title: in "received" the author varies and leads the line, in
 *    "written" the author is the profile owner on every row and the game
 *    leads instead.
 * Everything else is shared, the fetch and the pagination included.
 *
 * Why not the endorsements card: a recommendation is plain text in a speech
 * bubble, a game review is BBCode the server renders. The row is therefore the
 * same collapsed accordion the game's own "Рецензии" tab already uses, so a
 * review reads identically wherever it is met.
 *
 * Architecture:
 *  - `useFetchData` listens to route.query (+ username/mode via a closure)
 *    and re-runs the fetch.
 *  - Server-side pagination — paging info comes from the ListEnvelope.
 */
import { ref, computed, watch, type Ref } from "vue";
import { useRoute } from "vue-router";
import { userApi } from "@/entities/user";
import type { Username } from "@/shared/api/models/community";
import type { GameReview } from "@/shared/api/models/game/reviews";
import type { ListEnvelope } from "@/shared/api/models/common";
import { ContentText } from "@/shared/ui/Content";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import { SecondaryText } from "@/shared/ui/Layout";
import { ErrorState } from "@/shared/ui/ErrorState";
import { ExpandableListSkeleton } from "@/shared/ui/Skeleton";
import {
  ExpandableList,
  type ExpandableItem,
} from "@/shared/ui/ExpandableList";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";
import { formatDateFull } from "@/shared/lib/utils/datetime";

const props = defineProps<{
  username: string;
  /**
   * "received" — reviews of the games this user masters.
   * "written"  — reviews this user wrote.
   * Determines which of userApi.getUserGameReviews /
   * getWrittenUserGameReviews is called, and (via `routeName` below) where the
   * pagination links lead.
   */
  mode: "received" | "written";
  /** The current page's route name — for Paging.to. */
  routeName: "received-game-reviews" | "given-game-reviews";
}>();

const route = useRoute();

const envelope: Ref<ListEnvelope<GameReview> | null> = ref(null);

// Keeps any already-shown items on a failure (stale-while-revalidate); the
// ErrorState banner renders independently above the list — see template — so
// the previous message is not cleared until the next answer lands.
const {
  loading,
  error: loadError,
  clearError,
  run,
} = useGuardedRequest({ message: "Не удалось загрузить рецензии" });

// Username/mode change (navigating between "received" and "written" pages, or
// to another profile) must drop the previous list immediately.
watch(
  () => `${props.username}:${props.mode}`,
  () => {
    envelope.value = null;
    clearError();
  },
);

const page = computed(() => {
  const number = route.query.number;
  return number ? parseInt(String(number), 10) : 1;
});

function fetch() {
  // Do not detach the method — both calls go through
  // `this.buildGameReviewParams(q)`, and a detached `const fn =
  // userApi.getX` loses `this` and crashes with a TypeError.
  const params = { number: page.value };
  return run(
    () =>
      props.mode === "received"
        ? userApi.getUserGameReviews(props.username as Username, params)
        : userApi.getWrittenUserGameReviews(props.username as Username, params),
    (data) => {
      envelope.value = data;
    },
  );
}

useFetchData(
  () => fetch(),
  [
    {
      // The URL is the single source of truth for paging: route.query changed
      // → refetch. Username/mode come in via the props closure so they are
      // covered too (mode switches received/written without changing
      // route.query, hence explicitly part of the key).
      param: () =>
        JSON.stringify({ q: route.query, u: props.username, m: props.mode }),
      callback: () => fetch(),
    },
  ],
);

/**
 * Row headings. The game leads in "written" mode, where the author is the
 * profile owner on every row and naming him again says nothing; the author
 * leads in "received", where he is what differs between rows. Parts joined by
 * a comma, because the em dash is out of interface copy.
 */
const items = computed<(ExpandableItem & { review: GameReview })[]>(() =>
  (envelope.value?.resources ?? []).map((review) => {
    const game = review.gameTitle ?? "Игра без названия";
    const date = formatDateFull(review.createdUtc);
    return {
      id: review.id,
      title:
        props.mode === "written"
          ? `${game}, ${date}`
          : `${review.author?.username ?? "Аноним"}, ${game}, ${date}`,
      review,
    };
  }),
);

const paging = computed(() => envelope.value?.paging ?? null);
const isEmpty = computed(
  () => envelope.value !== null && items.value.length === 0,
);

const emptyText = computed(() =>
  props.mode === "received"
    ? "На игры пользователя пока не писали рецензий"
    : "Пользователь пока не писал рецензий на игры",
);

const pagingTo = computed(() => ({
  name: props.routeName,
  params: { username: props.username },
}));

// Paging scrolls the reviews block (top paging + rows) back into view instead
// of the page top.
const listRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return listRef.value;
}
</script>

<template>
  <div class="user-game-reviews-list">
    <!-- Error banner — independent of the list, matches /pulse: never hides
         already-loaded items on a failed refetch. 404-vs-failure distinction
         (does this profile exist at all) is resolved one level up by
         useProfileSubpageUser; this ErrorState is purely for "the request to
         load reviews failed". -->
    <ErrorState
      v-if="loadError"
      class="error-banner"
      :message="loadError"
      :retry="fetch"
    />

    <ExpandableListSkeleton v-if="loading && !envelope" />

    <SecondaryText v-else-if="isEmpty">
      {{ emptyText }}
    </SecondaryText>

    <div v-else-if="items.length" ref="listRef" class="list">
      <PagingWithSeparators
        v-if="paging"
        :paging="paging"
        :to="pagingTo"
        :use-query="true"
        :scroll-anchor="pagingAnchor"
      />

      <ExpandableList :items="items" :allow-multiple="true">
        <template #content="{ item }">
          <div class="review-text">
            <ContentText :html="item.review.text" />
          </div>
        </template>
      </ExpandableList>

      <PagingWithSeparators
        v-if="paging"
        :paging="paging"
        :to="pagingTo"
        :use-query="true"
        :scroll-anchor="pagingAnchor"
      />
    </div>
  </div>
</template>

<style scoped lang="sass">
.user-game-reviews-list
  display: flex
  flex-direction: column
  gap: $small

.error-banner
  margin-bottom: $medium

// Paging blocks sit $medium from the review rows; the rows carry their own
// separators inside ExpandableList.
.list
  display: flex
  flex-direction: column
  gap: $medium
  margin-top: $medium

// Line-height comes from the global .bbcode-content (SSOT) on ContentText.
.review-text
  color: $text
</style>

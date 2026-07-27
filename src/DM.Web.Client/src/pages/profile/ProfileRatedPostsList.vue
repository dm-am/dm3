<script setup lang="ts">
/**
 * ProfileRatedPostsList — the shared rated posts list for two profile
 * pages: "Оценки постов пользователя" (received) and "Оценил чужих
 * постов" (given). A mirror of ProfileEndorsementsList architecturally.
 *
 * The modes differ only in how the scope param is sent to the
 * server:
 *   - received → posts where this user is the AUTHOR
 *     (authorUsernames=username)
 *   - given    → posts where this user has at least one review
 *     (reviewerUsername=username) — the filter was added to PostsQuery
 *     specifically for this page.
 *
 * Architecture:
 *  - `usePulseFilter` manages the URL state of user filters
 *    (search, sort, rating, dates). The "Авторы" option is hidden
 *    via `hide-author-filter` because the author scope is nailed
 *    down by the route and must stay out of the editable field.
 *  - `useFetchData` listens to route.query / username and refetches.
 *  - Server-side pagination via `PagingWithSeparators`.
 *  - Rendered via `<GamePost show-navigation>` — the same component
 *    as on /pulse, a single visual unit.
 */
import { ref, computed, watch, type Ref } from "vue";
import { useRoute } from "vue-router";
import { gameApi } from "@/entities/game";
import type { Post } from "@/entities/game";
import type { ListEnvelope } from "@/shared/api/models/common";
import { PulseFilter, usePulseFilter } from "@/features/pulse-filter";
import { GamePost } from "@/widgets/game-post";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import { SecondaryText } from "@/shared/ui/Layout";
import { ErrorState } from "@/shared/ui/ErrorState";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";

const props = defineProps<{
  username: string;
  /**
   * "received" — posts authored by this user (they are the author).
   * "given"    — posts where this user left a review
   *              (they are the reviewer).
   * Determines which scope param goes to gameApi.getRatedPosts.
   */
  mode: "received" | "given";
  /** The current page's route name — for Paging.to. */
  routeName: "received-reviews" | "given-reviews";
}>();

const route = useRoute();
const { filterState, searchParams, hasActiveFilters } = usePulseFilter();

const envelope: Ref<ListEnvelope<Post> | null> = ref(null);
const loading = ref(false);
const loadError = ref<string | null>(null);

// Discards stale responses when a fast filter/page change races an
// in-flight request.
const guard = createRequestGuard();

// Username/mode change (navigating between "received" and "given" pages,
// or to another profile) must drop the previous list immediately — a
// stale envelope from the old scope must never flash while the new one
// is loading.
watch(
  () => `${props.username}:${props.mode}`,
  () => {
    envelope.value = null;
    loadError.value = null;
  },
);

async function fetch() {
  const requestId = guard.next();
  loading.value = true;
  try {
    const apiParams: Parameters<typeof gameApi.getRatedPosts>[0] = {
      sortBy: searchParams.value.sortBy ?? "lastreview",
      sortOrder: searchParams.value.sortOrder ?? "desc",
      hasReviews: true,
    };

    // User-facing filters (search/rating/dates/game) — shared logic
    // with /pulse. The author filter is deliberately not passed through: the scope
    // is imposed by the route and set below.
    if (searchParams.value.search) apiParams.search = searchParams.value.search;
    if (searchParams.value.minRating !== undefined)
      apiParams.minRating = searchParams.value.minRating;
    if (searchParams.value.maxRating !== undefined)
      apiParams.maxRating = searchParams.value.maxRating;
    if (searchParams.value.createdFrom)
      apiParams.createdAfter = new Date(
        `${searchParams.value.createdFrom}T00:00:00Z`,
      ).toISOString();
    if (searchParams.value.createdTo)
      apiParams.createdBefore = new Date(
        `${searchParams.value.createdTo}T23:59:59.999Z`,
      ).toISOString();
    if (searchParams.value.gameId) apiParams.gameId = searchParams.value.gameId;

    // Scope is the only thing the modes differ in.
    if (props.mode === "received") {
      apiParams.authorUsernames = props.username;
    } else {
      apiParams.reviewerUsername = props.username;
    }

    // Pagination (server-side). `searchParams.size` is always filled by
    // `usePulseFilter` from `entitiesPerPage` → no fallback needed.
    const pageSize = searchParams.value.size!;
    apiParams.take = pageSize;
    if (searchParams.value.number && searchParams.value.number > 1) {
      apiParams.skip = (searchParams.value.number - 1) * pageSize;
    }

    const { data, error } = await gameApi.getRatedPosts(apiParams);
    if (!guard.isCurrent(requestId)) return;
    if (error) {
      // Keep any already-shown posts (stale-while-revalidate); the
      // ErrorState banner renders independently above the list — see
      // template — matching /pulse's error-does-not-hide-content pattern.
      loadError.value = "Не удалось загрузить оцененные посты";
    } else {
      loadError.value = null;
      envelope.value = (data as ListEnvelope<Post>) ?? null;
    }
  } finally {
    if (guard.isCurrent(requestId)) loading.value = false;
  }
}

useFetchData(
  () => fetch(),
  [
    {
      // URL + username fully define the request. We use a JSON
      // string so `useFetchData` compares by value.
      param: () =>
        JSON.stringify({ q: route.query, u: props.username, m: props.mode }),
      callback: () => fetch(),
    },
  ],
);

const items = computed(() => envelope.value?.resources ?? []);
const paging = computed(() => envelope.value?.paging ?? null);
const isEmpty = computed(
  () => envelope.value !== null && items.value.length === 0,
);

const emptyText = computed(() => {
  if (hasActiveFilters.value) return "Постов по заданным фильтрам не найдено";
  return props.mode === "received"
    ? "У пользователя пока нет оцененных постов"
    : "Пользователь пока никого не оценивал";
});

const pagingTo = computed(() => ({
  name: props.routeName,
  params: { username: props.username },
}));

// Paging scrolls the posts list back into view (not the page top)
const listRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return listRef.value;
}
</script>

<template>
  <div class="user-rated-posts-list">
    <PulseFilter class="filters" hide-author-filter />

    <!-- Error banner — independent of the list, matches /pulse: never
         hides already-loaded posts on a failed refetch. -->
    <ErrorState
      v-if="loadError"
      class="error-banner"
      :message="loadError"
      :retry="fetch"
    />

    <GamePostSkeleton v-if="loading && !envelope" :count="5" />

    <SecondaryText v-else-if="isEmpty">
      {{ emptyText }}
    </SecondaryText>

    <template v-else-if="items.length">
      <PagingWithSeparators
        v-if="paging && paging.pages && paging.pages > 1"
        :paging="paging"
        :to="pagingTo"
        :use-query="true"
        :scroll-anchor="pagingAnchor"
      />

      <div
        ref="listRef"
        class="posts-list"
        :class="{ 'with-paging': paging && paging.pages && paging.pages > 1 }"
      >
        <GamePost
          v-for="post in items"
          :key="post.id"
          :post="post"
          show-navigation
          :search-query="filterState.search"
        />
      </div>

      <PagingWithSeparators
        v-if="paging && paging.pages && paging.pages > 1"
        :paging="paging"
        :to="pagingTo"
        :use-query="true"
        :scroll-anchor="pagingAnchor"
      />
    </template>
  </div>
</template>

<style scoped lang="sass">
.user-rated-posts-list
  display: flex
  flex-direction: column
  gap: $small

.filters
  margin-bottom: $medium

.error-banner
  margin-bottom: $medium

.posts-list
  display: flex
  flex-direction: column
  gap: $medium

  // Container gap ($small) + this margin = $medium between the paging
  // blocks and the posts — same rhythm as between the posts themselves.
  // Only when paging is actually rendered (flex gaps don't collapse
  // margins, so an unconditional margin would add dead space otherwise).
  &.with-paging
    margin: $small 0
</style>

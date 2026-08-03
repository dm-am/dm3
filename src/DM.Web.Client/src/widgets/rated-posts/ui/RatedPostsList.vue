<script setup lang="ts">
/**
 * RatedPostsList — the one list of posts that got reviews.
 *
 * Four pages show it: the two profile subpages ("Полученные оценки постов" and
 * "Поставленные оценки постов"), a game's "Оцененные посты" and the moderation
 * worklist. Two of them were built whole — the shared filter, server paging
 * above and below, a skeleton, an error banner that does not hide the posts
 * already on screen — and two fetched one fixed page and drew it bare, which on
 * the game page meant a room filter computed on the client over a truncated
 * hundred posts.
 *
 * A caller chooses the scope (whose posts, which game) and the copy around it.
 * Everything else lives here.
 *
 * Architecture:
 *  - `usePulseFilter` owns the URL state of the reader's filters, shared with
 *    /pulse; `buildRatedPostsParams` turns it and the scope into the request.
 *  - `useFetchData` re-runs that request on a route or scope change.
 *  - `GamePost` draws a post — the same component as on /pulse.
 */
import { computed, ref, watch, type Ref } from "vue";
import { useRoute, type RouteLocationRaw } from "vue-router";
import { buildRatedPostsParams, gameApi } from "@/entities/game";
import type { Post, PulseSearchParams, RatedPostsScope } from "@/entities/game";
import type { ListEnvelope } from "@/shared/api/models/common";
import { PulseFilter, usePulseFilter } from "@/features/pulse-filter";
import { GamePost } from "@/widgets/game-post/@x/rated-posts";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import { SecondaryText } from "@/shared/ui/Layout";
import { ErrorState } from "@/shared/ui/ErrorState";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";

const props = withDefaults(
  defineProps<{
    /** Whose posts the list is about — the route's word, not the reader's. */
    scope: RatedPostsScope;
    /** The page the list stands on, so its paging links point back at it. */
    pagingTo: RouteLocationRaw;
    /** Empty state while nothing is filtered; a filtered miss is worded here. */
    emptyText: string;
    /** Hide the "Авторы" filter where the scope already fixes the author. */
    hideAuthorFilter?: boolean;
    /** Breadcrumb depth of a post: "room" on a page that already is the game. */
    navigationLevel?: "game" | "room";
  }>(),
  { hideAuthorFilter: false, navigationLevel: "game" },
);

const route = useRoute();
const { filterState, searchParams, hasActiveFilters } = usePulseFilter();

const envelope: Ref<ListEnvelope<Post> | null> = ref(null);
const loading = ref(false);
const loadError = ref<string | null>(null);

// Discards stale responses when a fast filter/page change races an
// in-flight request.
const guard = createRequestGuard();

const scopeKey = computed(() => JSON.stringify(props.scope));

// A scope change (another profile, another game) drops the previous list at
// once: an envelope from the old scope must never flash while the new one
// loads.
watch(scopeKey, () => {
  envelope.value = null;
  loadError.value = null;
});

/**
 * A hidden filter is not applied. The author scope is imposed by the route on
 * the profile subpages and its button is hidden there, so an `?authors=` typed
 * into the address bar must not narrow the list invisibly either.
 */
const requestFilters = computed<PulseSearchParams>(() =>
  props.hideAuthorFilter
    ? { ...searchParams.value, authorUsernames: undefined }
    : searchParams.value,
);

async function fetch() {
  const requestId = guard.next();
  loading.value = true;
  try {
    const { data, error } = await gameApi.getRatedPosts(
      buildRatedPostsParams(requestFilters.value, props.scope),
    );
    if (!guard.isCurrent(requestId)) return;
    if (error) {
      // Keep any already-shown posts (stale-while-revalidate); the ErrorState
      // banner renders independently above the list — see template — matching
      // /pulse's error-does-not-hide-content pattern.
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
      // URL + scope fully define the request. We use a JSON string so
      // `useFetchData` compares by value.
      param: () => JSON.stringify({ q: route.query, s: props.scope }),
      callback: () => fetch(),
    },
  ],
);

const items = computed(() => envelope.value?.resources ?? []);
const paging = computed(() => envelope.value?.paging ?? null);
const isEmpty = computed(
  () => envelope.value !== null && items.value.length === 0,
);

const emptyMessage = computed(() =>
  hasActiveFilters.value
    ? "Постов по заданным фильтрам не найдено"
    : props.emptyText,
);

// Paging scrolls the posts list back into view (not the page top)
const listRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return listRef.value;
}
</script>

<template>
  <div class="rated-posts-list">
    <PulseFilter class="filters" :hide-author-filter="hideAuthorFilter" />

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
      {{ emptyMessage }}
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
          :navigation-level="navigationLevel"
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
.rated-posts-list
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

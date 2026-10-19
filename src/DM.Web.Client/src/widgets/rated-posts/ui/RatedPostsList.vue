<script setup lang="ts">
/** The one list of posts that got reviews; a caller chooses the scope and the words. */
import { computed, ref, watch, type Ref } from "vue";
import { useRoute, type RouteLocationNamedRaw } from "vue-router";
import { buildRatedPostsParams, postApi } from "@/entities/game";
import type { Post, PulseSearchParams, RatedPostsScope } from "@/entities/game";
import type { ListEnvelope } from "@/shared/api/models/common";
import { PulseFilter, usePulseFilter } from "@/features/pulse-filter";
import { GamePost } from "@/widgets/game-post/@x/rated-posts";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import { SecondaryText } from "@/shared/ui/Layout";
import { ErrorState } from "@/shared/ui/ErrorState";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";

const props = withDefaults(
  defineProps<{
    /** Whose posts the list is about — the route's word, not the reader's. */
    scope: RatedPostsScope;
    /** The page the list stands on, so its paging links point back at it. */
    pagingTo: RouteLocationNamedRaw;
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

// A failure keeps the posts shown; its banner stands above the list, as on /pulse.
const {
  loading,
  error: loadError,
  clearError,
  run,
} = useGuardedRequest({ message: "Не удалось загрузить оцененные посты" });

const scopeKey = computed(() => JSON.stringify(props.scope));

// A scope change drops the previous list at once, so the old one never flashes.
watch(scopeKey, () => {
  envelope.value = null;
  clearError();
});

/** A hidden filter is not applied: an ?authors= typed in must not narrow the list unseen. */
const requestFilters = computed<PulseSearchParams>(() =>
  props.hideAuthorFilter
    ? { ...searchParams.value, authorUsernames: undefined }
    : searchParams.value,
);

function loadPosts() {
  return run(
    () =>
      postApi.getRatedPosts(
        buildRatedPostsParams(requestFilters.value, props.scope),
      ),
    (data) => {
      envelope.value = data;
    },
  );
}

useFetchData(loadPosts, [
  {
    // A string, so useFetchData compares the address and the scope by value.
    param: () => JSON.stringify({ q: route.query, s: props.scope }),
    callback: loadPosts,
  },
]);

const items = computed(() => envelope.value?.resources ?? []);
const paging = computed(() => envelope.value?.paging ?? null);
const hasPages = computed(() => (paging.value?.pages ?? 0) > 1);
const isEmpty = computed(
  () => envelope.value !== null && items.value.length === 0,
);

const emptyMessage = computed(() =>
  hasActiveFilters.value
    ? "Постов по заданным фильтрам не найдено"
    : props.emptyText,
);

// Paging scrolls the list back into view, not the page top.
const listRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return listRef.value;
}
</script>

<template>
  <div class="rated-posts-list">
    <PulseFilter class="filters" :hide-author-filter="hideAuthorFilter" />

    <!-- A banner of its own: it does not hide the posts already shown -->
    <ErrorState
      v-if="loadError"
      class="error-banner"
      :message="loadError"
      :retry="loadPosts"
    />

    <GamePostSkeleton v-if="loading && !envelope" :count="5" />

    <SecondaryText v-else-if="isEmpty">
      {{ emptyMessage }}
    </SecondaryText>

    <template v-else-if="items.length">
      <PagingWithSeparators
        v-if="paging && hasPages"
        :paging="paging"
        :to="pagingTo"
        :use-query="true"
        :scroll-anchor="pagingAnchor"
      />

      <div
        ref="listRef"
        class="posts-list"
        :class="{ 'with-paging': hasPages }"
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
        v-if="paging && hasPages"
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

  // The gap and this margin make $medium to the paging blocks; only when they are drawn.
  &.with-paging
    margin: $small 0
</style>

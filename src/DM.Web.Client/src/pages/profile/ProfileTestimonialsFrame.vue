<script setup lang="ts" generic="T extends { id: string | number }">
/**
 * ProfileTestimonialsFrame — the shared frame of the profile's
 * testimonial-like lists (endorsements, game reviews): the filter bar, the
 * guarded fetch keyed to the URL, the skeleton/empty/error states and the
 * paging above and below the items.
 *
 * A caller chooses the endpoint (`fetch-page`), the card (the `item` slot) and
 * the texts; the frame owns everything the two lists used to spell twice.
 *
 * Architecture:
 *  - `useTestimonialsFilter` manages the URL state (search + sort) — both
 *    lists carry the same state in the URL and the same page size.
 *  - `useFetchData` listens to route.query (+ username/mode via a closure)
 *    and re-runs the fetch.
 *  - Server-side pagination — paging info comes from the ListEnvelope.
 */
import { ref, computed, watch, type Ref } from "vue";
import { useRoute } from "vue-router";
import {
  TestimonialsFilter,
  useTestimonialsFilter,
  SORT_OPTIONS,
} from "@/features/testimonial-filter";
import type { SortOption } from "@/shared/ui/Filters";
import type { ApiResult, ListEnvelope } from "@/shared/api/models/common";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import { SecondaryText } from "@/shared/ui/Layout";
import { ErrorState } from "@/shared/ui/ErrorState";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { TestimonialSkeleton } from "@/entities/testimonial";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";

const props = defineProps<{
  username: string;
  /** Which side of the pair the list shows; part of the refetch key. */
  mode: "received" | "written";
  /** The current page's route name — for Paging.to. */
  routeName: string;
  /** The text `ErrorState` shows when a page fails to load. */
  errorMessage: string;
  /** Empty state while nothing is filtered; a filtered miss is worded apart. */
  emptyText: string;
  /** Empty state when the search filtered everything out. */
  emptyFilteredText: string;
  /** Loads one page for the current query; the frame decides when. */
  fetchPage: (q: {
    search?: string;
    sortBy?: "created" | "author";
    sortOrder?: "asc" | "desc";
    number?: number;
    take?: number;
  }) => Promise<ApiResult<ListEnvelope<T>>>;
  /**
   * Relabels the "Автор" sort option (label + hint) where the author is the
   * profile owner on every row and the backend already sorts the scope by the
   * other side of the pair. The sortBy value sent stays "author".
   */
  authorSortOverride?: { label: string; hint: string };
  searchPlaceholder?: string;
}>();

defineSlots<{
  /** One list item; the frame draws the dash separators between items. */
  item: (props: { item: T; searchQuery: string }) => void;
}>();

const route = useRoute();

const { filterState, searchParams, hasActiveFilters } = useTestimonialsFilter();

const envelope: Ref<ListEnvelope<T> | null> = ref(null);

// Keeps any already-shown items on a failure (stale-while-revalidate); the
// ErrorState banner renders independently above the list — see template — so
// the previous message is not cleared until the next answer lands.
const {
  loading,
  error: loadError,
  clearError,
  run,
} = useGuardedRequest({ message: () => props.errorMessage });

// Username/mode change (navigating between "received" and "written" pages, or
// to another profile) must drop the previous list immediately.
watch(
  () => `${props.username}:${props.mode}`,
  () => {
    envelope.value = null;
    clearError();
  },
);

function fetch() {
  return run(
    () =>
      props.fetchPage({
        search: searchParams.value.search,
        sortBy: searchParams.value.sortBy,
        sortOrder: searchParams.value.sortOrder,
        number: searchParams.value.number,
        take: searchParams.value.size,
      }),
    (data) => {
      envelope.value = data;
    },
  );
}

useFetchData(
  () => fetch(),
  [
    {
      // The URL is the single source of truth for filter/paging: route.query
      // changed → refetch. Username/mode come in via the props closure so they
      // are covered too (mode switches received/written without changing
      // route.query, hence explicitly part of the key).
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

const emptyStateText = computed(() =>
  hasActiveFilters.value ? props.emptyFilteredText : props.emptyText,
);

const sortOptions = computed<SortOption[] | undefined>(() => {
  const override = props.authorSortOverride;
  if (!override) return undefined;
  return SORT_OPTIONS.map((o) =>
    o.value === "author"
      ? {
          value: o.value,
          label: override.label,
          hint: override.hint,
          defaultDirection: o.defaultDirection,
        }
      : {
          value: o.value,
          label: o.label,
          hint: o.hint,
          defaultDirection: o.defaultDirection,
        },
  );
});

const pagingTo = computed(() => ({
  name: props.routeName,
  params: { username: props.username },
}));

// Paging scrolls the list block (top paging + cards) back into view instead
// of the page top.
const listRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return listRef.value;
}
</script>

<template>
  <div class="testimonials-list">
    <TestimonialsFilter
      :sort-options="sortOptions"
      :search-placeholder="searchPlaceholder"
    />

    <!-- Error banner — independent of the list, matches /pulse: never hides
         already-loaded items on a failed refetch. 404-vs-failure distinction
         (does this profile exist at all) is resolved one level up by
         useProfileSubpageUser; this ErrorState is purely for "the request to
         load the list failed". -->
    <ErrorState
      v-if="loadError"
      class="error-banner"
      :message="loadError"
      :retry="fetch"
    />

    <TestimonialSkeleton v-if="loading && !envelope" :count="3" />

    <SecondaryText v-else-if="isEmpty">
      {{ emptyStateText }}
    </SecondaryText>

    <div v-else-if="items.length" ref="listRef" class="list">
      <PagingWithSeparators
        v-if="paging"
        :paging="paging"
        :to="pagingTo"
        :use-query="true"
        :scroll-anchor="pagingAnchor"
      />

      <div class="list-items">
        <template v-for="(item, idx) in items" :key="item.id">
          <slot name="item" :item="item" :search-query="filterState.search" />
          <DashSeparator v-if="idx < items.length - 1" spacing="tiny" />
        </template>
      </div>

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
.testimonials-list
  display: flex
  flex-direction: column
  gap: $small

.error-banner
  margin-bottom: $medium

// Paging blocks sit $medium from the items; the tight $tiny rhythm between
// items and their dash separators lives on the inner wrapper.
.list
  display: flex
  flex-direction: column
  gap: $medium
  margin-top: $medium

.list-items
  display: flex
  flex-direction: column
  gap: $tiny
</style>

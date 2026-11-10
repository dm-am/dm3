<script setup lang="ts" generic="T extends { id: string | number }">
/**
 * The shared frame of the profile's testimonial-like lists: the filter, the
 * fetch keyed to the address, the states and the paging. A caller gives the
 * endpoint, the card (the `item` slot) and the texts.
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
import type { SortDirection } from "@/shared/lib/filters/types";
import type { TestimonialSortBy } from "@/features/testimonial-filter";
import type { TestimonialSide } from "./profileSubpages";

const props = defineProps<{
  username: string;
  /** Which side of the pair the list shows; part of the refetch key. */
  mode: TestimonialSide;
  /** The route of the page, for the paging links. */
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
    sortBy?: TestimonialSortBy;
    sortOrder?: SortDirection;
    number?: number;
    take?: number;
  }) => Promise<ApiResult<ListEnvelope<T>>>;
  /** Relabels the "Автор" sort where every row's author is the owner; the value sent stays "author". */
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

// A failure keeps the items already shown; the banner stands above them.
const {
  loading,
  error: loadError,
  clearError,
  run,
} = useGuardedRequest({ message: () => props.errorMessage });

// Another profile or the other side of the pair drops the list at once.
watch(
  () => `${props.username}:${props.mode}`,
  () => {
    envelope.value = null;
    clearError();
  },
);

function loadPage() {
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
  () => loadPage(),
  [
    {
      // The address holds the filter and the page; the mode is part of the key, since it changes no query.
      param: () =>
        JSON.stringify({ q: route.query, u: props.username, m: props.mode }),
      callback: () => loadPage(),
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
      ? { ...o, label: override.label, hint: override.hint }
      : o,
  );
});

const pagingTo = computed(() => ({
  name: props.routeName,
  params: { username: props.username },
}));

// Paging scrolls the list back into view, not the page top.
const listRef = ref<HTMLElement | null>(null);
</script>

<template>
  <div class="testimonials-list">
    <TestimonialsFilter
      :sort-options="sortOptions"
      :search-placeholder="searchPlaceholder"
    />

    <!-- Never hides the items already loaded; a profile that does not exist is a 404 one level up. -->
    <ErrorState
      v-if="loadError"
      class="error-banner"
      :message="loadError"
      :retry="loadPage"
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
        :scroll-anchor="() => listRef"
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
        :scroll-anchor="() => listRef"
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

// The paging $medium from the items; the $tiny rhythm of items and dashes is the inner wrapper's.
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

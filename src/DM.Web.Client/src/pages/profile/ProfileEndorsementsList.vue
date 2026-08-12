<script setup lang="ts">
/**
 * ProfileEndorsementsList — the shared endorsements list with filter, search,
 * sorting and pagination. Used by both pages:
 * "Полученные рекомендации" and "Написанные рекомендации".
 *
 * Differences between the modes:
 *  - endpoint: `getUserEndorsements` (received) vs
 *    `getWrittenUserEndorsements` (written);
 *  - the route name for Paging links (the `routeName` prop);
 *  - sorting/search: in "written" the "Автор" column is relabeled to
 *    "Получатель" (label + hint + search placeholder, item "68г") — the value
 *    "author" itself is sent to the backend as is; for this scope the backend already
 *    silently sorts by the counterparty (see UserEndorsementFilter.cs).
 * Everything else is shared, the card footer included: both modes hand
 * `<TestimonialCard>` the same `about` (the endorsement recipient), so both
 * pages read "<автор> о <получатель>" — the author (the one "speaking" in
 * the bubble) as a regular link, the recipient as a muted one. The line is
 * composed in exactly one place, inside the card. It used to be passed in
 * "written" only, and the two pages then spelled one footer two ways:
 * "SolohinLex о TestSeniorMod" on one, a bare "TestHonorary" on the other.
 * The filter bar, URL state and pagination are shared as well.
 *
 * Architecture:
 *  - `useTestimonialsFilter` manages the URL state (search + sort).
 *  - `useFetchData` listens to route.query (+ username/mode via a closure)
 *    and re-runs the fetch.
 *  - Server-side pagination — paging info comes from the ListEnvelope.
 *  - Rendered via `<TestimonialCard>` (the same component as on
 *    /about/testimonials and the endorsements tab — a single visual
 *    unit for all "testimonial-like" entities); the text is plain text.
 */
import { ref, computed, watch, type Ref } from "vue";
import { useRoute } from "vue-router";
import { userApi } from "@/entities/user";
import type {
  UserEndorsement,
  Username,
  WebsiteTestimonial,
} from "@/shared/api/models/community";
import type { ListEnvelope } from "@/shared/api/models/common";
import {
  TestimonialsFilter,
  useTestimonialsFilter,
  SORT_OPTIONS,
} from "@/features/testimonial-filter";
import type { SortOption } from "@/shared/ui/Filters";
import { TestimonialCard, TestimonialSkeleton } from "@/entities/testimonial";
import PagingWithSeparators from "@/shared/ui/Paging/PagingWithSeparators.vue";
import { SecondaryText } from "@/shared/ui/Layout";
import { ErrorState } from "@/shared/ui/ErrorState";
import { DashSeparator } from "@/shared/ui/DashSeparator";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";

const props = defineProps<{
  username: string;
  /**
   * "received" — endorsements received by this user (they are the recipient).
   * "written"  — endorsements written by this user (they are the author).
   * Determines which of userApi.getUserEndorsements /
   * getWrittenUserEndorsements is called, and (via `routeName` below)
   * where the pagination links lead.
   */
  mode: "received" | "written";
  /** The current page's route name — for Paging.to. */
  routeName: "received-endorsements" | "given-endorsements";
}>();

const route = useRoute();

const { filterState, searchParams, hasActiveFilters } = useTestimonialsFilter();

const envelope: Ref<ListEnvelope<UserEndorsement> | null> = ref(null);

// Keeps any already-shown items on a failure (stale-while-revalidate); the
// ErrorState banner renders independently above the list — see template — so
// the previous message is not cleared until the next answer lands.
const {
  loading,
  error: loadError,
  clearError,
  run,
} = useGuardedRequest({ message: "Не удалось загрузить рекомендации" });

// Username/mode change (navigating between "received" and "written" pages,
// or to another profile) must drop the previous list immediately.
watch(
  () => `${props.username}:${props.mode}`,
  () => {
    envelope.value = null;
    clearError();
  },
);

function fetch() {
  // Do not detach the method — `getUserEndorsements`/`getWrittenUserEndorsements`
  // call `this.buildEndorsementParams(q)`, and a detached `const fn =
  // userApi.getX` loses `this` and crashes with a TypeError.
  const params = {
    search: searchParams.value.search,
    sortBy: searchParams.value.sortBy,
    sortOrder: searchParams.value.sortOrder,
    number: searchParams.value.number,
    take: searchParams.value.size,
  };
  return run(
    () =>
      props.mode === "received"
        ? userApi.getUserEndorsements(props.username as Username, params)
        : userApi.getWrittenUserEndorsements(
            props.username as Username,
            params,
          ),
    (data) => {
      envelope.value = data;
    },
  );
}

useFetchData(
  () => fetch(),
  [
    {
      // The URL is the single source of truth for filter/paging:
      // route.query changed → refetch. Username/mode come in via
      // the props closure so they are covered too (mode switches
      // received/given without changing route.query, hence explicitly part of the key).
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

const emptyText = computed(() =>
  hasActiveFilters.value
    ? "Рекомендаций по заданным фильтрам не найдено"
    : props.mode === "received"
      ? "У пользователя пока нет рекомендаций"
      : "Пользователь пока не писал рекомендаций",
);

/**
 * Written mode: the "Автор" sort option and default search
 * placeholder both talk about the wrong party — every item's author IS
 * the profile owner, so sorting/searching "by author" is meaningless here.
 * The backend already silently sorts this scope by the counterparty (see
 * UserEndorsementFilter.cs), so only the FE label/hint/placeholder need
 * relabeling to "Получатель" — the sortBy value sent to the API is
 * unchanged ("author").
 */
const sortOptionsOverride = computed<SortOption[] | undefined>(() => {
  if (props.mode !== "written") return undefined;
  return SORT_OPTIONS.map((o) =>
    o.value === "author"
      ? {
          value: o.value,
          label: "Получатель",
          hint: "По имени получателя",
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

const searchPlaceholder = computed(() =>
  props.mode === "written" ? "Поиск по тексту или получателю" : undefined,
);

/**
 * UserEndorsement and WebsiteTestimonial are structurally compatible
 * (id / author / text / createdUtc / modifiedUtc). `<TestimonialCard>`
 * reads only these fields — we project at the boundary instead of duplicating
 * the visuals.
 */
function asTestimonial(e: UserEndorsement): WebsiteTestimonial {
  return e as unknown as WebsiteTestimonial;
}

/** Recipient of the endorsement — the muted recipient link in the
 * "<author> о <recipient>" footer line, in both modes. In "received" the
 * recipient is the profile owner, and the server sends targetUser either
 * way (GET users/{name}/endorsements carries author AND targetUser). */
function targetOf(e: UserEndorsement) {
  return e.targetUser;
}

const pagingTo = computed(() => ({
  name: props.routeName,
  params: { username: props.username },
}));

// Paging scrolls the endorsements block (top paging + rows) back into
// view instead of the page top.
const listRef = ref<HTMLElement | null>(null);
function pagingAnchor(): HTMLElement | null {
  return listRef.value;
}
</script>

<template>
  <div class="user-endorsements-list">
    <TestimonialsFilter
      :sort-options="sortOptionsOverride"
      :search-placeholder="searchPlaceholder"
    />

    <!-- Error banner — independent of the list, matches /pulse: never
         hides already-loaded items on a failed refetch. 404-vs-failure
         distinction (does this profile exist at all) is resolved one
         level up by useProfileSubpageUser; this ErrorState is purely for
         "the request to load endorsements failed". -->
    <ErrorState
      v-if="loadError"
      class="error-banner"
      :message="loadError"
      :retry="fetch"
    />

    <TestimonialSkeleton v-if="loading && !envelope" :count="3" />

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

      <div class="list-items">
        <template v-for="(item, idx) in items" :key="item.id">
          <TestimonialCard
            :testimonial="asTestimonial(item)"
            :search-query="filterState.search"
            :about="targetOf(item)"
          />
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
.user-endorsements-list
  display: flex
  flex-direction: column
  gap: $small

.error-banner
  margin-bottom: $medium

// Paging blocks sit $medium from the endorsement rows; the tight $tiny
// rhythm between rows and their dash separators lives on the inner wrapper.
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

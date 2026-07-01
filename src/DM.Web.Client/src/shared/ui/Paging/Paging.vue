<template>
  <nav
    v-if="localPaging.pages > 1"
    ref="pagingRef"
    class="paging"
    aria-label="Пагинация"
  >
    <!-- First page << -->
    <Tooltip v-if="showFirst" text="Первая страница">
      <router-link
        :to="getPageLink(1)"
        class="nav-button"
        @click="prematureUpdate(1)"
        >&lt;&lt;</router-link
      >
    </Tooltip>

    <!-- Left ellipsis -->
    <Tooltip v-if="showLeftEllipsis" text="Назад">
      <router-link
        :to="getPageLink(leftEllipsisTarget)"
        class="nav-button"
        @click="prematureUpdate(leftEllipsisTarget)"
        >...</router-link
      >
    </Tooltip>

    <!-- Page numbers -->
    <router-link
      v-for="page in pageNumbers"
      :key="page"
      :to="getPageLink(page)"
      :class="['page-number', { active: page === localPaging.current }]"
      :aria-current="page === localPaging.current ? 'page' : undefined"
      @click="prematureUpdate(page)"
      >{{ page }}</router-link
    >

    <!-- Right ellipsis -->
    <Tooltip v-if="showRightEllipsis" text="Вперед">
      <router-link
        :to="getPageLink(rightEllipsisTarget)"
        class="nav-button"
        @click="prematureUpdate(rightEllipsisTarget)"
        >...</router-link
      >
    </Tooltip>

    <!-- Last page >> -->
    <Tooltip v-if="showLast" text="Последняя страница">
      <router-link
        :to="getPageLink(localPaging.pages)"
        class="nav-button"
        @click="prematureUpdate(localPaging.pages)"
        >&gt;&gt;</router-link
      >
    </Tooltip>
  </nav>
</template>

<script setup lang="ts">
import type { Paging } from "@/shared/api/models/common";
import { computed, ref, watch, onMounted, onUnmounted } from "vue";
import { useRoute } from "vue-router";
import { Tooltip } from "@/shared/ui/Tooltip";
import { scrollContentToTop } from "@/shared/lib/scroll";

const props = withDefaults(
  defineProps<{
    paging: Paging;
    to: any;
    /** Use query param instead of route param for page number */
    useQuery?: boolean;
    /** Query param key (default: "number") */
    queryKey?: string;
    /** Callback to prefetch next page when pagination becomes visible */
    onPrefetch?: (page: number) => void;
  }>(),
  {
    useQuery: false,
    queryKey: "number",
    onPrefetch: undefined,
  },
);

const route = useRoute();

// Ref for Intersection Observer (prefetch)
const pagingRef = ref<HTMLElement | null>(null);
let prefetchedPage: number | null = null;

// Intersection Observer for prefetch
let observer: IntersectionObserver | null = null;

onMounted(() => {
  if (!props.onPrefetch) return;

  observer = new IntersectionObserver(
    (entries) => {
      const entry = entries[0];
      if (entry?.isIntersecting) {
        const nextPage = localPaging.value.current + 1;
        // Only prefetch if there's a next page and we haven't prefetched it yet
        if (
          nextPage <= localPaging.value.pages &&
          prefetchedPage !== nextPage
        ) {
          prefetchedPage = nextPage;
          props.onPrefetch?.(nextPage);
        }
      }
    },
    { rootMargin: "200px" }, // Start prefetch 200px before pagination is visible
  );

  if (pagingRef.value) {
    observer.observe(pagingRef.value);
  }
});

onUnmounted(() => {
  observer?.disconnect();
});

// Reset prefetched page when current page changes
watch(
  () => props.paging.current,
  () => {
    prefetchedPage = null;
  },
);

// Reactive local copy of paging for immediate UI updates
const localPaging = ref<Paging>({
  current: 1,
  size: 10,
  pages: 1,
  number: 1,
  total: 0,
});

watch(
  () => props.paging,
  (paging) => {
    localPaging.value = { ...paging };
  },
  { immediate: true },
);

// Optimistic update when clicking pagination link
const prematureUpdate = (page: number) => {
  localPaging.value = { ...localPaging.value, current: page };
  // Query-based paging doesn't change the route path, so the router's
  // scroll handling won't fire — scroll the content container ourselves
  scrollContentToTop();
};

// Calculate window bounds (always 10 pages or fewer)
function getWindowBounds(paging: Paging): {
  lowerBound: number;
  upperBound: number;
} {
  const total = paging.pages;
  const current = paging.current;

  if (total <= 10) {
    return { lowerBound: 1, upperBound: total };
  }

  if (current <= 5) {
    // Near the start: show pages 1-10
    return { lowerBound: 1, upperBound: 10 };
  }

  if (current > total - 5) {
    // Near the end: show last 10 pages
    return { lowerBound: total - 9, upperBound: total };
  }

  // Middle: center the current page
  return { lowerBound: current - 4, upperBound: current + 5 };
}

// Page numbers to display
const pageNumbers = computed(() => {
  const paging = localPaging.value;
  if (!paging.pages || paging.pages < 1) {
    return [];
  }

  const { lowerBound, upperBound } = getWindowBounds(paging);

  const pages: number[] = [];
  for (let i = lowerBound; i <= upperBound; i++) {
    pages.push(i);
  }
  return pages;
});

// Window bounds for visibility checks
const windowBounds = computed(() => getWindowBounds(localPaging.value));

// Show << when not displaying first page
const showFirst = computed(() => {
  return localPaging.value.pages > 10 && windowBounds.value.lowerBound > 1;
});

// Show >> when not displaying last page
const showLast = computed(() => {
  return (
    localPaging.value.pages > 10 &&
    windowBounds.value.upperBound < localPaging.value.pages
  );
});

// Left ellipsis: same condition as <<
const showLeftEllipsis = computed(() => showFirst.value);

// Right ellipsis: same condition as >>
const showRightEllipsis = computed(() => showLast.value);

// Left ellipsis target: last page before visible window
const leftEllipsisTarget = computed(() => {
  return windowBounds.value.lowerBound - 1;
});

// Right ellipsis target: first page after visible window
const rightEllipsisTarget = computed(() => {
  return windowBounds.value.upperBound + 1;
});

// Generate link for a page number
function getPageLink(page: number) {
  if (props.useQuery) {
    // Use page number directly for query-based pagination
    const newQuery = { ...route.query, [props.queryKey]: String(page) };
    if (page === 1) {
      delete newQuery[props.queryKey];
    }
    return {
      name: props.to.name,
      params: props.to.params,
      query: newQuery,
    };
  } else {
    // Legacy: use entity number for path-based pagination
    const entityNumber = (page - 1) * localPaging.value.size + 1;
    return {
      name: props.to.name,
      params: Object.assign({}, props.to.params, {
        n: entityNumber,
      }),
    };
  }
}
</script>

<style scoped lang="sass">
@import "src/assets/styles/Paging"

+paging
</style>

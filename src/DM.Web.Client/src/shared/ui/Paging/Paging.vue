<template>
  <nav
    v-if="localPaging.pages > 1"
    ref="pagingRef"
    class="paging"
    aria-label="Пагинация"
  >
    <!-- Every item is followed by a zero-width .copy-space text node
         (a preserved " " at font-size: 0): invisible in layout, but
         Selection.toString() emits it as a real space, so the strip
         copies as "<< ... 4 5 6 ... >>" on one line instead of one
         page number per line (same idiom as Tabs.vue, zero-width
         variant because the visible spacing comes from margins). -->

    <!-- First page << -->
    <template v-if="showFirst">
      <Tooltip text="Первая страница">
        <router-link
          :to="getPageLink(1)"
          class="nav-button"
          aria-label="Первая страница"
          @click="prematureUpdate(1)"
          >&lt;&lt;</router-link
        ></Tooltip
      ><span class="copy-space">{{ " " }}</span>
    </template>

    <!-- Left ellipsis -->
    <template v-if="showLeftEllipsis">
      <Tooltip text="Назад">
        <router-link
          :to="getPageLink(leftEllipsisTarget)"
          class="nav-button"
          aria-label="Назад"
          @click="prematureUpdate(leftEllipsisTarget)"
          >...</router-link
        ></Tooltip
      ><span class="copy-space">{{ " " }}</span>
    </template>

    <!-- Page numbers -->
    <template v-for="page in pageNumbers" :key="page">
      <router-link
        :to="getPageLink(page)"
        :class="['page-number', { active: page === localPaging.current }]"
        :aria-label="`Страница ${page}`"
        :aria-current="page === localPaging.current ? 'page' : undefined"
        @click="prematureUpdate(page)"
        >{{ page }}</router-link
      ><span class="copy-space">{{ " " }}</span>
    </template>

    <!-- Right ellipsis -->
    <template v-if="showRightEllipsis">
      <Tooltip text="Вперед">
        <router-link
          :to="getPageLink(rightEllipsisTarget)"
          class="nav-button"
          aria-label="Вперед"
          @click="prematureUpdate(rightEllipsisTarget)"
          >...</router-link
        ></Tooltip
      ><span class="copy-space">{{ " " }}</span>
    </template>

    <!-- Last page >> -->
    <Tooltip v-if="showLast" text="Последняя страница">
      <router-link
        :to="getPageLink(localPaging.pages)"
        class="nav-button"
        aria-label="Последняя страница"
        @click="prematureUpdate(localPaging.pages)"
        >&gt;&gt;</router-link
      >
    </Tooltip>
  </nav>
</template>

<script setup lang="ts">
import type { PagingInfo } from "@/shared/api/models/common";
import { computed, ref, watch, onMounted, onUnmounted } from "vue";
import { useRoute } from "vue-router";
import { Tooltip } from "@/shared/ui/Tooltip";
import { scrollBlockIntoView, scrollContentToTop } from "@/shared/lib/scroll";
import {
  PAGES_WIDE,
  pagesThatFit,
  pageWindow,
  type PageWindow,
  type StripMeasure,
} from "./pageWindow";

const props = withDefaults(
  defineProps<{
    paging: PagingInfo;
    to: any;
    /** Use query param instead of route param for page number */
    useQuery?: boolean;
    /** Query param key (default: "number") */
    queryKey?: string;
    /** Callback to prefetch next page when pagination becomes visible */
    onPrefetch?: (page: number) => void;
    /**
     * Getter for the top of the paginated block (table/list). When set,
     * paging scrolls that block into view (scrollBlockIntoView) instead of
     * jumping to the top of the page content. A getter — not a raw element —
     * so it survives the consumer re-rendering the block between clicks.
     */
    scrollAnchor?: () => HTMLElement | null;
  }>(),
  {
    useQuery: false,
    queryKey: "number",
    onPrefetch: undefined,
    scrollAnchor: undefined,
  },
);

const route = useRoute();

// The strip: watched for the prefetch, and measured for the numbers it holds
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

// As many numbers as the line holds, up to ten (pageWindow.ts). The labels are
// measured in the face they are drawn in, the line is the strip's own width:
// the same strip stands in a list, under a table and in a column of comments,
// and a window width would say nothing about any of them.
const fitted = ref(PAGES_WIDE);

/** What the strip has to lay out, or null where nothing is laid out (a test). */
function measure(): StripMeasure | null {
  const strip = pagingRef.value;
  // Any link but the current page, which is bold.
  const link = strip?.querySelector("a:not(.active)");
  const line = strip?.getBoundingClientRect().width ?? 0;
  if (!strip || !link || line <= 0) return null;
  const pen = document.createElement("canvas").getContext("2d");
  if (!pen) return null;
  const style = getComputedStyle(link);
  // The side of a link, one at every width (_Paging.sass, --paging-side).
  const sides = getComputedStyle(strip);
  const px = (value: string) => parseFloat(value);
  const margins =
    (px(style.getPropertyValue("margin-left")) || 0) +
    (px(style.getPropertyValue("margin-right")) || 0);
  const side = px(sides.getPropertyValue("--paging-side")) || 0;
  const width = (text: string, weight: string) => {
    pen.font = `${weight} ${style.fontSize} ${style.fontFamily}`;
    return pen.measureText(text).width;
  };
  // The widest digit, since a strip that moves on may show any of them.
  const label = (weight: string) =>
    String(localPaging.value.pages).length *
    Math.max(...[..."0123456789"].map((digit) => width(digit, weight)));
  const regular = label(style.fontWeight);
  const around = margins + 2 * side;
  return {
    // Each label is rounded up to the 1/64 of a pixel the layout counts in:
    // fourteen of them can take a quarter of a pixel more than measured.
    room: line - 0.25,
    number: regular + around,
    current: label("700") - regular,
    jumps:
      2 * (width("<<", style.fontWeight) + around) +
      2 * (width("...", style.fontWeight) + around),
  };
}

function refit() {
  const measured = measure();
  fitted.value = measured
    ? pagesThatFit(localPaging.value.pages, measured)
    : PAGES_WIDE;
}

// Measured again when the line changes width (a phone turned sideways, a
// sidebar folded away), when the face arrives, and when the count of pages
// gains a digit. The strip comes and goes with the count, so the observer
// follows the element rather than the mount.
const resizeObserver =
  typeof ResizeObserver === "undefined" ? null : new ResizeObserver(refit);
watch(
  pagingRef,
  (strip, gone) => {
    if (gone) resizeObserver?.unobserve(gone);
    if (strip) {
      resizeObserver?.observe(strip);
      refit();
    }
  },
  { flush: "post" },
);
onMounted(() => {
  void document.fonts?.ready.then(refit);
});
onUnmounted(() => resizeObserver?.disconnect());

/** How many numbers the strip shows. */
const windowSize = computed(() => fitted.value);

// Reset prefetched page when current page changes
watch(
  () => props.paging.current,
  () => {
    prefetchedPage = null;
  },
);

// Reactive local copy of paging for immediate UI updates
const localPaging = ref<PagingInfo>({
  current: 1,
  skip: 0,
  take: 10,
  pages: 1,
  total: 0,
});

watch(
  () => props.paging,
  (paging) => {
    localPaging.value = { ...paging };
  },
  { immediate: true },
);

// Another count of pages takes another fit: a label one digit longer is
// another measure (pages 99 to 100), and the count itself decides whether the
// strip shows whole and which side its links take (three pages and twelve).
watch(() => localPaging.value.pages, refit, { flush: "post" });

// Optimistic update when clicking pagination link
const prematureUpdate = (page: number) => {
  localPaging.value = { ...localPaging.value, current: page };
  // Query-based paging doesn't change the route path, so the router's
  // scroll handling won't fire — scroll ourselves. With an anchor the
  // paginated block is brought into view (no-op when its top is already
  // visible); without one, legacy behavior: content container to top.
  const anchor = props.scrollAnchor?.();
  if (anchor) {
    scrollBlockIntoView(anchor);
  } else {
    scrollContentToTop();
  }
};

// The first and the last number of the strip.
function getWindowBounds(paging: PagingInfo): PageWindow {
  return pageWindow(paging.current, paging.pages, windowSize.value);
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
  return (
    localPaging.value.pages > windowSize.value &&
    windowBounds.value.lowerBound > 1
  );
});

// Show >> when not displaying last page
const showLast = computed(() => {
  return (
    localPaging.value.pages > windowSize.value &&
    windowBounds.value.upperBound < localPaging.value.pages
  );
});

// Left ellipsis: same condition as <<, at every width. Where the line is short
// the window gives up numbers, not the dots.
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
    const entityNumber = (page - 1) * localPaging.value.take + 1;
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
@use "@/assets/styles/Paging" as *

+paging
</style>

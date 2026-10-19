<script setup lang="ts">
import type { PagingInfo } from "@/shared/api/models/common";
import { computed, ref, watch, onMounted, onUnmounted } from "vue";
import { useRoute, type RouteLocationNamedRaw } from "vue-router";
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
    /** The list's route, by name and params; the strip adds the page to it. */
    to: RouteLocationNamedRaw;
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

const pagingRef = ref<HTMLElement | null>(null);
let prefetchedPage: number | null = null;

let observer: IntersectionObserver | null = null;

// As many numbers as the strip's own width holds, up to ten (pageWindow.ts): the same strip stands in a
// list, under a table and in a column of comments, so a window width says nothing.
const fitted = ref(PAGES_WIDE);

function measure(): StripMeasure | null {
  const strip = pagingRef.value;
  // Any link but the current page, which is bold.
  const link = strip?.querySelector("a:not(.active)");
  const line = strip?.getBoundingClientRect().width ?? 0;
  if (!strip || !link || line <= 0) return null;
  const pen = document.createElement("canvas").getContext("2d");
  if (!pen) return null;
  const style = getComputedStyle(link);
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
    // Layout rounds each label up to 1/64px: fourteen of them take a quarter of a pixel more.
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

// The strip comes and goes with the count, so the observer follows the element, not the mount.
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
  if (!props.onPrefetch) return;

  observer = new IntersectionObserver(
    (entries) => {
      const entry = entries[0];
      if (entry?.isIntersecting) {
        const nextPage = localPaging.value.current + 1;
        if (
          nextPage <= localPaging.value.pages &&
          prefetchedPage !== nextPage
        ) {
          prefetchedPage = nextPage;
          props.onPrefetch?.(nextPage);
        }
      }
    },
    { rootMargin: "200px" },
  );

  if (pagingRef.value) {
    observer.observe(pagingRef.value);
  }
});

onUnmounted(() => {
  observer?.disconnect();
  resizeObserver?.disconnect();
});

watch(
  () => props.paging.current,
  () => {
    prefetchedPage = null;
  },
);

const localPaging = ref<PagingInfo>({ ...props.paging });

watch(
  () => props.paging,
  (paging) => {
    localPaging.value = { ...paging };
  },
);

// A label one digit longer is another measure (pages 99 to 100).
watch(() => localPaging.value.pages, refit, { flush: "post" });

const prematureUpdate = (page: number) => {
  localPaging.value = { ...localPaging.value, current: page };
  // Query paging leaves the route path as it is, so the router's scroll handling never fires.
  const anchor = props.scrollAnchor?.();
  if (anchor) {
    scrollBlockIntoView(anchor);
  } else {
    scrollContentToTop();
  }
};

function getWindowBounds(paging: PagingInfo): PageWindow {
  return pageWindow(paging.current, paging.pages, fitted.value);
}

const pageNumbers = computed(() => {
  const { lowerBound, upperBound } = getWindowBounds(localPaging.value);

  const pages: number[] = [];
  for (let i = lowerBound; i <= upperBound; i++) {
    pages.push(i);
  }
  return pages;
});

const windowBounds = computed(() => getWindowBounds(localPaging.value));

// The dots stand with << and >>: where the line is short the window gives up numbers, not the dots.
const showFirst = computed(() => {
  return (
    localPaging.value.pages > fitted.value && windowBounds.value.lowerBound > 1
  );
});

const showLast = computed(() => {
  return (
    localPaging.value.pages > fitted.value &&
    windowBounds.value.upperBound < localPaging.value.pages
  );
});

const leftEllipsisTarget = computed(() => {
  return windowBounds.value.lowerBound - 1;
});

const rightEllipsisTarget = computed(() => {
  return windowBounds.value.upperBound + 1;
});

function getPageLink(page: number) {
  if (props.useQuery) {
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

<template>
  <nav
    v-if="localPaging.pages > 1"
    ref="pagingRef"
    class="paging"
    aria-label="Пагинация"
  >
    <!-- A zero-width .copy-space after every item: the strip copies as "<< ... 4 5 6 ... >>" on one line. -->
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

    <template v-if="showFirst">
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

    <template v-if="showLast">
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

<style scoped lang="sass">
@use "@/assets/styles/Paging" as *

+paging
</style>

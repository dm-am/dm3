<template>
  <div
    v-if="testimonials.length > 0"
    ref="galleryRef"
    class="testimonials-gallery"
    @mouseenter="isHovered = true"
    @mouseleave="isHovered = false"
    @focusin="onFocusIn"
    @focusout="onFocusOut"
  >
    <Transition
      name="testimonial-fade"
      mode="out-in"
      appear
      @after-enter="onTestimonialEntered"
    >
      <TestimonialCard
        :key="currentIndex"
        ref="testimonialRef"
        :testimonial="currentTestimonial"
        :expandable="true"
      />
    </Transition>
  </div>

  <ErrorState
    v-else-if="loadFailed"
    message="Не удалось загрузить отзывы"
    :retry="() => loadTestimonials()"
  />

  <secondary-text v-else-if="loaded"> Отзывов пока нет </secondary-text>

  <!-- Reserves space equal to one collapsed testimonial bubble so the
       content below does not jump up when the testimonials array arrives. -->
  <TestimonialSkeleton v-else />
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from "vue";
import { communityApi } from "@/shared/api";
import type { WebsiteTestimonial } from "@/shared/api/models/community";
import { useTestimonialStore } from "@/entities/testimonial";
import { TestimonialCard, TestimonialSkeleton } from "@/entities/testimonial";
import { ErrorState } from "@/shared/ui/ErrorState";

const store = useTestimonialStore();

const testimonials = ref<WebsiteTestimonial[]>([]);
const currentIndex = ref(0);
const totalTestimonials = ref(0);
const loading = ref(false);
const loaded = ref(false);
const loadFailed = ref(false);

const currentTestimonial = computed(
  () => testimonials.value[currentIndex.value] ?? null,
);
const isHovered = ref(false);
const isFocusWithin = ref(false);
const hasSelection = ref(false);
const galleryRef = ref<HTMLElement | null>(null);
const testimonialRef = ref<InstanceType<typeof TestimonialCard> | null>(null);

const hasMore = computed(
  () => testimonials.value.length < totalTestimonials.value,
);

async function loadTestimonials() {
  loadFailed.value = false;
  // Use store for initial load (benefits from 60s cache)
  const ok = await store.fetchTestimonials({ take: 10 }, true);
  const data = store.testimonials;
  if (data?.resources?.length) {
    testimonials.value = [...data.resources]; // Copy to local ref for pagination
    totalTestimonials.value = data.paging?.total ?? data.resources.length;
    // Start with a random testimonial
    currentIndex.value = Math.floor(Math.random() * data.resources.length);
  } else if (!ok) {
    loadFailed.value = true;
  }
  loaded.value = true;
}

/**
 * Load the next page of testimonials into the local gallery list.
 * Returns true only when the list actually grew, so callers can keep
 * currentIndex inside the array bounds when the request fails.
 */
async function loadMoreTestimonials(): Promise<boolean> {
  if (loading.value || !hasMore.value) return false;
  loading.value = true;
  try {
    const { data, error } = await communityApi.getTestimonials({
      take: 10,
      number: Math.floor(testimonials.value.length / 10) + 1,
    });
    if (error || !data?.resources?.length) return false;
    testimonials.value = [...testimonials.value, ...data.resources];
    totalTestimonials.value = data.paging?.total ?? totalTestimonials.value;
    return true;
  } finally {
    loading.value = false;
  }
}

async function nextTestimonial() {
  if (currentIndex.value < testimonials.value.length - 1) {
    currentIndex.value++;
  } else if (hasMore.value) {
    // Advance only when the list actually grew — on a failed page load
    // wrap to the first testimonial instead of stepping out of bounds.
    const grew = await loadMoreTestimonials();
    currentIndex.value = grew ? currentIndex.value + 1 : 0;
  } else {
    // Cycle to first
    currentIndex.value = 0;
  }
}

// Auto-rotate testimonials every 10 seconds (pause when hovered,
// focused within, text selected, or the current testimonial is expanded —
// swapping it out mid-read would be jarring).
const AUTO_ROTATE_INTERVAL = 10000;
let autoRotateTimer: ReturnType<typeof setInterval> | null = null;

function shouldPauseRotation() {
  return (
    isHovered.value ||
    isFocusWithin.value ||
    hasSelection.value ||
    testimonialRef.value?.isExpanded === true
  );
}

// Pause rotation while keyboard focus is inside the gallery (WCAG 2.2.2):
// otherwise the focused element unmounts mid-reading on the next tick.
function onFocusIn() {
  isFocusWithin.value = true;
}

function onFocusOut(event: FocusEvent) {
  const next = event.relatedTarget as Node | null;
  if (!next || !galleryRef.value?.contains(next)) {
    isFocusWithin.value = false;
  }
}

function startAutoRotate() {
  stopAutoRotate();
  autoRotateTimer = setInterval(() => {
    if (!shouldPauseRotation() && testimonials.value.length > 1) {
      nextTestimonial();
    }
  }, AUTO_ROTATE_INTERVAL);
}

function stopAutoRotate() {
  if (autoRotateTimer) {
    clearInterval(autoRotateTimer);
    autoRotateTimer = null;
  }
}

function checkSelection() {
  const selection = document.getSelection();
  if (!selection || selection.toString().length === 0) {
    hasSelection.value = false;
    return;
  }
  // Only pause if selection is within the gallery
  if (galleryRef.value && selection.anchorNode) {
    hasSelection.value = galleryRef.value.contains(selection.anchorNode);
  } else {
    hasSelection.value = false;
  }
}

// Pause auto-rotate when tab is hidden (saves CPU/battery)
function handleVisibilityChange() {
  if (document.hidden) {
    stopAutoRotate();
  } else {
    startAutoRotate();
  }
}

onMounted(() => {
  loadTestimonials();
  startAutoRotate();
  document.addEventListener("selectionchange", checkSelection);
  document.addEventListener("visibilitychange", handleVisibilityChange);
});

onUnmounted(() => {
  stopAutoRotate();
  document.removeEventListener("selectionchange", checkSelection);
  document.removeEventListener("visibilitychange", handleVisibilityChange);
});

// Called when new testimonial element has finished entering (after Transition)
function onTestimonialEntered() {
  // Measure content after transition completes
  testimonialRef.value?.measureContent();
}
</script>

<style scoped lang="sass">
.testimonials-gallery
  position: relative
  margin: 0 0 $small

// Testimonial transition animation - entire component as one unit.
//
// NOTE: no `:deep(*) transition: none` killer here. It used to disable
// child transitions during the fade, but children are static during
// enter/leave anyway (new testimonial mounts collapsed, old one
// unmounts as-is). Worse: when a fade phase got stuck (background tab
// throttling leaves the -active class on the element), the killer
// permanently disabled the expand/collapse animation of the bubble —
// collapse became instant while expand stayed smooth.
.testimonial-fade-enter-active,
.testimonial-fade-leave-active
  transition: opacity 0.3s ease, transform 0.3s ease
  // Force single compositing layer for entire testimonial
  will-change: opacity, transform

.testimonial-fade-enter-from
  opacity: 0
  transform: translateX(16px)

.testimonial-fade-leave-to
  opacity: 0
  transform: translateX(-16px)

// Respect reduced motion preference
@media (prefers-reduced-motion: reduce)
  .testimonial-fade-enter-active,
  .testimonial-fade-leave-active
    transition: opacity 0.15s ease
  .testimonial-fade-enter-from,
  .testimonial-fade-leave-to
    transform: none
</style>

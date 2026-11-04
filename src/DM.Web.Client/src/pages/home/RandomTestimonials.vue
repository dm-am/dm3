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

  <!-- One collapsed bubble, so the content below does not jump. -->
  <TestimonialSkeleton v-else />
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from "vue";
import { testimonialApi } from "@/entities/testimonial";
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
  // The store's cache serves the first page
  const ok = await store.fetchTestimonials({ take: 10 }, true);
  const data = store.testimonials;
  if (data?.resources?.length) {
    testimonials.value = [...data.resources];
    totalTestimonials.value = data.paging?.total ?? data.resources.length;
    currentIndex.value = Math.floor(Math.random() * data.resources.length);
  } else if (!ok) {
    loadFailed.value = true;
  }
  loaded.value = true;
}

/** Returns true only when the list grew, so the index stays in bounds on a failure. */
async function loadMoreTestimonials(): Promise<boolean> {
  if (loading.value || !hasMore.value) return false;
  loading.value = true;
  try {
    const { data, error } = await testimonialApi.getTestimonials({
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
    // On a failed page load wrap to the first one instead of stepping out of bounds.
    const grew = await loadMoreTestimonials();
    currentIndex.value = grew ? currentIndex.value + 1 : 0;
  } else {
    currentIndex.value = 0;
  }
}

// Held while the reader is on it: swapping it out mid-read would be jarring.
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

// WCAG 2.2.2: otherwise the focused element unmounts mid-reading.
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
  if (galleryRef.value && selection.anchorNode) {
    hasSelection.value = galleryRef.value.contains(selection.anchorNode);
  } else {
    hasSelection.value = false;
  }
}

// Saves CPU and battery in a hidden tab
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

function onTestimonialEntered() {
  testimonialRef.value?.measureContent();
}
</script>

<style scoped lang="sass">
.testimonials-gallery
  position: relative
  margin: 0 0 $small

// No `:deep(*) transition: none` here: a fade stuck in a background tab would
// keep it and switch off the bubble's expand animation for good.
.testimonial-fade-enter-active,
.testimonial-fade-leave-active
  transition: opacity $transition-slow, transform $transition-slow
  will-change: opacity, transform

.testimonial-fade-enter-from
  opacity: 0
  transform: translateX($medium)

.testimonial-fade-leave-to
  opacity: 0
  transform: translateX(-$medium)

@media (prefers-reduced-motion: reduce)
  .testimonial-fade-enter-active,
  .testimonial-fade-leave-active
    transition: opacity $transition-normal
  .testimonial-fade-enter-from,
  .testimonial-fade-leave-to
    transform: none
</style>

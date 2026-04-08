<template>
  <div
    v-if="testimonials.length > 0"
    ref="galleryRef"
    class="testimonials-gallery"
    @mouseenter="isHovered = true"
    @mouseleave="isHovered = false"
  >
    <Transition name="testimonial-fade" mode="out-in" appear @after-enter="onTestimonialEntered">
      <Testimonial
        :key="currentIndex"
        ref="testimonialRef"
        :testimonial="currentTestimonial"
        :expandable="true"
      />
    </Transition>
  </div>

  <secondary-text v-else-if="loaded && testimonials.length === 0">
    Нет отзывов о проекте
  </secondary-text>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from "vue";
import { communityApi } from "@/shared/api";
import type { WebsiteTestimonial } from "@/shared/api/models/community";
import { useTestimonialStore } from "@/shared/stores/testimonials";
import { Testimonial } from "@/entities/testimonial";

const store = useTestimonialStore();

const testimonials = ref<WebsiteTestimonial[]>([]);
const currentIndex = ref(0);
const totalTestimonials = ref(0);
const loading = ref(false);
const loaded = ref(false);

const currentTestimonial = computed(() => testimonials.value[currentIndex.value] ?? null);
const isHovered = ref(false);
const hasSelection = ref(false);
const galleryRef = ref<HTMLElement | null>(null);
const testimonialRef = ref<InstanceType<typeof Testimonial> | null>(null);

const hasMore = computed(() => testimonials.value.length < totalTestimonials.value);

async function loadTestimonials() {
  // Use store for initial load (benefits from 60s cache)
  await store.fetchTestimonials({ take: 10 });
  const data = store.testimonials;
  if (data?.resources) {
    testimonials.value = [...data.resources]; // Copy to local ref for pagination
    totalTestimonials.value = data.paging?.total ?? data.resources.length;
    // Start with a random testimonial
    currentIndex.value = Math.floor(Math.random() * data.resources.length);
  }
  loaded.value = true;
}

async function loadMoreTestimonials() {
  if (loading.value || !hasMore.value) return;
  loading.value = true;
  const { data } = await communityApi.getTestimonials({
    take: 10,
    number: Math.floor(testimonials.value.length / 10) + 1,
  });
  if (data?.resources) {
    testimonials.value = [...testimonials.value, ...data.resources];
  }
  loading.value = false;
}

const MAX_GALLERY_PAGES = 10; // Limit to 100 testimonials max

async function prevTestimonial() {
  if (currentIndex.value > 0) {
    currentIndex.value--;
  } else {
    // Cycle to last - load with limit to prevent unbounded requests
    let pagesLoaded = 0;
    while (hasMore.value && pagesLoaded < MAX_GALLERY_PAGES) {
      await loadMoreTestimonials();
      pagesLoaded++;
    }
    currentIndex.value = testimonials.value.length - 1;
  }
}

async function nextTestimonial() {
  if (currentIndex.value < testimonials.value.length - 1) {
    currentIndex.value++;
  } else if (hasMore.value) {
    await loadMoreTestimonials();
    currentIndex.value++;
  } else {
    // Cycle to first
    currentIndex.value = 0;
  }
}

// Auto-rotate testimonials every 10 seconds (pause when expanded, hovered, or text selected)
const AUTO_ROTATE_INTERVAL = 10000;
let autoRotateTimer: ReturnType<typeof setInterval> | null = null;

function shouldPauseRotation() {
  return isHovered.value || hasSelection.value;
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
@import "src/assets/styles/Variables"

.testimonials-gallery
  position: relative
  margin: 0 0 $small

// Testimonial transition animation - entire component as one unit
.testimonial-fade-enter-active,
.testimonial-fade-leave-active
  transition: opacity 0.3s ease, transform 0.3s ease
  // Force single compositing layer for entire testimonial
  will-change: opacity, transform
  // Disable all child transitions during parent animation
  :deep(*)
    transition: none !important

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

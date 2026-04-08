<script setup lang="ts">
import { ref, computed, watch, nextTick, onMounted } from "vue";
import type { WebsiteTestimonial } from "@/shared/api/models/community";
import { IconType } from "@/shared/ui/Icon/iconType";
import { Tooltip } from "@/shared/ui";
import SvgIcon from "@/shared/ui/Icon/SvgIcon.vue";
import { useTestimonialStore } from "@/shared/stores/testimonials";
import { useUserStore } from "@/entities/user";
import { userIsAdmin } from "@/entities/user";
import dayjs from "dayjs";

const props = withDefaults(
  defineProps<{
    testimonial: WebsiteTestimonial;
    /** Enable expand/collapse for long text (gallery mode) */
    expandable?: boolean;
    /** Show admin delete controls */
    controls?: boolean;
    /** Search query for highlighting */
    searchQuery?: string;
  }>(),
  {
    expandable: false,
    controls: false,
    searchQuery: undefined,
  },
);

const userStore = useUserStore();
const testimonialStore = useTestimonialStore();

const canAdministrate = computed(
  () => props.controls && userIsAdmin(userStore.user),
);
const loading = ref(false);

// Single source of truth for collapsed height calculation
const MAX_COLLAPSED_LINES = 3;
const LINE_HEIGHT = 1.5;

// Expand/collapse state
const isExpanded = ref(false);
const isOverflowing = ref(false);
const contentRef = ref<HTMLElement | null>(null);
const actualHeight = ref(0);
const enableTransition = ref(false);

// Normalize consecutive newlines for collapsed display
// Preserves single newlines, collapses 2+ newlines to single
const displayText = computed(() => {
  if (!props.expandable || isExpanded.value) {
    return props.testimonial.text;
  }
  // Collapse 2+ consecutive newlines to single newline
  return props.testimonial.text.replace(/\n{2,}/g, "\n");
});

// Date formatting
function formatDate(dateStr: string): string {
  return dayjs(dateStr).format("DD.MM.YYYY");
}

function formatFullDate(dateStr: string): string {
  return dayjs(dateStr).format("DD.MM.YYYY HH:mm");
}

// Measure overflow by comparing natural height to collapsed height
function measureContent() {
  if (!props.expandable) return;

  const el = contentRef.value;
  if (!el) {
    isOverflowing.value = false;
    actualHeight.value = 0;
    return;
  }

  nextTick(() => {
    actualHeight.value = el.scrollHeight;

    // Collapsed max-height = fontSize * lineHeight * maxLines (matches CSS calc)
    const fontSize = parseFloat(getComputedStyle(el).fontSize);
    const collapsedMaxHeight = fontSize * LINE_HEIGHT * MAX_COLLAPSED_LINES;

    // Add 1px buffer for floating-point precision issues
    isOverflowing.value = el.scrollHeight > collapsedMaxHeight + 1;
  });
}

function toggleExpand() {
  if (!isOverflowing.value) return;
  enableTransition.value = true;
  isExpanded.value = !isExpanded.value;
}

async function remove() {
  loading.value = true;
  await testimonialStore.removeTestimonial(props.testimonial.id);
  loading.value = false;
}

// Measure on mount for expandable mode
onMounted(() => {
  if (props.expandable) {
    measureContent();
  }
});

// Re-measure when testimonial changes (for gallery rotation)
watch(
  () => props.testimonial.id,
  () => {
    if (props.expandable) {
      enableTransition.value = false;
      isExpanded.value = false;
      nextTick(() => measureContent());
    }
  },
);

// Expose for parent transition callback
defineExpose({ measureContent });
</script>

<template>
  <article class="testimonial">
    <div
      class="testimonial-text"
      :class="{
        collapsed: expandable && !isExpanded && isOverflowing,
        animating: enableTransition,
      }"
      :style="expandable ? {
        '--actual-height': actualHeight + 'px',
        '--line-height': LINE_HEIGHT,
        '--max-lines': MAX_COLLAPSED_LINES,
      } : undefined"
    >
      <!-- Plain text only, NO BBCode -->
      <div ref="contentRef" class="testimonial-content">
        {{ displayText }}
      </div>
      <SvgIcon
        v-if="expandable && isOverflowing"
        name="chevronDown"
        class="expand-icon"
        :class="{ expanded: isExpanded }"
        @click="toggleExpand"
      />
    </div>
    <div class="testimonial-footer">
      <user-link
        :user="testimonial.author"
        :search-query="searchQuery"
        :hide-badge="true"
      />
      <span class="testimonial-right">
        <Tooltip :text="formatFullDate(testimonial.createdUtc)">
          <secondary-text class="testimonial-date">
            {{ formatDate(testimonial.createdUtc) }}
          </secondary-text>
        </Tooltip>
        <secondary-text v-if="canAdministrate" class="testimonial-controls">
          <a v-if="!loading" @click="remove">
            <Icon :font="IconType.Close" />
            Удалить
          </a>
          <span v-else>...</span>
        </secondary-text>
      </span>
    </div>
  </article>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.testimonial
  margin: 0

.testimonial-text
  position: relative
  display: flex
  flex-direction: column
  justify-content: center
  box-sizing: border-box
  padding: $medium + $tiny $medium + $small
  margin-bottom: $small
  border-radius: 20px
  background-color: $bg-highlight-green
  color: $text-on-green
  // Min-height for consistent visual appearance (3 lines + padding)
  min-height: calc(var(--line-height, 1.5) * var(--max-lines, 3) * 1em + $medium + $tiny + $medium + $tiny)

  .testimonial-content
    max-height: var(--actual-height, 1000px)
    overflow: hidden

  &.animating .testimonial-content
    transition: max-height 0.4s ease

  &.collapsed .testimonial-content
    max-height: calc(var(--line-height) * var(--max-lines) * 1em)

  // Speech bubble arrow (on the left side)
  &::after
    position: absolute
    top: 100%
    left: $medium
    content: ''
    border: solid 8px transparent
    border-top-color: $bg-highlight-green
    border-left-color: $bg-highlight-green

.testimonial-content
  white-space: pre-wrap
  word-wrap: break-word
  line-height: var(--line-height, 1.5)

.expand-icon
  position: absolute
  bottom: $minor + 2px
  left: 50%
  transform: translateX(-50%)
  font-size: 12px
  color: $text-on-green
  opacity: 0.7
  cursor: pointer

  &:hover
    opacity: 1

  &.expanded
    transform: translateX(-50%) rotate(180deg)

.testimonial-footer
  display: flex
  align-items: center
  justify-content: space-between
  gap: $small
  margin-top: 18px  // Clear the speech bubble arrow

.testimonial-right
  display: flex
  align-items: center
  gap: $small

.testimonial-date
  font-size: $font-size
  cursor: help

.testimonial-controls
  display: flex
  gap: $small
</style>

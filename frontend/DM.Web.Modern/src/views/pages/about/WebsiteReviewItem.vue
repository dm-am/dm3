<script setup lang="ts">
import type { WebsiteReview } from "@/api/models/community";
import { IconType } from "@/components/icons/iconType";
import { useWebsiteReviewStore, useUserStore } from "@/stores";
import { computed, ref, onMounted, watch, nextTick } from "vue";
import { userIsAdmin } from "@/api/models/community/helpers";
import { initBbcodeInteractive } from "@/utils/bbcodeInteractive";

const props = defineProps<{
  review: WebsiteReview;
  controls: boolean;
}>();
const userStore = useUserStore();
const websiteReviewStore = useWebsiteReviewStore();
const reviewTextRef = ref<HTMLElement | null>(null);

const canAdministrate = computed(
  () => props.controls && userIsAdmin(userStore.user),
);
const loading = ref(false);

async function remove() {
  loading.value = true;
  await websiteReviewStore.removeWebsiteReview(props.review.id);
  loading.value = false;
}

// Initialize interactive BBCode elements (spoilers, NSFW toggles)
onMounted(() => {
  nextTick(() => {
    initBbcodeInteractive(reviewTextRef.value);
  });
});

watch(
  () => props.review.text,
  () => {
    nextTick(() => {
      initBbcodeInteractive(reviewTextRef.value);
    });
  },
);
</script>

<template>
  <article class="website-review">
    <div ref="reviewTextRef" class="website-review-text" v-html="review.text" />
    <div class="website-review-info">
      <user-link :user="review.author" :hide-badge="true" />
      <secondary-text v-if="canAdministrate" class="website-review-controls">
        <template v-if="!loading">
          <a @click="remove">
            <the-icon :font="IconType.Close" />
            Удалить</a
          >
        </template>
        <the-loader v-else />
      </secondary-text>
    </div>
  </article>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"
@import "src/assets/styles/BbcodeContent"

.website-review
  margin: 0

.website-review-text
  +bbcode-content

  position: relative
  padding: $medium
  margin-bottom: $small

  border-radius: $border-radius
  background-color: $bg-highlight-green
  color: $text-on-green

  // Speech bubble arrow
  &::after
    position: absolute
    top: 100%
    left: $small

    content: ''
    border: solid $minor transparent
    border-top-color: $bg-highlight-green
    border-left-color: $bg-highlight-green

.website-review-info
  display: flex
  justify-content: space-between

.website-review-controls
  display: flex
  & > *
    margin-left: $small
</style>

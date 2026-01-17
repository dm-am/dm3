<script setup lang="ts">
import type { WebsiteReview } from "@/api/models/community";
import { IconType } from "@/components/icons/iconType";
import { useWebsiteReviewStore, useUserStore } from "@/stores";
import { computed, ref } from "vue";
import { userIsAdmin } from "@/api/models/community/helpers";

const props = defineProps<{
  review: WebsiteReview;
  controls: boolean;
}>();
const userStore = useUserStore();
const websiteReviewStore = useWebsiteReviewStore();

const canAdministrate = computed(
  () => props.controls && userIsAdmin(userStore.user),
);
const loading = ref(false);

async function remove() {
  loading.value = true;
  await websiteReviewStore.removeWebsiteReview(props.review.id);
  loading.value = false;
}
</script>

<template>
  <article class="website-review">
    <div class="website-review-text" v-html="review.text" />
    <div class="website-review-info">
      <user-link :user="review.author" />
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

.website-review
  margin: $medium 0

.website-review-text
  position: relative

  padding: $medium
  margin-bottom: $small

  border-radius: $border-radius
  +theme(background-color, $bg-accent-green)
  +theme(color, $text-review)

  &:after
    position: absolute
    top: 100%
    left: $small

    content: ''
    border: solid $minor transparent
    +theme(border-top-color, $bg-accent-green)
    +theme(border-left-color, $bg-accent-green)

.website-review-info
  display: flex
  justify-content: space-between

.website-review-controls
  display: flex
  & > *
    margin-left: $small
</style>

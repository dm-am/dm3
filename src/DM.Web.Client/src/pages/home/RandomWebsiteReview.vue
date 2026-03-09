<template>
  <block-title>Случайный отзыв</block-title>
  <website-review-item
    v-if="websiteReview"
    :controls="false"
    :review="websiteReview"
  />
  <secondary-text v-else-if="loaded">Нет отзывов о проекте</secondary-text>
</template>

<script setup lang="ts">
import WebsiteReviewItem from "@/pages/about/WebsiteReviewItem.vue";
import { ref, onMounted } from "vue";
import { communityApi } from "@/shared/api";

const websiteReview = ref();
const loaded = ref(false);
onMounted(async () => {
  const { data } = await communityApi.getWebsiteReviews({ size: 0 }, true);
  if (!data?.paging) {
    loaded.value = true;
    return;
  }

  const { paging } = data;
  if (paging.total === 0) {
    loaded.value = true;
    return;
  }

  const randomNumber = Math.floor(Math.random() * paging.total);
  const { data: websiteReviews } = await communityApi.getWebsiteReviews(
    { size: 1, skip: randomNumber },
    true,
  );
  websiteReview.value = websiteReviews?.resources?.[0] ?? null;
  loaded.value = true;
});
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"

:deep(.website-review)
  margin-bottom: $major
</style>

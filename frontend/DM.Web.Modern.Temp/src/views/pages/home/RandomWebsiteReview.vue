<template>
  <block-title>Случайный отзыв</block-title>
  <the-website-review v-if="websiteReview" :controls="false" :review="websiteReview" />
  <secondary-text v-else-if="loaded">Пока тут ничего нет...</secondary-text>
  <the-loader v-else />
</template>

<script setup lang="ts">
import TheWebsiteReview from "@/views/pages/about/TheWebsiteReview.vue";
import { ref, onMounted } from "vue";
import communityApi from "@/api/requests/communityApi";

const websiteReview = ref();
const loaded = ref(false);
onMounted(async () => {
  const { data } = await communityApi.getWebsiteReviews({ size: 0 }, true);
  const { paging } = data!;

  if (paging!.total === 0) {
    loaded.value = true;
    return;
  }

  const randomNumber = Math.floor(Math.random() * paging!.total);
  const { data: websiteReviews } = await communityApi.getWebsiteReviews(
    { size: 1, skip: randomNumber },
    true,
  );
  const { resources } = websiteReviews!;
  websiteReview.value = resources[0];
  loaded.value = true;
});
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"

:deep(.website-review)
  margin-bottom: $major
</style>

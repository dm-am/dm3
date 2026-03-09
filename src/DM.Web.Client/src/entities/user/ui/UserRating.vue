<script setup lang="ts">
import type { User } from "../model/types";

defineProps<{ user: User }>();
</script>

<template>
  <router-link
    class="rating"
    :to="{ name: 'profile', params: { username: user.username } }"
  >
    <template v-if="user.rating">
      <span
        :class="{
          quality: true,
          positive: user.rating.postReviewScoreSum > 0,
          negative: user.rating.postReviewScoreSum < 0,
        }"
        >{{ user.rating.postReviewScoreSum }}</span
      >/{{ user.rating.totalPosts }}
    </template>
    <template v-else>скрыт</template>
  </router-link>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.quality
  font-weight: bold

.positive
  color: $accent-green

.negative
  color: $accent-red
</style>

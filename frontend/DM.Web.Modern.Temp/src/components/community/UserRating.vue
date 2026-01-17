<script setup lang="ts">
import type { User } from "@/api/models/community";

defineProps<{ user: User }>();
</script>

<template>
  <router-link
    class="rating"
    :to="{ name: 'profile', params: { login: user.login } }"
  >
    <template v-if="user.rating.isEnabled">
      <span
        :class="{
          quality: true,
          positive: user.rating.totalRating > 0,
          negative: user.rating.totalRating < 0,
        }"
        >{{ user.rating.totalRating }}</span
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
  +theme(color, $positive-text)

.negative
  +theme(color, $negative-text)
</style>

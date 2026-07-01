<script setup lang="ts">
/**
 * ProfileAchievements — контент таба «Достижения».
 *
 * Две независимые секции:
 *   1. Награды (ProfileAwardsSection) — курируемые, выдаются вручную.
 *   2. Достижения (ProfileAchievementsSection) — автоматически
 *      начисляются evaluator'ом (Phase 1: lazy-eval на запрос).
 *
 * Сам компонент тонкий — каждая секция самостоятельно тянет свои данные
 * (ленивая загрузка только при открытии таба, через `v-else-if` в
 * ProfilePage). Композиция > наследование.
 */
import type { User } from "@/shared/api/models/community";
import ProfileAwardsSection from "./ProfileAwardsSection.vue";
import ProfileAchievementsSection from "./ProfileAchievementsSection.vue";

defineProps<{
  username: string;
  user: User;
}>();
</script>

<template>
  <div class="profile-achievements">
    <ProfileAwardsSection :username="username" />
    <ProfileAchievementsSection :username="username" :user="user" />
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.profile-achievements
  display: flex
  flex-direction: column
  gap: $big
</style>

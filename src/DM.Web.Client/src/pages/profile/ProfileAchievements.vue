<script setup lang="ts">
/**
 * The "Достижения" tab: awards and achievements, each loading itself and
 * hiding when empty. The tab says it is empty only once both have settled so.
 */
import { ref } from "vue";
import type { User } from "@/shared/api/models/community";
import { SecondaryText } from "@/shared/ui/Layout";
import ProfileAwardsSection from "./ProfileAwardsSection.vue";
import ProfileAchievementsSection from "./ProfileAchievementsSection.vue";
import type { AchievementsBlockState } from "./achievementsTabState";

defineProps<{
  username: string;
  user: User;
}>();

const awardsState = ref<AchievementsBlockState>("loading");
const achievementsState = ref<AchievementsBlockState>("loading");
</script>

<template>
  <div class="profile-achievements">
    <ProfileAwardsSection :username="username" @state="awardsState = $event" />
    <ProfileAchievementsSection
      :username="username"
      :user="user"
      @state="achievementsState = $event"
    />
    <SecondaryText
      v-if="awardsState === 'empty' && achievementsState === 'empty'"
    >
      Пока нет наград и достижений
    </SecondaryText>
  </div>
</template>

<style scoped lang="sass">
.profile-achievements
  display: flex
  flex-direction: column
  gap: $big
</style>

<script setup lang="ts">
/**
 * ProfileAchievements — content of the "Достижения" profile tab.
 *
 * Two independent sections:
 *   1. Awards (ProfileAwardsSection) — curated, granted manually.
 *   2. Achievements (ProfileAchievementsSection) — granted automatically
 *      by the evaluator (Phase 1: lazy-eval on request).
 *
 * Each section fetches its own data (lazy: only once the tab opens, via
 * `v-else-if` in ProfilePage), renders its own BlockTitle and hides
 * itself entirely when it has nothing to show. Achievements render every
 * catalog chain (locked ones included), so that section settles "empty"
 * only when the catalog itself is empty; awards are still empty for most
 * users. The parent aggregates the `state` emits for the single case no
 * section can decide alone: both settled empty — one shared empty-state
 * text for the whole tab. The initial "loading" state guarantees that
 * text never flashes before the fetches finish.
 */
import { ref } from "vue";
import type { User } from "@/shared/api/models/community";
import { SecondaryText } from "@/shared/ui/Layout";
import ProfileAwardsSection from "./ProfileAwardsSection.vue";
import ProfileAchievementsSection from "./ProfileAchievementsSection.vue";

defineProps<{
  username: string;
  user: User;
}>();

type SectionState = "loading" | "error" | "empty" | "content";

const awardsState = ref<SectionState>("loading");
const achievementsState = ref<SectionState>("loading");
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

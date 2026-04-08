<script setup lang="ts">
import type { User, UserRef } from "../model/types";
import { UserRole } from "../model/types";
import { computed } from "vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { highlightMatch } from "@/shared/lib/utils/highlight";

/**
 * Role badge configuration (same as useUserDisplay)
 * [А], [С], [М], [Н], [Р] - gray brackets, green bold letter
 */
type RoleBadge = {
  letter: string;
  label: string;
  cssClass: string;
};

const ROLE_BADGES: Partial<Record<UserRole, RoleBadge>> = {
  [UserRole.Admin]: { letter: "А", label: "Администратор", cssClass: "role-admin" },
  [UserRole.SeniorModerator]: { letter: "С", label: "Старший модератор", cssClass: "role-senior-moderator" },
  [UserRole.Moderator]: { letter: "М", label: "Модератор", cssClass: "role-moderator" },
  [UserRole.Mentor]: { letter: "Н", label: "Наставник", cssClass: "role-mentor" },
  [UserRole.System]: { letter: "Р", label: "Робот-администратор", cssClass: "role-system" },
};

const props = defineProps<{
  user: User | UserRef;
  hideBadge?: boolean;
  searchQuery?: string;
}>();

// Highlighted username HTML
const highlightedUsername = computed(() => {
  if (!props.searchQuery) return null;
  return highlightMatch(props.user.username, props.searchQuery);
});

// Type guard to check if we have a full User (has usernameHistory property)
function isFullUser(u: User | UserRef): u is User {
  return "usernameHistory" in u;
}



// Role badge for staff and system users
const roleBadge = computed((): RoleBadge | null => {
  if (props.hideBadge) return null;
  return ROLE_BADGES[props.user.role] ?? null;
});

// Honorary badge [П] for former staff (only if no role badge)
const showHonoraryBadge = computed(() => {
  if (props.hideBadge) return false;
  if (roleBadge.value) return false; // Don't show [П] if user has role badge
  return props.user.isHonorary;
});
</script>

<template>
  <span>
    <router-link
      :to="{ name: 'profile', params: { username: props.user.username } }"
      class="user-link"
    >
      <span v-if="highlightedUsername" v-html="highlightedUsername"></span>
      <template v-else>{{ props.user.username }}</template>
    </router-link>

    <span v-if="roleBadge" class="role-badge">
      <span class="bracket">[</span>
      <Tooltip :text="roleBadge.label">
        <span class="letter" :class="roleBadge.cssClass">{{ roleBadge.letter }}</span>
      </Tooltip>
      <span class="bracket">]</span>
    </span>

    <span v-if="showHonoraryBadge" class="role-badge honorary">
      <span class="bracket">[</span>
      <Tooltip text="Почетный пользователь">
        <span class="letter">П</span>
      </Tooltip>
      <span class="bracket">]</span>
    </span>
  </span>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.user-link
  word-wrap: break-word
  // .search-highlight styled globally in Reset.sass

// Role badges: [А], [С], [М], [Н], [Р] - gray brackets, green bold letter
.role-badge
  display: inline
  margin-left: 0.35em
  white-space: nowrap

  .bracket
    color: $text-muted

  .letter
    font-weight: bold
    color: $accent-green
    cursor: help

  // Honorary badge [П] - gray letter instead of green
  &.honorary .letter
    color: $text-muted
</style>

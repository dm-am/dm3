<script setup lang="ts">
import type { User, UserRef } from "../model/types";
import { computed } from "vue";
import { Tooltip } from "@/shared/ui/Tooltip";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { getRoleBadge, type RoleBadge } from "@/shared/config/roles";

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

// Role badge for staff and system users
const roleBadge = computed((): RoleBadge | null => {
  if (props.hideBadge) return null;
  return getRoleBadge(props.user.role);
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

    <span v-if="roleBadge" class="role-badge"
      >{{ " " }}<span class="bracket">[</span
      ><Tooltip :text="roleBadge.label" focusable
        ><span class="letter" :class="roleBadge.cssClass">{{
          roleBadge.letter
        }}</span></Tooltip
      ><span class="bracket">]</span></span
    >
  </span>
</template>

<style scoped lang="sass">
.user-link
  word-wrap: break-word
  // .search-highlight styled globally in Reset.sass

// Role badges: [А], [С], [М], [Н] - gray brackets, green bold letter
.role-badge
  display: inline
  white-space: nowrap

  .bracket
    color: $text-muted

  .letter
    font-weight: bold
    color: $accent-green
    cursor: help
</style>

<script setup lang="ts">
import type { User, UserRef } from "../model/types";
import { computed } from "vue";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { getRoleBadge, type RoleBadge } from "@/shared/config/roles";
import UserRoleBadge from "./UserRoleBadge.vue";

const props = defineProps<{
  user: User | UserRef;
  hideBadge?: boolean;
  searchQuery?: string;
}>();

const highlightedUsername = computed(() => {
  if (!props.searchQuery) return null;
  return highlightMatch(props.user.username, props.searchQuery);
});

const roleBadge = computed((): RoleBadge | null => {
  if (props.hideBadge) return null;
  return getRoleBadge(props.user.role);
});
</script>

<template>
  <span>
    <router-link
      :to="{ name: 'profile', params: { username: user.username } }"
      class="user-link"
    >
      <span v-if="highlightedUsername" v-html="highlightedUsername"></span>
      <template v-else>{{ user.username }}</template>
    </router-link>

    <UserRoleBadge v-if="roleBadge" :badge="roleBadge" variant="link" />
  </span>
</template>

<style scoped lang="sass">
.user-link
  overflow-wrap: break-word
</style>

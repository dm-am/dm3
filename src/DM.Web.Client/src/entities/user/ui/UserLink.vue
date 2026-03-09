<template>
  <span :title="props.user.status">
    <router-link
      :to="{ name: 'profile', params: { username: props.user.username } }"
      class="user-link"
    >
      <span
        :style="{
          backgroundImage: props.user.smallPictureUrl
            ? `url(${props.user.smallPictureUrl})`
            : undefined,
        }"
        class="user-logo"
      />
      {{ props.user.username }}
    </router-link>

    <span v-if="badge" class="user-badge-container">
      [<span class="user-badge">{{ badge }}</span
      >]
    </span>
  </span>
</template>

<script setup lang="ts">
import type { User } from "../model/types";
import { computed } from "vue";
import { userIsAdmin, userIsModerator } from "../lib/helpers";

const props = defineProps<{ user: User; hideBadge?: boolean }>();
const badge = computed(() => {
  if (props.hideBadge) return null;
  if (userIsAdmin(props.user)) return "A";
  if (userIsModerator(props.user)) return "M";
  return null;
});
</script>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.user-link
  white-space: nowrap

.user-logo
  display: inline-block

  width: $medium
  height: $medium
  border-radius: $medium

  background: url('@/assets/images/userpic.png') 0 0 no-repeat
  vertical-align: text-bottom
  background-size: cover

.user-badge
  color: $accent-green
</style>
